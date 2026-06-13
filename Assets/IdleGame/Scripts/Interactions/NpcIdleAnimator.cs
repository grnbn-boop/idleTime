using UnityEngine;

namespace IdleTime.Interactions
{
    /// <summary>
    /// Generic sprite-sheet idle looper for NPCs. Cycles a frame array on a
    /// SpriteRenderer at a fixed rate. Nothing role-specific lives here — the
    /// same component drives the samurai trainer, shop NPCs, and plain talkers.
    /// </summary>
    public class NpcIdleAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 12f;
        [SerializeField] private bool playOnEnable = true;

        private int frameIndex;
        private float timer;
        private bool isPlaying;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            isPlaying = playOnEnable;
            frameIndex = 0;
            timer = 0f;
            ApplyFrame();
        }

        private void Update()
        {
            if (!isPlaying || frames == null || frames.Length <= 1 || spriteRenderer == null)
                return;

            timer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            while (timer >= frameDuration)
            {
                timer -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
                return;

            Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            if (frame != null)
                spriteRenderer.sprite = frame;
        }
    }
}
