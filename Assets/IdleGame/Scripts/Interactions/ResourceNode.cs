using UnityEngine;
using IdleTime.Core;

namespace IdleTime.Interactions
{
    // A clickable, walk-through world object the player gathers from (a tree, rock, etc.).
    // Like WorldItem it uses a TRIGGER collider so the player passes through it, but it's
    // still picked up by GatheringController's OverlapPoint click test so it can be clicked
    // to start gathering. Nodes never deplete — gathering loops until the player leaves or
    // clicks elsewhere. Authoring: drop a copy of the ResourceNode prefab into a scene and
    // set skillType + reward + sprite per instance.
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ResourceNode : MonoBehaviour
    {
        [Tooltip("Which gathering skill this node trains.")]
        [SerializeField] private GatheringSkillType skillType = GatheringSkillType.Woodcutting;
        [Tooltip("Item awarded on a successful gather. Leave empty for a skill with no drop yet (e.g. a Crafting stub).")]
        [SerializeField] private ItemDefinition reward;

        [Header("Gather Tuning")]
        [Tooltip("Base success chance before the player's stat/level bonuses (0.5 = 50%).")]
        [Range(0f, 1f)] [SerializeField] private float baseSuccessChance = 0.5f;
        [Tooltip("Seconds between gather attempts while in range.")]
        [SerializeField] private float gatherInterval = 1.5f;
        [Tooltip("How close (to the node centre) the player must be to gather.")]
        [SerializeField] private float interactRange = 2f;
        [Tooltip("How far to the side of the node the player stands while gathering.")]
        [SerializeField] private float standXOffset = 1f;

        [Header("Reward Drop")]
        [Tooltip("Speed (units/s) of the initial launch impulse for the reward drop.")]
        [SerializeField] private float dropLaunchSpeed = 5f;
        [Tooltip("Total fan angle in degrees, centred straight up, the drop can launch within.")]
        [SerializeField] private float dropArcSpreadDeg = 40f;
        [Tooltip("Offset from the node centre the drop spawns at (raise it to pop from the top).")]
        [SerializeField] private Vector2 dropOriginOffset = new Vector2(0f, 0.5f);
        [Tooltip("Click-target radius of the spawned drop (matches the WorldItem prefab's 0.15).")]
        [SerializeField] private float dropColliderRadius = 0.15f;

        [Header("Pickup Zone")]
        [Tooltip("While gathering this node, a left-click inside this box keeps the gather " +
                 "session alive (and doesn't send the player walking) so you can grab dropped " +
                 "rewards without stopping. Size it to cover where the drops land. Shown as a " +
                 "yellow wire box when the node is selected.")]
        [SerializeField] private Vector2 dropZoneSize = new Vector2(4f, 3f);
        [Tooltip("Offset of the pickup zone from the node centre. Nudge it down/up to sit over " +
                 "the ground the rewards land on.")]
        [SerializeField] private Vector2 dropZoneOffset = new Vector2(0f, -0.5f);

        public GatheringSkillType SkillType => skillType;
        public ItemDefinition Reward => reward;
        public float BaseSuccessChance => baseSuccessChance;
        public float GatherInterval => Mathf.Max(0.1f, gatherInterval);
        public float InteractRange => interactRange;

        void Awake()
        {
            // Trigger so the player walks through the node (OverlapPoint still clicks it).
            GetComponent<Collider2D>().isTrigger = true;
        }

        // Pops the reward out of the node as a floor drop, arcing up like monster loot.
        // The player must click/drag it to actually bank it (WorldItem handles pickup +
        // the full-bag check), so this is the only thing a successful gather yields in-world.
        //
        // Built in code rather than from a prefab: WorldItem.Awake self-configures the trigger
        // collider + sorting, SetItem supplies the sprite, and we borrow this node's own sprite
        // material so the drop renders identically to the scene. (Monster loot still spawns from
        // Item.prefab; the only thing that prefab adds — a currency database for gold drops —
        // gathering never needs, so no prefab wiring is required here.)
        public void SpawnReward()
        {
            if (reward == null) return;

            float angleDeg = 90f + Random.Range(-dropArcSpreadDeg * 0.5f, dropArcSpreadDeg * 0.5f);
            float rad      = angleDeg * Mathf.Deg2Rad;
            Vector2 vel    = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dropLaunchSpeed;

            var go = new GameObject($"Drop_{reward.name}");
            go.transform.position = transform.position + (Vector3)dropOriginOffset;

            var sr = go.AddComponent<SpriteRenderer>();
            var mine = GetComponent<SpriteRenderer>();
            if (mine != null) sr.sharedMaterial = mine.sharedMaterial;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = dropColliderRadius;

            // RequireComponent(SpriteRenderer, Collider2D) is already satisfied above, so
            // WorldItem.Awake runs cleanly (forces isTrigger + sortingOrder).
            var drop = go.AddComponent<WorldItem>();
            drop.SetItem(reward);
            drop.Launch(vel);
        }

        // True when worldPos lands inside this node's pickup zone — the area its rewards
        // drop into. GatheringController checks this so a click meant to grab a drop doesn't
        // end the gather session or walk the player off the node.
        public bool IsWithinDropZone(Vector2 worldPos)
        {
            Vector2 center = (Vector2)transform.position + dropZoneOffset;
            Vector2 half = dropZoneSize * 0.5f;
            return Mathf.Abs(worldPos.x - center.x) <= half.x
                && Mathf.Abs(worldPos.y - center.y) <= half.y;
        }

        // World X the player should stand at to gather: just to the side of the node,
        // on whichever side the player is approaching from.
        public float GetStandX(float playerX)
        {
            float side = Mathf.Sign(playerX - transform.position.x);
            if (side == 0f) side = 1f;
            return transform.position.x + side * standXOffset;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactRange);

            // The pickup zone: clicks here keep gathering alive (see IsWithinDropZone).
            Gizmos.color = new Color(0.95f, 0.85f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(transform.position + (Vector3)dropZoneOffset, dropZoneSize);
        }
    }
}
