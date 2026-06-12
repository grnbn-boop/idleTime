using System;
using UnityEngine;
using TMPro;
using IdleTime.Combat;
using IdleTime.Player;

namespace IdleTime.Interactions
{
    // A portal = one edge of the map TREE: it leads from the room it lives in (`room`)
    // to a neighbouring room (`destination` — a forward child, or the back parent). Its
    // gate and target scene are read straight off the RoomDefinition tree, so the asset
    // is the single source of truth: a forward portal opens once the room's shared kill
    // pool reaches that edge's killsRequired; the back portal is always open.
    //
    // While locked it plays the inactive idle and shows "<remaining>"; when open it
    // switches to the active idle, and walking into the trigger fires LevelLoader.Go to
    // the destination scene. Progress is keyed by the room's id in the static RoomProgress,
    // so it survives the death→scene-reload flow and is saved across sessions.
    //
    [RequireComponent(typeof(Animator))]
    public class PortalController : MonoBehaviour
    {
        [Header("Map")]
        [Tooltip("The room this portal LIVES in — its shared kill pool + identity.")]
        [SerializeField] private RoomDefinition room;
        [Tooltip("Where this portal leads: one of `room`'s forward children, or its back parent. " +
                 "The gate (kills required) is read from the tree edge; the back edge is always open.")]
        [SerializeField] private RoomDefinition destination;

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
        [Tooltip("If true, the player must walk into the portal's trigger to travel. If false, activation alone is enough (wire OnPortalEntered yourself).")]
        [SerializeField] private bool travelOnPlayerEnter = true;
        [Tooltip("Only fire the transition once per scene load (avoids re-triggering while standing in the trigger).")]
        [SerializeField] private bool travelOnce = true;

        // Fired when the portal flips to active. Hook VFX/SFX here.
        public static event Action<PortalController> OnPortalActivated;
        // Fired when the player travels through an active portal.
        public static event Action<PortalController> OnPortalEntered;

        public bool IsActive { get; private set; }

        private string resolvedRoomId;
        private int resolvedRequiredKills;
        private string resolvedDestinationScene;
        private bool isConfigured;

        public string RoomId => resolvedRoomId;

        // Nav-HUD label: the room this portal leads to.
        public string DisplayName => destination != null ? destination.DisplayName : "Unknown Room";

        private int activeParamHash;
        private bool hasTravelled;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (promptText == null) promptText = GetComponentInChildren<TMP_Text>();
            activeParamHash = Animator.StringToHash(activeBoolParameter);

            ResolveFromTree();
        }

        private void ResolveFromTree()
        {
            resolvedRoomId = room != null ? room.RoomId : gameObject.scene.name;
            resolvedDestinationScene = destination != null ? destination.sceneName : "";
            isConfigured = room != null && destination != null;

            if (!isConfigured)
            {
                resolvedRequiredKills = int.MaxValue;
                Debug.LogWarning($"[Portal] '{name}' needs both Room and Destination assigned.", this);
                return;
            }

            int gate = room.KillsToReach(destination);
            if (gate < 0)
            {
                Debug.LogWarning($"[Portal] '{name}': destination " +
                                 $"'{(destination != null ? destination.name : "none")}' is not a neighbour of " +
                                 $"room '{room.name}' (not a forward child or the back parent).", this);
                isConfigured = false;
                resolvedRequiredKills = int.MaxValue;
            }
            else
            {
                resolvedRequiredKills = gate;
            }
        }

        // This portal is open iff the room's (shared, persisted) kill total has reached
        // THIS edge's threshold. Threshold 0 → open from the start (the back portal).
        private bool RequirementMet => isConfigured && RoomProgress.GetKills(resolvedRoomId) >= resolvedRequiredKills;

        private void OnEnable()
        {
            // Derive state from the (persisted) room kill total — covers scene reloads on
            // death and a cold boot where SaveManager has already rehydrated RoomProgress.
            if (RequirementMet)
            {
                Activate();   // already met; just reflect it
            }
            else
            {
                IsActive = false;
                ApplyAnimatorState();
                RefreshPrompt();
                if (isConfigured) MonsterController.OnAnyDeath += HandleMonsterKilled;
            }
        }

        private void OnDisable()
        {
            MonsterController.OnAnyDeath -= HandleMonsterKilled;
        }

        private void HandleMonsterKilled(MonsterController mc)
        {
            if (!isConfigured) return;

            // Bump the room's shared total once per death (AddKillOnce dedups across the
            // other portals in this room), then gate on THIS edge's own threshold.
            // NOTE: counts every monster death in the scene. To gate on specific enemy
            // types or a spawn zone later, filter on `mc` here before counting.
            int deathToken = mc != null ? mc.GetHashCode() : 0;
            int kills = RoomProgress.AddKillOnce(resolvedRoomId, deathToken);
            if (kills >= resolvedRequiredKills)
                Activate();
            else
                RefreshPrompt();
        }

        private void Activate()
        {
            bool wasActive = IsActive;
            IsActive = true;

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
                if (!isConfigured)
                {
                    promptText.text = "";
                    return;
                }

                int remaining = Mathf.Max(0, resolvedRequiredKills - RoomProgress.GetKills(resolvedRoomId));
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
            LevelLoader.Go(resolvedDestinationScene);
        }

        // Persistence: RoomProgress survives scene reloads (static) AND sessions —
        // SaveManager serializes each room's kills into MasterSaveData and rehydrates it
        // BeforeSceneLoad, so this portal's RequirementMet check is correct on a cold boot.

        // Debug helper: force-open from a context-menu in the Inspector. Banks enough
        // kills in the room to meet this edge's threshold, so it stays open across a
        // reload (and counts toward any higher-threshold portals in the same room).
        [ContextMenu("Debug: Activate Now")]
        private void DebugActivate()
        {
            if (resolvedRoomId == null) ResolveFromTree();   // context-menu before Awake
            if (!isConfigured) return;
            if (RoomProgress.GetKills(resolvedRoomId) < resolvedRequiredKills)
                RoomProgress.Restore(resolvedRoomId, resolvedRequiredKills);
            Activate();
        }
    }
}
