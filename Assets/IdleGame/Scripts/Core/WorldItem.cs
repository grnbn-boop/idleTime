using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleTime.Core
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldItem : MonoBehaviour
    {
        [SerializeField] ItemDefinition _startItem;
        [SerializeField] float _fadeInDuration = 0.25f;

        [Tooltip("Denomination table used by gold drops to pick which coin sprite to show " +
                 "for a rolled amount. Only needed on prefabs spawned as currency.")]
        [SerializeField] CurrencyDatabase _currencyDatabase;

        [Tooltip("Layers treated as solid ground/platforms for landing. " +
                 "Triggers (drops, monsters) are ignored regardless of this mask.")]
        [SerializeField] LayerMask _groundMask = ~0;

        ItemDefinition _item;
        // For gold drops: the exact gold this drop is worth. -1 means "not overridden",
        // so a plain coin item is worth its own currencyValue.
        int _goldOverride = -1;
        SpriteRenderer _sr;
        ContactFilter2D _groundFilter;
        readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

        // ── Simulated-physics constants ──────────────────────────────────────
        // Tweak these if the arc feels too flat/tall for world scale.
        const float SimGravity            = -14f;  // units/s²  (negative = down)
        const float BounceDamping         =  0.40f; // fraction of Y speed kept on bounce
        const float BounceXFriction       =  0.60f; // fraction of X speed kept on bounce
        const int   MaxBounces            =  2;
        const float StopVelocityThreshold =  0.8f;  // stop when bounce would be weaker than this
        const float GroundProbeSkin       =  0.05f; // extra cast length so fast falls don't tunnel
        const float MaxFallDistance       =  60f;   // give up (settle) if no platform is found below

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _sr.sortingOrder = 20;

            // Trigger so the player walks through drops; OnMouseDown still works on triggers.
            GetComponent<Collider2D>().isTrigger = true;

            // Only solid (non-trigger) terrain counts as ground, so the cast never
            // lands on another drop, a monster, or this item's own trigger collider.
            _groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            _groundFilter.SetLayerMask(_groundMask);
        }

        void Start()
        {
            if (_startItem != null)
                SetItem(_startItem);
        }

        public void SetItem(ItemDefinition item)
        {
            _item = item;
            _goldOverride = -1;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = item.icon;
        }

        // Spawns this drop as `amount` gold, rendered as the highest-fitting coin from the
        // currency database. The drop still carries the exact amount (112 gold shows a
        // Silver coin but pays out 112 on pickup).
        public void SetGold(int amount)
        {
            var coin = _currencyDatabase != null ? _currencyDatabase.HighestDenominationFor(amount) : null;
            if (coin == null)
            {
                Debug.LogError($"[WorldItem] {name}: gold drop of {amount} has no coin to render — " +
                               "assign a Currency Database with denominations on the prefab.");
                return;
            }
            SetItem(coin);
            _goldOverride = Mathf.Max(0, amount);
        }

        // Gold this drop pays out: the override (rolled amount) if set, else the coin's own value.
        int GoldValue => _goldOverride >= 0 ? _goldOverride : (_item != null ? _item.currencyValue : 0);

        /// <summary>
        /// Fires the item upward along <paramref name="velocity"/>, fades it in,
        /// then lets it arc down and settle with a small bounce.
        /// </summary>
        public void Launch(Vector2 velocity)
        {
            SetAlpha(0f);
            StartCoroutine(LaunchRoutine(velocity));
        }

        IEnumerator LaunchRoutine(Vector2 velocity)
        {
            // Begin fading in immediately so the item appears as it rises
            StartCoroutine(FadeIn(_fadeInDuration));

            Vector2 pos      = (Vector2)transform.position;
            float   floorY   = pos.y - MaxFallDistance; // safety net if it drifts over a bottomless gap
            int     bounces  = 0;

            while (true)
            {
                Vector2 prevPos = pos;
                velocity.y += SimGravity * Time.deltaTime;
                pos        += velocity   * Time.deltaTime;

                // While falling, look for the platform surface this step would cross.
                // Casting only the per-step fall distance (plus a skin) lands the item on
                // whatever platform is directly below — and lets it keep falling past a gap
                // until it reaches a real platform, instead of floating in mid-air.
                if (velocity.y < 0f && TryGetLandingY(prevPos, pos, out float landY))
                {
                    pos.y = landY;

                    bool tooWeak = Mathf.Abs(velocity.y) < StopVelocityThreshold;
                    if (tooWeak || bounces >= MaxBounces)
                    {
                        transform.position = new Vector3(pos.x, landY, transform.position.z);
                        break;
                    }

                    velocity.y  = -velocity.y * BounceDamping;
                    velocity.x *= BounceXFriction;
                    bounces++;
                }
                else if (pos.y <= floorY)
                {
                    // No platform found anywhere below — settle rather than fall forever.
                    transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                    break;
                }

                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                yield return null;
            }
        }

        /// <summary>
        /// Returns the Y of the platform surface the item would cross while falling from
        /// <paramref name="from"/> to <paramref name="to"/>, or false if none is in range.
        /// </summary>
        bool TryGetLandingY(Vector2 from, Vector2 to, out float landingY)
        {
            float castDist = Mathf.Max(0f, from.y - to.y) + GroundProbeSkin;
            int   hitCount = Physics2D.Raycast(from, Vector2.down, _groundFilter, _groundHits, castDist);

            for (int i = 0; i < hitCount; i++)
            {
                // Only land on upward-facing surfaces (tops of platforms).
                if (_groundHits[i].normal.y > 0.5f)
                {
                    landingY = _groundHits[i].point.y;
                    return true;
                }
            }

            landingY = 0f;
            return false;
        }

        IEnumerator FadeIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            SetAlpha(1f);
        }

        void SetAlpha(float a)
        {
            Color c = _sr.color;
            c.a     = a;
            _sr.color = c;
        }

        // Hold left mouse and move over items to collect them.
        void OnMouseEnter()
        {
            if (Mouse.current.leftButton.isPressed)
                TryPickup();
        }

        void OnMouseDown() => TryPickup();

        void TryPickup()
        {
            if (_item == null) return;
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Currency never takes an inventory slot — it folds straight into the gold total.
            if (_item.IsCurrency)
            {
                PlayerManager.Instance?.AddGold(GoldValue);
                Destroy(gameObject);
                return;
            }

            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character != null
                && _item.equipSlot != EquipSlot.None
                && character.equipment.IsEmpty(_item.equipSlot)
                && EquipmentManager.Instance != null
                && EquipmentManager.Instance.CanEquip(_item, character))
            {
                EquipmentManager.Instance.Equip(_item, character);
                Destroy(gameObject);
                return;
            }

            if (Inventory.Instance != null && Inventory.Instance.AddItem(_item))
                Destroy(gameObject);
        }
    }
}
