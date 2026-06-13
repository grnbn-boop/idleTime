using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IdleTime.Core;
using IdleTime.Interactions;

namespace IdleTime.Player
{
    // Drives gathering from the player's side, mirroring PlayerAttack: it runs before
    // ClickToMove2D (execution order -2, ahead of PlayerAttack's -1), captures clicks that
    // land on a ResourceNode, walks the player up to the node, then loops gather attempts
    // on a timer. While gathering it floats the skill icon over the player's head and
    // pulses a placeholder VFX on the body each attempt. Clicking elsewhere / moving away
    // ends the session.
    [DefaultExecutionOrder(-2)]
    public class GatheringController : MonoBehaviour
    {
        [SerializeField] private ClickToMove2D movement;
        [SerializeField] private PlayerAttack attack;
        [SerializeField] private Camera worldCamera;

        [Header("Over-head Icon (placeholder)")]
        [Tooltip("Sprite shown floating over the player's head while gathering. Built as a coloured square if left empty.")]
        [SerializeField] private SpriteRenderer headIcon;
        [Tooltip("Desired size in WORLD units — applied independently of the (often up-scaled) " +
                 "player it parents to. Lower this if the icon looks too big.")]
        [SerializeField] private float headIconWorldScale = 1f;
        [SerializeField] private Vector2 headIconOffset = new Vector2(0f, 1.7f);
        [SerializeField] private float headIconBobHeight = 0.15f;
        [SerializeField] private float headIconBobSpeed = 3f;

        [Header("Body VFX (placeholder)")]
        [Tooltip("Sprite pulsed on the player's body each gather attempt. Built as a coloured square if left empty.")]
        [SerializeField] private SpriteRenderer bodyVfx;
        [Tooltip("Desired size in WORLD units — applied independently of the player's scale.")]
        [SerializeField] private float bodyVfxWorldScale = 1f;
        [SerializeField] private Vector2 bodyVfxOffset = new Vector2(0.4f, 0.2f);
        [SerializeField] private float vfxFlashDuration = 0.25f;
        [SerializeField] private int vfxSortingOrder = 100;

        private ResourceNode currentNode;
        private float gatherTimer;
        private float vfxTimer;
        private float bobTime;

        private readonly List<Collider2D> overlapResults = new List<Collider2D>();
        private ContactFilter2D nodeFilter;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (movement == null) movement = GetComponent<ClickToMove2D>();
            if (attack == null) attack = GetComponent<PlayerAttack>();

            // Pick up trigger colliders (ResourceNode uses a trigger); we identify the node
            // by component rather than a layer mask, like PlayerAttack does for monsters.
            nodeFilter = new ContactFilter2D { useTriggers = true };

            EnsurePlaceholderVisuals();
            if (headIcon != null) headIcon.enabled = false;
            if (bodyVfx != null) bodyVfx.enabled = false;
        }

        private void Update()
        {
            if (PlayerManager.Instance != null && PlayerManager.Instance.IsDead)
            {
                ClearNode();
                return;
            }

            ReadClickForNode();
            UpdateGatherLoop();
            UpdateVisuals();
        }

        // ── Click capture ─────────────────────────────────────────────────────────

        private void ReadClickForNode()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            Vector2 worldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            overlapResults.Clear();
            Physics2D.OverlapPoint(worldPos, nodeFilter, overlapResults);

            ResourceNode node = null;
            foreach (Collider2D col in overlapResults)
            {
                var candidate = col.GetComponent<ResourceNode>();
                if (candidate != null) { node = candidate; break; }
            }

