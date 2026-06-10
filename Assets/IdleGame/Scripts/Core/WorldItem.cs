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

        ItemDefinition _item;
        SpriteRenderer _sr;

        // ── Simulated-physics constants ──────────────────────────────────────
        // Tweak these if the arc feels too flat/tall for world scale.
        const float SimGravity            = -14f;  // units/s²  (negative = down)
        const float BounceDamping         =  0.40f; // fraction of Y speed kept on bounce
        const float BounceXFriction       =  0.60f; // fraction of X speed kept on bounce
        const int   MaxBounces            =  2;
        const float StopVelocityThreshold =  0.8f;  // stop when bounce would be weaker than this

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _sr.sortingOrder = 20;

            // Trigger so the player walks through drops; OnMouseDown still works on triggers.
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Start()
        {
            if (_startItem != null)
                SetItem(_startItem);
        }

        public void SetItem(ItemDefinition item)
        {
            _item = item;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = item.icon;
        }

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

            float   groundY = transform.position.y;
            Vector2 pos     = (Vector2)transform.position;
            int     bounces = 0;

            while (true)
            {
                velocity.y += SimGravity * Time.deltaTime;
                pos        += velocity   * Time.deltaTime;

                if (pos.y <= groundY && velocity.y < 0f)
                {
                    pos.y = groundY;

                    bool tooWeak = Mathf.Abs(velocity.y) < StopVelocityThreshold;
                    if (tooWeak || bounces >= MaxBounces)
                    {
                        transform.position = new Vector3(pos.x, groundY, transform.position.z);
                        break;
                    }

                    velocity.y  = -velocity.y * BounceDamping;
                    velocity.x *= BounceXFriction;
                    bounces++;
                }

                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                yield return null;
            }
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

            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character != null
                && _item.equipSlot != EquipSlot.None
                && character.equipment.IsEmpty(_item.equipSlot)
                && EquipmentManager.Instance != null)
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
