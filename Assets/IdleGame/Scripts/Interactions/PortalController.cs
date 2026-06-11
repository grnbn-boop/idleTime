using System;
using UnityEngine;
using TMPro;
using IdleTime.Combat;
using IdleTime.Player;

namespace IdleTime.Interactions
{
    // The portal that gates progress to the next level. While the room hasn't met its
    // kill quota it plays the inactive idle and shows "<remaining> to advance"; once the
    // quota is met it permanently switches to the active idle and shows the enter prompt.
    // When active, the player walking into the trigger fires the level transition.
    //
    // Progress is keyed by `roomId` and stored in the static RoomProgress, so it survives
    // the death→scene-reload flow. One portal per scene = one room is the simplest mapping.
    [RequireComponent(typeof(Animator))]
    public class PortalController : MonoBehaviour
    {
        [Header("Identity & Requirement")]
        [Tooltip("Unique key for this room's progress. Keep it stable across scene reloads.")]
        [SerializeField] private string roomId = "room_01";
        [Tooltip("Kills needed in this room before the portal activates.")]
        [SerializeField] private int killsRequired = 10;

        [Header("References (auto-found if left empty)")]
        [SerializeField] private Animator animator;
        [Tooltip("Floating label above the portal. Add a TextWaveEffect to it for the per-letter sway.")]
        [SerializeField] private TMP_Text promptText;

        [Header("Animator")]
        [Tooltip("Bool parameter on the portal controller that flips inactive → active.")]
        [SerializeField] private string activeBoolParameter = "IsActive";

        [Header("Prompt text")]
        [Tooltip("{0} = remaining kills. Default shows just the number.")]
        [SerializeField] private string lockedFormat = "{0}";
        [Tooltip("Shown once unlocked. Leave empty — the active animation/color indicates it's open.")]
        [SerializeField] private string unlockedText = "";

        [Header("Transition")]
        [Tooltip("Left empty = stub (logs only). See LevelLoader.cs for the expansion options.")]
        [SerializeField] private string destinationSceneName = "";
        [Tooltip("If true, the player must walk into the portal's trigger to travel. If false, activation alone is enough (wire OnPortalEntered yourself).")]
        [SerializeField] private bool travelOnPlayerEnter = true;
        [Tooltip("Only fire the transition once per scene load (avoids re-triggering while standing in the trigger).")]
        [SerializeField] private bool travelOnce = true;

        // Fired when the portal flips to active. Hook VFX/SFX here.
        public static event Action<PortalController> OnPortalActivated;
        // Fired when the player travels through an active portal.
        public static event Action<PortalController> OnPortalEntered;

        public string RoomId => roomId;
        public bool IsActive { get; private set; }

        private int activeParamHash;
        private bool hasTravelled;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (promptText == null) promptText = GetComponentInChildren<TMP_Text>();
            activeParamHash = Animator.StringToHash(activeBoolParameter);
        }

        private void OnEnable()
        {
            // Rehydrate from the persistent store first (covers scene reloads on death).
            if (RoomProgress.IsUnlocked(roomId) || RoomProgress.GetKills(roomId) >= killsRequired)
            {
                Activate(persist: false);   // already recorded; just reflect it
            }
            else
            {
                IsActive = false;
                ApplyAnimatorState();
                RefreshPrompt();
                MonsterController.OnAnyDeath += HandleMonsterKilled;
            }
        }

        private void OnDisable()
        {
            MonsterController.OnAnyDeath -= HandleMonsterKilled;
        }

        private void HandleMonsterKilled(MonsterController _)
        {
            // NOTE: counts every monster death in the scene. With one portal per scene
            // that maps cleanly to "this room." To gate on specific enemy types or a
            // particular spawn zone later, filter on the MonsterController here.
            int kills = RoomProgress.AddKill(roomId);
            if (kills >= killsRequired)
                Activate(persist: true);
            else
                RefreshPrompt();
        }

        private void Activate(bool persist)
        {
            bool wasActive = IsActive;
            IsActive = true;
            if (persist) RoomProgress.SetUnlocked(roomId);

            MonsterController.OnAnyDeath -= HandleMonsterKilled;   // no longer counting
            ApplyAnimatorState();
            RefreshPrompt();

            if (!wasActive) OnPortalActivated?.Invoke(this);
        }

        private void ApplyAnimatorState()
        {
            if (animator != null) animator.SetBool(activeParamHash, IsActive);
        }

        private void RefreshPrompt()
        {
            if (promptText == null) return;

            if (IsActive)
            {
                promptText.text = unlockedText;
            }
            else
            {
                int remaining = Mathf.Max(0, killsRequired - RoomProgress.GetKills(roomId));
                promptText.text = string.Format(lockedFormat, remaining);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive || !travelOnPlayerEnter) return;
            if (travelOnce && hasTravelled) return;
            if (other.GetComponentInParent<ClickToMove2D>() == null) return;

            hasTravelled = true;
            OnPortalEntered?.Invoke(this);
            LevelLoader.Go(destinationSceneName);
        }

        // --- Save/load seam ----------------------------------------------------------
        // RoomProgress already survives scene reloads (it's static). To persist across
        // sessions, serialize each room's kills/unlocked into SaveData and call
        // RoomProgress.AddKill / SetUnlocked on load before the scene's portals enable.
        // -----------------------------------------------------------------------------

        // Debug helper: force-unlock from a context-menu in the Inspector.
        [ContextMenu("Debug: Activate Now")]
        private void DebugActivate() => Activate(persist: true);
    }
}