            if (node != null)
            {
                SetNode(node);
                movement?.SuppressNextClick();   // don't also treat the click as a move command
            }
            else if (currentNode != null && currentNode.IsWithinDropZone(worldPos))
            {
                // Clicked inside the active node's pickup zone — almost certainly to grab a
                // reward that popped out. Keep gathering and swallow the move command so the
                // player stays put. WorldItem.OnMouseDown still banks the drop independently.
                movement?.SuppressNextClick();
            }
            else
            {
                // Clicked off the node and outside its pickup zone — stop gathering
                // (the click falls through to movement).
                ClearNode();
            }
        }

        // ── Gather loop ─────────────────────────────────────────────────────────────

        private void UpdateGatherLoop()
        {
            if (currentNode == null) return;

            float dist = Vector2.Distance(transform.position, currentNode.transform.position);
            if (dist > currentNode.InteractRange)
            {
                // Walk up to the node, stopping just to its side.
                movement?.SetMoveTarget(currentNode.GetStandX(transform.position.x));
                return;
            }

            movement?.ClearMoveTarget();
            movement?.FaceDirection(currentNode.transform.position.x);

            gatherTimer -= Time.deltaTime;
            if (gatherTimer <= 0f)
            {
                gatherTimer = currentNode.GatherInterval;
                PerformGather();
            }
        }

        private void PerformGather()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c == null) return;

            GatherOutcome outcome = GatheringManager.Instance != null
                ? GatheringManager.Instance.TryGather(c, currentNode.SkillType, currentNode.BaseSuccessChance)
                : GatherOutcome.Failed;

            FlashVfx();

            // On a winning roll the reward pops out of the node as a floor drop; the player
            // clicks it to bank it (a full bag is handled at pickup, not here).
            if (outcome == GatherOutcome.Success)
                currentNode.SpawnReward();
        }

        // ── Session start/stop ────────────────────────────────────────────────────

        private void SetNode(ResourceNode node)
        {
            currentNode = node;
            gatherTimer = 0f;                 // first attempt fires as soon as the player arrives
            attack?.SetAutoAttack(false);     // engaging a resource drops out of combat

            // Record this as the active character's AFK activity, so quitting while gathering
            // accrues offline gains for this skill until the player engages something else.
            PlayerManager.Instance?.ActiveCharacter?.activity.SetGathering(
                node.SkillType,
                node.Reward != null ? node.Reward.name : "",
                node.GatherInterval,
                node.BaseSuccessChance);

            if (headIcon != null)
            {
                var def = GatheringManager.Instance?.GetDefinition(node.SkillType);
                if (def != null && def.icon != null)
                {
                    headIcon.sprite = def.icon;
                    headIcon.color = Color.white;
                }
                else if (def != null)
                {
                    headIcon.color = def.placeholderColor;
                }
                headIcon.enabled = true;
            }
        }

        private void ClearNode()
        {
            if (currentNode == null) return;
            currentNode = null;
            // Stopped gathering — clear the AFK activity so we don't accrue a skill the
            // player has walked away from. (A persistent assignment UI comes in Phase 2.)
            PlayerManager.Instance?.ActiveCharacter?.activity.SetIdle();
            if (headIcon != null) headIcon.enabled = false;
            if (bodyVfx != null) bodyVfx.enabled = false;
        }

        // ── Visuals ───────────────────────────────────────────────────────────────

        private void FlashVfx()
        {
            if (bodyVfx == null) return;
            vfxTimer = vfxFlashDuration;
            bodyVfx.enabled = true;
        }

        private void UpdateVisuals()
        {
            // The icons are children of the (often up-scaled) player, so convert the desired
            // world size into a local scale that cancels the parent's scale — otherwise the
            // player's scale multiplies the icon and it balloons.
            float parentScale = Mathf.Abs(transform.lossyScale.x) < 0.0001f ? 1f : transform.lossyScale.x;

            if (headIcon != null && headIcon.enabled)
            {
                headIcon.transform.localScale = Vector3.one * (headIconWorldScale / parentScale);
                bobTime += Time.deltaTime * headIconBobSpeed;
                float bob = Mathf.Sin(bobTime) * headIconBobHeight;
                headIcon.transform.position = transform.position + (Vector3)headIconOffset + Vector3.up * bob;
            }

            if (bodyVfx != null && bodyVfx.enabled)
            {
                bodyVfx.transform.position = transform.position + (Vector3)bodyVfxOffset;
                vfxTimer -= Time.deltaTime;
                // Fade + shrink the pulse over its lifetime, then hide.
                float t = Mathf.Clamp01(vfxTimer / Mathf.Max(0.0001f, vfxFlashDuration));
                var col = bodyVfx.color; col.a = t; bodyVfx.color = col;
                bodyVfx.transform.localScale = Vector3.one * (bodyVfxWorldScale / parentScale) * Mathf.Lerp(0.4f, 1f, t);
                if (vfxTimer <= 0f) bodyVfx.enabled = false;
            }
        }

        // Builds coloured-square placeholders for the head icon and body VFX when none are
        // wired, so the system is visible immediately (same fallback spirit as InventoryUI's
        // auto-built paging controls).
        private void EnsurePlaceholderVisuals()
        {
            if (headIcon == null)
                headIcon = BuildPlaceholderRenderer("GatherHeadIcon", Color.white, vfxSortingOrder + 1);
            if (bodyVfx == null)
                bodyVfx = BuildPlaceholderRenderer("GatherBodyVFX", new Color(1f, 0.95f, 0.5f, 1f), vfxSortingOrder);
        }

        private SpriteRenderer BuildPlaceholderRenderer(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        static Sprite _placeholderSprite;
        static Sprite PlaceholderSprite
        {
            get
            {
                if (_placeholderSprite == null)
                {
                    var tex = new Texture2D(4, 4);
                    var px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels(px);
                    tex.Apply();
                    _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 8f);
                    _placeholderSprite.name = "GatherPlaceholder";
                }
                return _placeholderSprite;
            }
        }
    }
}
