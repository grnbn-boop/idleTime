using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using IdleTime.Core;

namespace IdleTime.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ClickToMove2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stopDistance = 0.05f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = 18f;
        [SerializeField] private LayerMask terrainMask = ~0;
        [SerializeField] private Vector2 groundSensorOffset = new Vector2(0f, -0.66f);
        [SerializeField] private Vector2 groundSensorSize = new Vector2(0.4f, 0.08f);
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private Vector2 wallSensorOffset = new Vector2(0.46f, -0.3f);
        [SerializeField] private Vector2 wallSensorSize = new Vector2(0.08f, 0.56f);
        [SerializeField] private float wallCheckDistance = 0.08f;
        [SerializeField] private float wallJumpPreparationDuration = 0.12f;
        [SerializeField] private float wallJumpHorizontalDelay = 0.08f;
        [SerializeField] private float groundClickProjectionHeight = 4f;
        [SerializeField] private float landingPulseDuration = 0.08f;
        [SerializeField] private float landingLagDuration = 0.18f;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool flipSpriteToDirection = true;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Animator animator;

        private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
        private ContactFilter2D terrainFilter;

        [Header("Hit Response")]
        [SerializeField] private float invincibilityDuration = 0.8f;

        [Header("Death Animation")]
        [Tooltip("Upward launch speed when the death sequence starts (then gravity takes over).")]
        [SerializeField] private float deathPopSpeed = 8f;
        [Tooltip("Y-axis spin rate in degrees/sec — reads as a 2D coin-flip under the orthographic camera.")]
        [SerializeField] private float deathSpinSpeed = 720f;

        private float targetX;
        private bool hasTarget;
        private float verticalVelocity;
        private bool isGrounded;
        private bool landedThisFrame;
        private float landingPulseTimer;
        private float landingLagTimer;
        private bool isPreparingWallJump;
        private float wallJumpPreparationTimer;
        private float wallJumpHorizontalDelayTimer;
        private float horizontalVelocity;

        private float knockbackVelocityX;
        private bool isInvincible;
        private Coroutine hitFlashCoroutine;
        private bool suppressNextClick;
        private bool deathActive;   // this avatar is playing the pop/spin/fall cinematic

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            targetX = transform.position.x;

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            terrainFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            terrainFilter.SetLayerMask(terrainMask);

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (groundTilemap == null)
            {
                groundTilemap = FindFirstTerrainTilemap();
            }
        }

        private void Start()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerDeath += HandleDeath;
                PlayerManager.Instance.OnPlayerRespawn += HandleRespawn;
            }
        }

        private void OnDestroy()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerDeath -= HandleDeath;
                PlayerManager.Instance.OnPlayerRespawn -= HandleRespawn;
            }
        }

        private bool IsDead => PlayerManager.Instance != null && PlayerManager.Instance.IsDead;

        private void Update()
        {
            if (deathActive) { UpdateDeathSpin(); return; }   // cinematic owns the avatar
            if (IsDead) return;                                // dead but not animating (e.g. fresh avatar pre-respawn): no input
            ReadClickTarget();
        }

        private void FixedUpdate()
        {
            if (deathActive)
            {
                DeathFallStep(Time.fixedDeltaTime);
                return;
            }

            landedThisFrame = false;
            Vector2 position = body.position;

            isGrounded = IsTouchingGround(position);
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
            }

            float horizontalMove = GetHorizontalMove(position, Time.fixedDeltaTime);
            float verticalMove = GetVerticalMove(ref position, Time.fixedDeltaTime);

            position.x += horizontalMove;
            position.y += verticalMove;

            body.MovePosition(position);
            UpdateAnimation();
        }

        // ── External control (called by PlayerAttack) ────────────────────────────

        public void SuppressNextClick() => suppressNextClick = true;

        public void SetMoveTarget(float worldX)
        {
            targetX = worldX;
            hasTarget = true;
            if (flipSpriteToDirection && spriteRenderer != null)
                spriteRenderer.flipX = targetX < transform.position.x;
        }

        public void ClearMoveTarget() => hasTarget = false;

        public void FaceDirection(float worldX)
        {
            if (flipSpriteToDirection && spriteRenderer != null)
                spriteRenderer.flipX = worldX < transform.position.x;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void ReadClickTarget()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
            if (cameraToUse == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));

            if (!TryGetClickedGround(worldPosition, out Vector2 groundPoint))
            {
                return;
            }

            targetX = groundPoint.x;
            hasTarget = true;

            if (flipSpriteToDirection && spriteRenderer != null)
            {
                spriteRenderer.flipX = targetX < transform.position.x;
            }
        }

        private float GetHorizontalMove(Vector2 position, float deltaTime)
        {

            horizontalVelocity = 0f;
            if (!hasTarget)
            {
                isPreparingWallJump = false;
                return 0f;
            }

            float remainingDistance = targetX - position.x;
            if (Mathf.Abs(remainingDistance) <= stopDistance)
            {
                hasTarget = false;
                return 0f;
            }

            float direction = Mathf.Sign(remainingDistance);
            if (landingLagTimer > 0f)
            {
                landingLagTimer = Mathf.Max(0f, landingLagTimer - deltaTime);
                return 0f;
            }

            if (isPreparingWallJump)
            {
                wallJumpPreparationTimer = Mathf.Max(0f, wallJumpPreparationTimer - deltaTime);
                if (wallJumpPreparationTimer > 0f)
                {
                    return 0f;
                }

                isPreparingWallJump = false;
                StartJump();
                wallJumpHorizontalDelayTimer = wallJumpHorizontalDelay;
                return 0f;
            }

            if (wallJumpHorizontalDelayTimer > 0f)
            {
                wallJumpHorizontalDelayTimer = Mathf.Max(0f, wallJumpHorizontalDelayTimer - deltaTime);
                return 0f;
            }

            if (IsWallAhead(position, direction))
            {
                if (isGrounded)
                {
                    isPreparingWallJump = true;
                    wallJumpPreparationTimer = wallJumpPreparationDuration;
                    return 0f;
                }

                if (verticalVelocity <= 0f)
                {
                    return 0f;
                }
            }

            float moveDistance = Mathf.Min(Mathf.Abs(remainingDistance), moveSpeed * deltaTime);
            horizontalVelocity = direction * moveDistance / deltaTime;
            return direction * moveDistance;
        }

        private float GetVerticalMove(ref Vector2 position, float deltaTime)
        {
            bool wasGrounded = isGrounded;
            verticalVelocity -= gravity * deltaTime;
            float requestedMove = verticalVelocity * deltaTime;

            if (requestedMove <= 0f && TryGetGroundBelow(position, Mathf.Abs(requestedMove) + groundCheckDistance, out RaycastHit2D groundHit))
            {
                float feetY = position.y + groundSensorOffset.y - groundSensorSize.y * 0.5f;
                float distanceToGround = feetY - groundHit.point.y;
                float allowedFall = Mathf.Max(0f, distanceToGround);
                requestedMove = -Mathf.Min(Mathf.Abs(requestedMove), allowedFall);
                isGrounded = true;
                verticalVelocity = 0f;
            }
            else
            {
                isGrounded = false;
            }

            if (!wasGrounded && isGrounded)
            {
                landedThisFrame = true;
                landingPulseTimer = landingPulseDuration;
                landingLagTimer = landingLagDuration;
            }

            return requestedMove;
        }

        private bool TryGetClickedGround(Vector2 clickWorldPosition, out Vector2 groundPoint)
        {
            groundPoint = Vector2.zero;

            if (!TryGetClickedTile(clickWorldPosition, out Tilemap clickedTilemap, out Vector3Int clickedCell))
            {
                return false;
            }

            Vector2 rayOrigin = clickWorldPosition + Vector2.up * groundClickProjectionHeight;
            float rayDistance = groundClickProjectionHeight * 2f;
            int hitCount = Physics2D.Raycast(rayOrigin, Vector2.down, terrainFilter, castHits, rayDistance);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsOwnCollider(castHits[i].collider))
                {
                    continue;
                }

                groundPoint = castHits[i].point;
                return true;
            }

            groundPoint = GetTileTopPoint(clickedTilemap, clickedCell, clickWorldPosition.x);
            return true;
        }

        private bool TryGetClickedTile(Vector2 clickWorldPosition, out Tilemap clickedTilemap, out Vector3Int clickedCell)
        {
            clickedTilemap = null;
            clickedCell = Vector3Int.zero;

            if (groundTilemap != null && ContainsTileAtWorldPosition(groundTilemap, clickWorldPosition, out clickedCell))
            {
                clickedTilemap = groundTilemap;
                return true;
            }

            Grid grid = groundTilemap != null ? groundTilemap.GetComponentInParent<Grid>() : FindAnyObjectByType<Grid>();
            if (grid == null)
            {
                return false;
            }

            Tilemap[] tilemaps = grid.GetComponentsInChildren<Tilemap>();
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] == groundTilemap)
                {
                    continue;
                }

                if (!ContainsTileAtWorldPosition(tilemaps[i], clickWorldPosition, out clickedCell))
                {
                    continue;
                }

                clickedTilemap = tilemaps[i];
                return true;
            }

            return false;
        }

        private bool ContainsTileAtWorldPosition(Tilemap tilemap, Vector2 worldPosition, out Vector3Int cellPosition)
        {
            cellPosition = Vector3Int.zero;
            if (tilemap == null)
            {
                return false;
            }

            cellPosition = tilemap.WorldToCell(worldPosition);
            return tilemap.HasTile(cellPosition);
        }

        private Tilemap FindFirstTerrainTilemap()
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>();
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Collider2D tilemapCollider = tilemaps[i].GetComponent<Collider2D>();
                if (tilemapCollider != null && !IsOwnCollider(tilemapCollider) && IsInTerrainMask(tilemaps[i].gameObject.layer))
                {
                    return tilemaps[i];
                }
            }

            return tilemaps.Length > 0 ? tilemaps[0] : null;
        }

        private Vector2 GetTileTopPoint(Tilemap tilemap, Vector3Int cellPosition, float clickWorldX)
        {
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPosition);
            Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
            return new Vector2(clickWorldX, cellCenter.y + cellSize.y * 0.5f);
        }

        private bool IsInTerrainMask(int layer)
        {
            return (terrainMask.value & (1 << layer)) != 0;
        }

        private bool IsTouchingGround(Vector2 position)
        {
            int hitCount = CastGroundSensor(position, groundCheckDistance);
            return ContainsGroundHit(hitCount);
        }

        private bool TryGetGroundBelow(Vector2 position, float distance, out RaycastHit2D groundHit)
        {
            int hitCount = CastGroundSensor(position, distance);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsGroundHit(castHits[i]))
                {
                    groundHit = castHits[i];
                    return true;
                }
            }

            groundHit = default;
            return false;
        }

        private int CastGroundSensor(Vector2 position, float distance)
        {
            return Physics2D.BoxCast(position + groundSensorOffset, groundSensorSize, 0f, Vector2.down, terrainFilter, castHits, distance);
        }

        private bool IsWallAhead(Vector2 position, float direction)
        {
            Vector2 sensorCenter = position + new Vector2(wallSensorOffset.x * direction, wallSensorOffset.y);
            int hitCount = Physics2D.BoxCast(sensorCenter, wallSensorSize, 0f, Vector2.right * direction, terrainFilter, castHits, wallCheckDistance);
            return ContainsWallHit(hitCount, direction);
        }

        private bool ContainsGroundHit(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (IsGroundHit(castHits[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsWallHit(int hitCount, float direction)
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (IsWallHit(castHits[i], direction))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGroundHit(RaycastHit2D hit)
        {
            return !IsOwnCollider(hit.collider) && hit.normal.y > 0.5f;
        }

        private bool IsWallHit(RaycastHit2D hit, float direction)
        {
            return !IsOwnCollider(hit.collider) && hit.normal.x * direction < -0.5f;
        }

        private bool IsVerticalBlockHit(RaycastHit2D hit, Vector2 direction)
        {
            if (IsOwnCollider(hit.collider))
            {
                return false;
            }

            return direction.y < 0f ? hit.normal.y > 0.5f : hit.normal.y < -0.5f;
        }

        private bool IsOwnCollider(Collider2D otherCollider)
        {
            return otherCollider == null || otherCollider.transform == transform;
        }

        private void StartJump()
        {
            if (!isGrounded)
            {
                return;
            }

            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            isGrounded = false;
        }

        // ── Hit response ──────────────────────────────────────────────────────────

        public void ReceiveHit(float damage, Vector2 attackerPosition)
        {
            if (damage <= 0f) return;   // fully mitigated — no damage, no i-frames
            if (IsDead || isInvincible) return;

            // Floating white number at the moment the hit actually lands.
            Combat.DamageNumberSpawner.Show(transform.position + Vector3.up,
                damage, Combat.DamagePopupType.TakenByPlayer);

            // Deal damage and notify UI via PlayerManager
            PlayerManager.Instance?.ModifyHP(-damage);

            // If that was the killing blow, the death visual (HandleDeath) owns the
            // sprite now — don't fire the hit-flash over it.
            if (IsDead) return;

            // Flash: fade from transparent back to opaque
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        // ── Death & respawn response ────────────────────────────────────────────
        // On death this avatar pops up, spins on the Y-axis, and falls through the
        // floor. DeathSequenceController fades the screen and reloads the level; the
        // respawned avatar is a fresh scene object, so HandleRespawn only matters for
        // a death-state cleared *without* a reload (e.g. a direct PlayerManager.Respawn).

        private void HandleDeath()
        {
            deathActive = true;
            hasTarget = false;
            if (hitFlashCoroutine != null) { StopCoroutine(hitFlashCoroutine); hitFlashCoroutine = null; }

            // Pop upward; FixedUpdate's death branch then lets gravity pull the body
            // back down — with ground checks skipped, it falls through the world.
            verticalVelocity = deathPopSpeed;
        }

        private void HandleRespawn()
        {
            deathActive = false;
            verticalVelocity = 0f;
            transform.rotation = Quaternion.identity;
        }

        private void UpdateDeathSpin()
        {
            transform.Rotate(0f, deathSpinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void DeathFallStep(float deltaTime)
        {
            verticalVelocity -= gravity * deltaTime;
            Vector2 position = body.position;
            position.y += verticalVelocity * deltaTime;
            body.MovePosition(position);
        }

        private IEnumerator HitFlashRoutine()
        {
            isInvincible = true;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 0f;
                spriteRenderer.color = c;
            }

            float elapsed = 0f;
            while (elapsed < invincibilityDuration)
            {
                elapsed += Time.deltaTime;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = Mathf.Clamp01(elapsed / invincibilityDuration);
                    spriteRenderer.color = c;
                }
                yield return null;
            }

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            isInvincible = false;
            hitFlashCoroutine = null;
        }

        private void UpdateAnimation()
        {
            if (animator == null)
            {
                return;
            }

            float xVelocity = hasTarget ? 1f : 0f;
            float jumpBlend = verticalVelocity < 0f ? -1f : 0f;
            bool shouldPulseLanding = landedThisFrame || landingPulseTimer > 0f;

            animator.SetFloat("xVelocity", Mathf.Abs(horizontalVelocity) > 0.01f ? xVelocity : 0f);
            animator.SetFloat("Blend", jumpBlend);
            animator.SetBool("isJumping", !isGrounded || isPreparingWallJump);
            animator.SetBool("touchGround", shouldPulseLanding);

            if (landingPulseTimer > 0f)
            {
                landingPulseTimer = Mathf.Max(0f, landingPulseTimer - Time.fixedDeltaTime);
            }
        }
    }
}
