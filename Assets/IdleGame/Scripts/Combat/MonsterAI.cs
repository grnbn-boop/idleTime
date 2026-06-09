using UnityEngine;
using IdleTime.Player;

namespace IdleTime.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(MonsterController))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float detectionRadius = 6f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float wanderPauseMin = 1f;
        [SerializeField] private float wanderPauseMax = 3f;

        [Header("Combat")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Edge & Wall Sensors")]
        [SerializeField] private float edgeProbeXOffset = 0.45f;
        [SerializeField] private float edgeProbeYOffset = -0.45f;
        [SerializeField] private float edgeProbeDistance = 0.4f;
        [SerializeField] private float wallProbeXOffset = 0.4f;
        [SerializeField] private float wallProbeDistance = 0.1f;
        [SerializeField] private LayerMask terrainMask;

        private enum State { Wander, Chase, Attack }

        private static readonly int XVelocityHash = Animator.StringToHash("xVelocity");
        private static readonly int AttackHash = Animator.StringToHash("attack");

        private Rigidbody2D rb;
        private MonsterController controller;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private ContactFilter2D terrainFilter;
        private ContactFilter2D solidFilter;   // all layers, no triggers — used for edge detection
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];

        private State state = State.Wander;
        private float wanderXMin;
        private float wanderXMax;
        private bool initialized;

        private float wanderTargetX;
        private float wanderPauseTimer;
        private bool isPaused;
        private float attackTimer;
        private float spawnY;
        private float desiredHorizontalVelocity;
        private float facingDir = 1f;

        private Transform playerTransform;
        private Transform cachedPlayer;
        private ClickToMove2D playerController;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            controller = GetComponent<MonsterController>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            terrainFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            terrainFilter.SetLayerMask(terrainMask);

            // Edge probe filter: solid colliders only, any layer — never picks up trigger colliders
            solidFilter = new ContactFilter2D();
            solidFilter.useTriggers = false;
            solidFilter.useLayerMask = false;
        }

        private void Start()
        {
            controller.OnRespawn += _ => ResetAI();

            // Cache player by tag so detection works even when playerLayer isn't assigned
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                cachedPlayer = playerGO.transform;
                playerController = playerGO.GetComponent<ClickToMove2D>();
            }

            // Fallback if placed directly in the scene without a spawner
            if (!initialized)
                Initialize(transform.position.y, transform.position.x - 3f, transform.position.x + 3f);

            FlipSprite();
        }

        public void Initialize(float floorY, float xMin, float xMax)
        {
            spawnY = floorY;
            wanderXMin = xMin;
            wanderXMax = xMax;
            initialized = true;
            PickNewWanderTarget();
        }

        private void Update()
        {
            if (!controller.IsAlive)
            {
                desiredHorizontalVelocity = 0f;
                UpdateAnimator();
                return;
            }

            switch (state)
            {
                case State.Wander: UpdateWander(); break;
                case State.Chase:  UpdateChase();  break;
                case State.Attack: UpdateAttack(); break;
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!controller.IsAlive) return;

            Vector2 pos = rb.position;

            // Horizontal movement — edge/wall sensor prevents walking off ledges
            if (desiredHorizontalVelocity != 0f && CanMoveInDirection(pos, Mathf.Sign(desiredHorizontalVelocity)))
            {
                pos.x += desiredHorizontalVelocity * Time.fixedDeltaTime;
                facingDir = Mathf.Sign(desiredHorizontalVelocity);
                FlipSprite();
            }

            // Lock Y to spawn floor — monsters never move vertically
            pos.y = spawnY;

            rb.MovePosition(pos);
        }

        // ── State: Wander ──────────────────────────────────────────────────────

        private void UpdateWander()
        {
            if (ScanForPlayer())
            {
                state = State.Chase;
                return;
            }

            if (isPaused)
            {
                wanderPauseTimer -= Time.deltaTime;
                if (wanderPauseTimer <= 0f)
                {
                    isPaused = false;
                    PickNewWanderTarget();
                }
                desiredHorizontalVelocity = 0f;
                return;
            }

            float dir = Mathf.Sign(wanderTargetX - transform.position.x);
            desiredHorizontalVelocity = dir * moveSpeed;

            if (Mathf.Abs(transform.position.x - wanderTargetX) < 0.2f || !CanMoveInDirection(rb.position, dir))
                BeginWanderPause();
        }

        private void PickNewWanderTarget()
        {
            wanderTargetX = Random.Range(wanderXMin, wanderXMax);
        }

        private void BeginWanderPause()
        {
            isPaused = true;
            wanderPauseTimer = Random.Range(wanderPauseMin, wanderPauseMax);
            desiredHorizontalVelocity = 0f;
        }

        // ── State: Chase ───────────────────────────────────────────────────────

        private void UpdateChase()
        {
            if (!ScanForPlayer())
            {
                state = State.Wander;
                PickNewWanderTarget();
                return;
            }

            float dist = Mathf.Abs(playerTransform.position.x - transform.position.x);
            if (dist <= attackRange)
            {
                state = State.Attack;
                desiredHorizontalVelocity = 0f;
                return;
            }

            float dir = Mathf.Sign(playerTransform.position.x - transform.position.x);

            // Stop at platform edges — enemy waits at ledge rather than chasing off it
            if (!CanMoveInDirection(rb.position, dir))
            {
                desiredHorizontalVelocity = 0f;
                return;
            }

            desiredHorizontalVelocity = dir * moveSpeed;
        }

        // ── State: Attack ──────────────────────────────────────────────────────

        private void UpdateAttack()
        {
            if (!ScanForPlayer())
            {
                state = State.Wander;
                PickNewWanderTarget();
                return;
            }

            float dist = Mathf.Abs(playerTransform.position.x - transform.position.x);
            // Use a generous exit threshold so minor knockback doesn't immediately break out of attack
            if (dist > attackRange + 0.5f)
            {
                state = State.Chase;
                return;
            }

            desiredHorizontalVelocity = 0f;

            // Face the player even while standing still
            float dir = Mathf.Sign(playerTransform.position.x - transform.position.x);
            if (dir != facingDir)
            {
                facingDir = dir;
                FlipSprite();
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                animator?.SetTrigger(AttackHash);
                controller.TriggerAttack();
                playerController?.ReceiveHit(controller.data.attack, transform.position);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool ScanForPlayer()
        {
            // Try physics layer scan first if playerLayer is configured
            if (playerLayer.value != 0)
            {
                Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
                if (hit != null) { playerTransform = hit.transform; return true; }
            }

            // Always fall through to tag-based distance check (handles missing Collider2D on player)
            if (cachedPlayer == null) { playerTransform = null; return false; }
            if (Vector2.Distance(transform.position, cachedPlayer.position) <= detectionRadius)
            {
                playerTransform = cachedPlayer;
                return true;
            }
            playerTransform = null;
            return false;
        }

        // Returns false if a wall blocks forward movement or there is no floor ahead (edge)
        private bool CanMoveInDirection(Vector2 pos, float dir)
        {
            // Wall check — horizontal box probe
            Vector2 wallOrigin = pos + new Vector2(wallProbeXOffset * dir, 0f);
            int wallHits = Physics2D.Raycast(wallOrigin, new Vector2(dir, 0f), terrainFilter, castHits, wallProbeDistance);
            for (int i = 0; i < wallHits; i++)
            {
                if (castHits[i].normal.x * dir < -0.5f) return false;
            }

            // Edge check — cast downward from just above spawnY using solidFilter (no triggers, all layers)
            // so it always finds the ground tilemap and never misdetects enemy trigger colliders
            Vector2 edgeOrigin = new Vector2(pos.x + edgeProbeXOffset * dir, spawnY + 0.2f);
            int edgeHits = Physics2D.Raycast(edgeOrigin, Vector2.down, solidFilter, castHits, edgeProbeDistance + 0.3f);
            return edgeHits > 0;
        }

        private void FlipSprite()
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = facingDir > 0f; // sprite default faces left, flip when moving right
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            bool isMoving = controller.IsAlive && state != State.Attack && Mathf.Abs(desiredHorizontalVelocity) > 0.01f;

            animator.SetFloat(XVelocityHash, isMoving ? 1f : 0f);
        }

        private void ResetAI()
        {
            state = State.Wander;
            isPaused = false;
            attackTimer = 0f;
            desiredHorizontalVelocity = 0f;
            playerTransform = null;
            playerController = null;
            PickNewWanderTarget();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
