using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleTime.Core
{
    // Orchestrates the player's death cinematic end-to-end:
    //   death → (player avatar pops, spins, falls through the world)
    //         → fade to black → reset the level → restore the player → fade back in.
    //
    // Lives on a DontDestroyOnLoad object so its coroutine survives the scene reload
    // that resets the level. Requires a ScreenFader in the scene for the fades, and
    // the active scene must be in Build Settings for the reload to work.
    public class DeathSequenceController : MonoBehaviour
    {
        public static DeathSequenceController Instance { get; private set; }

        [Header("Timing (seconds)")]
        [Tooltip("How long to let the pop/spin/fall play before fading out.")]
        [SerializeField] float fallDuration = 1.25f;
        [SerializeField] float fadeOutDuration = 0.5f;
        [Tooltip("How long to hold on black while the level is reset.")]
        [SerializeField] float blackHoldDuration = 0.4f;
        [SerializeField] float fadeInDuration = 0.5f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnPlayerDeath += BeginSequence;
        }

        void OnDestroy()
        {
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnPlayerDeath -= BeginSequence;
        }

        void BeginSequence() => StartCoroutine(Sequence());

        IEnumerator Sequence()
        {
            // 1. Let the avatar's pop/spin/fall play out (ClickToMove2D drives it).
            yield return new WaitForSeconds(fallDuration);

            // 2. Fade to black.
            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.Fade(1f, fadeOutDuration);

            // 3. Reset the level while the screen is black.
            ResetLevel();

            // Wait a frame so the reloaded scene's objects have run Awake/Start and
            // re-subscribed to PlayerManager events before we fire the respawn.
            yield return null;

            // 4. Restore the player (vitals to full, lift the dead state). The fresh
            //    avatar/UI from the reload pick this up via OnPlayerRespawn / OnStatsChanged.
            PlayerManager.Instance?.Respawn();

            // 5. Hold on black briefly, then fade back in.
            if (blackHoldDuration > 0f) yield return new WaitForSeconds(blackHoldDuration);
            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.Fade(0f, fadeInDuration);
        }

        // Reloads the active scene. Managers (PlayerManager, Inventory, Equipment,
        // SaveManager, this controller, ScreenFader) are DontDestroyOnLoad, so they
        // persist with their runtime state while monsters and the player avatar are
        // rebuilt fresh at their authored positions.
        void ResetLevel()
        {
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }
    }
}
