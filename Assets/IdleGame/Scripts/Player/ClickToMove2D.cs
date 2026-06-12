using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using IdleTime.Core;
using IdleTime.Navigation;

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
        [Header("Gap Hop")]
        [Tooltip("Auto-jump a gap when walking toward a target if there's no ground at the edge but solid ground lands within this horizontal distance.")]
        [SerializeField] private float maxHopDistance = 2f;
        [Tooltip("How far ahead of the body to probe for the leading edge of a gap.")]
        [SerializeField] private float gapProbeAhead = 0.55f;
        [Tooltip("How far below the foot a gap probe looks for ground.")]
        [SerializeField] private float gapProbeDepth = 0.35f;
        [SerializeField] private float wallJumpPreparationDuration = 0.12f;
        [SerializeField] private float wallJumpHorizontalDelay = 0.08f;
        [SerializeField] private float groundClickProjectionHeight = 4f;
        [SerializeField] private float landingPulseDuration = 0.08f;
        [SerializeField] private float landingLagDuration = 0.18f;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool flipSpriteToDirection = true;

        [Header("Ladder Climbing")]
        [Tooltip("Tilemap holding ladder tiles. Must be on a layer excluded from terrainMask so the player walks through it. Auto-found by the 'Ladder' layer if left empty.")]
        [SerializeField] private Tilemap ladderTilemap;
        [SerializeField] private float climbSpeed = 3f;
        [Tooltip("Within this X distance of the ladder column the climb begins immediately (climb pose + fast snap onto the column). Beyond it, the player walks to the base first.")]
        [SerializeField] private float climbApproachDistance = 1.5f;
        [Tooltip("How fast the player snaps horizontally onto the ladder column once climbing.")]
        [SerializeField] private float climbSnapSpeed = 14f;
        [Tooltip("How close (in Y) to the climb target counts as arrived.")]
        [SerializeField] private float climbStopDistance = 0.04f;
        [Tooltip("Duration of the climb_top get-on-the-platform one-shot. Match this to the climb_top clip length.")]
        [SerializeField] private float climbTopDuration = 0.35f;
        [Tooltip("How far above the ladder's top tile to start probing for the platform surface the player tops onto. Should comfortably clear the platform (a couple of tiles).")]
        [SerializeField] private float topSurfaceProbe = 2f;
        [Tooltip("Fine-tune the top-out height. Negative seats the player lower (use ~-0.5 = half a tile if they finish floating above the platform). Watch the orange gizmo line — that's where the feet land.")]
        [SerializeField] private float topStandYOffset = 0f;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Animator animator;

        private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
        private readonly RaycastHit2D[] debugHits = new RaycastHit2D[8];
        private ContactFilter2D terrainFilter;

        [Header("Hit Response")]
        [SerializeField] private float invincibilityDuration = 0.8f;

        [Header("Debug")]
        [Tooltip("Log the active animation clip's playback frame + movement state each frame while moving. Toggle at runtime with the key below.")]
        [SerializeField] private bool logWalkFrames;
        [Tooltip("Runtime toggle key for the walk-frame log. (F6–F9 are taken by DebugCommands.)")]
        [SerializeField] private Key logWalkFramesKey = Key.F5;
        [Tooltip("While climbing, draw the ladder probe rays + target heights. Enable Gizmos in the Game view (or watch the Scene view in Play mode) to see them.")]
        [SerializeField] private bool drawClimbGizmos = true;

        [Header("Class Tint")]
        [SerializeField] private Color normieTint = Color.white;
        [SerializeField] private Color fighterTint = Color.red;
        [SerializeField] private Color wizardTint = new Color(0.55f, 0.15f, 1f, 1f);
        [SerializeField] private Color rangerTint = Color.green;

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

        // Ladder state. While `climbing`, gravity is suspended once the player is near
        // the column (within climbApproachDistance); beyond that the normal locomotion
        // walks them to the base first. A climb resolves to one of three exits:
        // Top (mount over the edge onto the platform), Bottom (drop off, gravity
        // resumes), or None (latch at an interior Y and hang).
        private enum ClimbExit { None, Top, Bottom }

        private bool climbing;
        private bool climbNear;            // within approach distance this frame → climb pose + gravity off
        private float ladderColumnX;
        private float ladderTopEdgeY;      // world Y of the top edge of the topmost ladder tile
        private float ladderBottomEdgeY;   // world Y of the bottom edge of the bottommost ladder tile
        private float climbTargetY;        // current vertical goal (body-center Y)
        private ClimbExit climbExit;
        private float climbVerticalSign;   // +1 up, -1 down, 0 latched — drives the climb_loop speed param

        private bool climbAnimActive;      // climb states are currently driving the animator
        private bool mounting;             // playing the climb_top lift-over-the-edge
        private float mountTimer;
        private float mountFromY;
        private float mountToY;

        private bool hasPostClimbWalk;     // after dismounting, walk to where the player clicked
        private float postClimbWalkX;

        // Feet sit this far below the body center (ground sensor offset + half its height).
        private float FeetToCenter => -groundSensorOffset.y + groundSensorSize.y * 0.5f;

        // Multi-leg navigation (walk/hop/climb across the level) — see TileNavGraph.
        private TileNavGraph navGraph;
        private readonly List<NavStep> navPath = new List<NavStep>();
        private int navIndex;
        private bool navActive;

        // Walk-frame debug (see LateUpdate).
        private float debugPrevNormalizedTime;
        private float debugPrevPosX;

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
            // Movement happens in FixedUpdate via MovePosition (50 Hz). Without
            // interpolation the rendered position only updates on physics steps, so at
            // higher framerates the body visibly stutters between steps even though the
            // animation plays smoothly. Interpolate smooths the in-between frames.
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

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

            if (ladderTilemap == null)
            {
                ladderTilemap = FindLadderTilemap();
            }

            navGraph = FindFirstObjectByType<TileNavGraph>();
        }

        private void Start()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerDeath += HandleDeath;
                PlayerManager.Instance.OnPlayerRespawn += HandleRespawn;
                PlayerManager.Instance.OnActiveCharacterChanged += ApplyClassTint;
                PlayerManager.Instance.OnClassChanged += ApplyClassTint;
            }

            ApplyClassTint();
        }

        private void OnDestroy()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.OnPlayerDeath -= HandleDeath;
                PlayerManager.Instance.OnPlayerRespawn -= HandleRespawn;
                PlayerManager.Instance.OnActiveCharacterChanged -= ApplyClassTint;
                PlayerManager.Instance.OnClassChanged -= ApplyClassTint;
            }
        }

        private bool IsDead => PlayerManager.Instance != null && PlayerManager.Instance.IsDead;

        private void Update()
        {
            if (deathActive) { UpdateDeathSpin(); return; }   // cinematic owns the avatar
            if (IsDead) return;                                // dead but not animating (e.g. fresh avatar pre-respawn): no input
            ReadClickTarget();

            if (climbing && drawClimbGizmos) DrawClimbDebug();
        }

        // Visualises the ladder math while climbing so the top-surface probe is debuggable.
        //   yellow  — ladder top/bottom edges (the tiles)
        //   cyan    — the top-surface down-probe ray
        //   green   — the surface the probe hit (where the player should top out)
        //   magenta — climbTargetY (body-center the climb is steering toward)
        //   orange  — the feet height that target implies
        //   red     — the player's current feet
        private void DrawClimbDebug()
        {
            float x = ladderColumnX;

            Debug.DrawLine(new Vector3(x - 0.5f, ladderTopEdgeY, 0f), new Vector3(x + 0.5f, ladderTopEdgeY, 0f), Color.yellow);
            Debug.DrawLine(new Vector3(x - 0.5f, ladderBottomEdgeY, 0f), new Vector3(x + 0.5f, ladderBottomEdgeY, 0f), Color.yellow);

            Vector2 origin = new Vector2(x, ladderTopEdgeY + topSurfaceProbe);
            float dist = topSurfaceProbe + 0.3f;
            Debug.DrawLine(origin, origin + Vector2.down * dist, Color.cyan);

            int hitCount = Physics2D.Raycast(origin, Vector2.down, terrainFilter, debugHits, dist);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsOwnCollider(debugHits[i].collider) || debugHits[i].normal.y <= 0.5f) continue;
                Vector2 p = debugHits[i].point;
                Debug.DrawLine(new Vector3(p.x - 0.3f, p.y, 0f), new Vector3(p.x + 0.3f, p.y, 0f), Color.green);
                Debug.DrawLine(new Vector3(p.x, p.y - 0.3f, 0f), new Vector3(p.x, p.y + 0.3f, 0f), Color.green);
                break;
            }

            Debug.DrawLine(new Vector3(x - 0.6f, climbTargetY, 0f), new Vector3(x + 0.6f, climbTargetY, 0f), Color.magenta);
            float targetFeetY = climbTargetY - FeetToCenter;
            Debug.DrawLine(new Vector3(x - 0.6f, targetFeetY, 0f), new Vector3(x + 0.6f, targetFeetY, 0f), new Color(1f, 0.55f, 0f));

            float feetY = body.position.y + groundSensorOffset.y - groundSensorSize.y * 0.5f;
            Debug.DrawLine(new Vector3(body.position.x - 0.3f, feetY, 0f), new Vector3(body.position.x + 0.3f, feetY, 0f), Color.red);
        }

        // Reads the animator AFTER it has evaluated this frame, so the logged frame is
        // what's actually on screen. Distinguishes a position wiggle (clean monotonic
        // normalizedTime, jumpy dX) from a real animation skip/replay (normalizedTime
        // jumping backward — flagged REPLAY).
        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current[logWalkFramesKey].wasPressedThisFrame)
            {
                logWalkFrames = !logWalkFrames;
                Debug.Log($"[WalkDbg] logging {(logWalkFrames ? "ON" : "OFF")}");
            }

            if (!logWalkFrames || animator == null) return;

            bool moving = hasTarget || navActive || Mathf.Abs(horizontalVelocity) > 0.01f;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length > 0 ? clips[0].clip.name : "<none>";
            float fps = clips.Length > 0 ? clips[0].clip.frameRate : 0f;
            float length = clips.Length > 0 ? clips[0].clip.length : 0f;

            float normalizedTime = state.normalizedTime;
            float loopT = normalizedTime - Mathf.Floor(normalizedTime);
            int totalFrames = (fps > 0f && length > 0f) ? Mathf.RoundToInt(length * fps) : 0;
            int frame = totalFrames > 0 ? Mathf.FloorToInt(loopT * totalFrames) : -1;

            float dNt = normalizedTime - debugPrevNormalizedTime;
            bool looped = dNt < -0.5f;                 // wrapped past the end of a loop — expected
            bool replay = dNt < -0.0001f && !looped;   // went backward without looping — a real skip/replay
            float dX = body.position.x - debugPrevPosX;

            if (moving || replay)
            {
                Debug.Log($"[WalkDbg] clip={clipName} frame={frame}/{totalFrames} nt={normalizedTime:F3} dNt={dNt:F4}" +
                          $"{(replay ? "  <<REPLAY/SKIP-BACK>>" : looped ? "  (loop)" : "")}" +
                          $" | xVel={horizontalVelocity:F2} hasTarget={hasTarget} nav={navActive} climb={climbing} dX={dX:F4}");
            }

            debugPrevNormalizedTime = normalizedTime;
            debugPrevPosX = body.position.x;
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

            if (climbing)
            {
                StepClimb(ref position, Time.fixedDeltaTime);
                body.MovePosition(position);
                UpdateAnimation();
                return;
            }

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

            // Walk/Hop leg of a route finished (reached x and on the ground) → next leg.
            if (navActive && !hasTarget && isGrounded)
            {
                AdvanceNav();
            }
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

        // Route the player to a world point using the nav graph (walk/hop/climb across
        // the level). For the portal-preview "click to traverse a level" flow. Returns
        // true if a multi-leg route was found; otherwise falls back to a same-level walk.
        public bool NavigateTo(Vector2 worldTarget)
        {
            navActive = false;
            if (climbing) EndClimb();

            if (navGraph != null &&
                navGraph.TryFindPath((Vector2)body.position + groundSensorOffset, worldTarget, navPath) &&
                navPath.Count > 0)
            {
                navActive = true;
                navIndex = 0;
                BeginNavStep();
                return true;
            }

            targetX = worldTarget.x;
            hasTarget = true;
            return false;
        }

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

            // A fresh click cancels any in-progress route.
            navActive = false;

            // Clicking the ladder itself climbs toward that exact spot.
            if (IsLadderAt(worldPosition))
            {
                BeginOrRetargetClimb(worldPosition);
                return;
            }

            // Clicking the ground while on the ladder: drive up or down depending on
            // whether the destination is above or below, then walk there on dismount.
            if (climbing && TryGetClickedGround(worldPosition, out Vector2 climbGround))
            {
                bool goUp = climbGround.y > body.position.y;
                RetargetClimbToEnd(goUp ? ClimbExit.Top : ClimbExit.Bottom, climbGround.x);
                return;
            }

            if (!TryGetClickedGround(worldPosition, out Vector2 groundPoint))
            {
                return;
            }

            // Try a full route (walk/hop/climb across the level). Falls back to a plain
            // same-level walk if there's no nav graph or no path to the target.
            if (!climbing && navGraph != null &&
                navGraph.TryFindPath((Vector2)body.position + groundSensorOffset, groundPoint, navPath) &&
                navPath.Count > 0)
            {
                navActive = true;
                navIndex = 0;
                BeginNavStep();
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

            // Auto-hop a small gap: at a ledge with no ground underfoot ahead but solid
            // ground within hop range, launch a jump and keep the horizontal carry going
            // (the airborne branch above lets horizontal continue while rising).
            if (ShouldHopGap(position, direction))
            {
                StartJump();
            }

            // DEX scales movement speed (StatFormulas.MoveSpeedMultiplier); ×1 if no active character.
            float speed = moveSpeed * (PlayerManager.Instance?.ActiveCharacter?.MoveSpeedMultiplier ?? 1f);
            float moveDistance = Mathf.Min(Mathf.Abs(remainingDistance), speed * deltaTime);
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

        // ── Ladder climbing ─────────────────────────────────────────────────────

        private bool IsLadderAt(Vector2 worldPosition)
        {
            return ladderTilemap != null && ladderTilemap.HasTile(ladderTilemap.WorldToCell(worldPosition));
        }

        // Click landed on a ladder tile: resolve the column extents, decide the exit,
        // and enter climb mode. The climb pose shows immediately (see climbNear).
        private void BeginOrRetargetClimb(Vector2 worldPosition)
        {
            Vector3Int cell = ladderTilemap.WorldToCell(worldPosition);
            ResolveLadderColumn(cell);

            bool clickedTopCell = !ladderTilemap.HasTile(cell + Vector3Int.up);
            bool clickedBottomCell = !ladderTilemap.HasTile(cell + Vector3Int.down);

            hasPostClimbWalk = false;

            if (clickedTopCell)
            {
                SetClimbExit(ClimbExit.Top);
            }
            else if (clickedBottomCell)
            {
                SetClimbExit(ClimbExit.Bottom);
            }
            else
            {
                // Interior click: stop at that exact spot and hang.
                climbExit = ClimbExit.None;
                climbTargetY = worldPosition.y;
            }

            climbing = true;
            mounting = false;
            hasTarget = false;   // climb logic owns movement now

            // Face the ladder so the walk-to-base doesn't moonwalk when clicked from behind.
            FaceDirection(ladderColumnX);
        }

        // Retarget an in-progress climb toward an end because the player clicked ground
        // above (Top) or below (Bottom). Walk to the click x after dismounting.
        private void RetargetClimbToEnd(ClimbExit exit, float walkX)
        {
            SetClimbExit(exit);
            hasPostClimbWalk = true;
            postClimbWalkX = walkX;
            mounting = false;
        }

        private void SetClimbExit(ClimbExit exit)
        {
            climbExit = exit;
            // Top: climb the loop all the way to the ladder top, then climb_top plays
            // as the "get on" flourish (only at the top). Bottom: stop with the feet on
            // the floor at the base so the player stands on the ground, not sunk into it.
            climbTargetY = exit == ClimbExit.Top
                ? TopStandY()
                : BottomStandY();
        }

        // Walks up/down from the clicked cell to find the ladder's full vertical span,
        // caching the column x and the top/bottom edge world-Y.
        private void ResolveLadderColumn(Vector3Int cell)
        {
            ladderColumnX = ladderTilemap.GetCellCenterWorld(cell).x;

            Vector3Int top = cell;
            while (ladderTilemap.HasTile(top + Vector3Int.up)) top += Vector3Int.up;
            Vector3Int bottom = cell;
            while (ladderTilemap.HasTile(bottom + Vector3Int.down)) bottom += Vector3Int.down;

            Vector3 cellSize = ladderTilemap.layoutGrid != null ? ladderTilemap.layoutGrid.cellSize : Vector3.one;
            ladderTopEdgeY = ladderTilemap.GetCellCenterWorld(top).y + cellSize.y * 0.5f;
            ladderBottomEdgeY = ladderTilemap.GetCellCenterWorld(bottom).y - cellSize.y * 0.5f;
        }

        // Body-center Y for standing on the platform the player tops onto. Cast DOWN
        // from ABOVE the ladder's top tile so we land on the platform's surface — the old
        // cast started below it and seated the player a tile low. Results come back
        // nearest-first, so the first upward-facing hit is the surface. Fall back to the
        // ladder's top edge if there's no platform directly above the column.
        private float TopStandY()
        {
            Vector2 origin = new Vector2(ladderColumnX, ladderTopEdgeY + topSurfaceProbe);
            int hitCount = Physics2D.Raycast(origin, Vector2.down, terrainFilter, castHits, topSurfaceProbe + 0.3f);
            for (int i = 0; i < hitCount; i++)
            {
                if (!IsOwnCollider(castHits[i].collider) && castHits[i].normal.y > 0.5f)
                {
                    return castHits[i].point.y + FeetToCenter + topStandYOffset;
                }
            }
            return ladderTopEdgeY + FeetToCenter + topStandYOffset;
        }

        // Body-center Y at which the player stands with feet on the floor at the BOTTOM
        // of the ladder. Same idea as TopStandY: find the real surface under the ladder
        // base so a descent ends standing on the ground rather than clipping into it.
        private float BottomStandY()
        {
            float surfaceY = ladderBottomEdgeY;
            Vector2 origin = new Vector2(ladderColumnX, ladderBottomEdgeY + 0.5f);
            int hitCount = Physics2D.Raycast(origin, Vector2.down, terrainFilter, castHits, 1.5f);
            for (int i = 0; i < hitCount; i++)
            {
                if (!IsOwnCollider(castHits[i].collider) && castHits[i].normal.y > 0.5f)
                {
                    surfaceY = castHits[i].point.y;
                    break;
                }
            }
            return surfaceY + FeetToCenter;
        }

        // Drives the player while in climb mode. Far from the column we reuse the normal
        // walk + gravity to reach the base; near it we suspend gravity, snap onto the
        // column, and track climbTargetY vertically. The mount phase is handled first.
        private void StepClimb(ref Vector2 position, float deltaTime)
        {
            if (mounting)
            {
                StepMount(ref position, deltaTime);
                return;
            }

            climbNear = Mathf.Abs(position.x - ladderColumnX) <= climbApproachDistance;

            if (!climbNear)
            {
                // Still approaching the ladder on foot — normal locomotion + gravity.
                isGrounded = IsTouchingGround(position);
                if (isGrounded && verticalVelocity < 0f)
                {
                    verticalVelocity = 0f;
                }

                targetX = ladderColumnX;
                hasTarget = true;
                position.x += GetHorizontalMove(position, deltaTime);
                position.y += GetVerticalMove(ref position, deltaTime);
                return;
            }

            // On the ladder: gravity off, slide onto the column, climb toward the goal.
            verticalVelocity = 0f;
            horizontalVelocity = 0f;
            position.x = Mathf.MoveTowards(position.x, ladderColumnX, climbSnapSpeed * deltaTime);

            float remaining = climbTargetY - position.y;
            if (Mathf.Abs(remaining) <= climbStopDistance)
            {
                climbVerticalSign = 0f;
                OnClimbReachedTarget(ref position);
                return;
            }

            float direction = Mathf.Sign(remaining);
            position.y += direction * Mathf.Min(Mathf.Abs(remaining), climbSpeed * deltaTime);
            climbVerticalSign = direction;
        }

        private void OnClimbReachedTarget(ref Vector2 position)
        {
            switch (climbExit)
            {
                case ClimbExit.Top:
                    // Reached the ladder top: play climb_top while lifting the player from
                    // the offset (animation-aligned) height UP onto the terrain by exactly
                    // topStandYOffset, so they finish standing on top instead of sunk in.
                    mounting = true;
                    mountTimer = 0f;
                    mountFromY = position.y;                       // offset height — climb_top lines up here
                    mountToY = TopStandY() - topStandYOffset;      // un-offset surface — feet on top of the terrain
                    position.x = ladderColumnX;
                    if (animator != null)
                    {
                        animator.speed = 1f;
                        animator.Play("climb_top", 0, 0f);   // force a clean restart from frame 0
                    }
                    break;

                case ClimbExit.Bottom:
                    EndClimb();   // gravity resumes and seats the feet on the floor
                    break;

                // None: latch and hang. climbVerticalSign == 0 freezes climb_loop.
            }
        }

        private void StepMount(ref Vector2 position, float deltaTime)
        {
            mountTimer += deltaTime;
            float t = climbTopDuration > 0f ? Mathf.Clamp01(mountTimer / climbTopDuration) : 1f;
            position.x = ladderColumnX;
            position.y = Mathf.Lerp(mountFromY, mountToY, t);
            climbVerticalSign = 0f;

            if (t >= 1f)
            {
                position.y = mountToY;
                EndClimb();
            }
        }

        private void EndClimb()
        {
            climbing = false;
            climbNear = false;
            mounting = false;
            climbVerticalSign = 0f;
            verticalVelocity = 0f;

            // Mid-route: the climb leg is done, move on to the next leg.
            if (navActive)
            {
                AdvanceNav();
                return;
            }

            // Walk to where the player clicked off the ladder, if anywhere.
            if (hasPostClimbWalk)
            {
                hasPostClimbWalk = false;
                targetX = postClimbWalkX;
                hasTarget = true;
            }
            else
            {
                hasTarget = false;
            }
        }

        // ── Route execution (TileNavGraph paths) ─────────────────────────────────

        private void BeginNavStep()
        {
            NavStep step = navPath[navIndex];
            switch (step.Type)
            {
                case NavMoveType.Walk:
                case NavMoveType.Hop:
                    targetX = step.World.x;
                    hasTarget = true;
                    if (flipSpriteToDirection && spriteRenderer != null)
                    {
                        spriteRenderer.flipX = targetX < transform.position.x;
                    }
                    break;

                case NavMoveType.ClimbUp:
                case NavMoveType.ClimbDown:
                    // step.World is a ladder cell at the end we're heading for, so
                    // BeginOrRetargetClimb derives the right Top/Bottom exit from it.
                    hasPostClimbWalk = false;
                    BeginOrRetargetClimb(step.World);
                    break;
            }
        }

        private void AdvanceNav()
        {
            navIndex++;
            if (navIndex >= navPath.Count)
            {
                navActive = false;
                return;
            }
            BeginNavStep();
        }

        private Tilemap FindLadderTilemap()
        {
            int ladderLayer = LayerMask.NameToLayer("Ladder");
            if (ladderLayer < 0)
            {
                return null;
            }

            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].gameObject.layer == ladderLayer)
                {
                    return tilemaps[i];
                }
            }

            return null;
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

        // True when the player stands at a ledge (no ground just ahead underfoot) but
        // solid ground lands within maxHopDistance — a gap worth auto-hopping.
        private bool ShouldHopGap(Vector2 position, float direction)
        {
            if (!isGrounded)
            {
                return false;
            }

            float footY = position.y + groundSensorOffset.y;

            // Ground immediately ahead → not a gap, just keep walking.
            if (HasGroundAt(new Vector2(position.x + direction * gapProbeAhead, footY), gapProbeDepth))
            {
                return false;
            }

            // Scan outward for a landing within hop range.
            for (float d = gapProbeAhead + 0.25f; d <= maxHopDistance; d += 0.25f)
            {
                if (HasGroundAt(new Vector2(position.x + direction * d, footY), gapProbeDepth))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasGroundAt(Vector2 origin, float depth)
        {
            int hitCount = Physics2D.Raycast(origin, Vector2.down, terrainFilter, castHits, depth);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsGroundHit(castHits[i]))
                {
                    return true;
                }
            }

            return false;
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
            navActive = false;
            climbing = false;
            climbAnimActive = false;
            if (animator != null) animator.speed = 1f;   // climbs freeze speed; don't leak it into death
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

        private void ApplyClassTint()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Color tint = GetClassTint(PlayerManager.Instance?.ActiveCharacter?.playerClass);
            tint.a = spriteRenderer.color.a;
            spriteRenderer.color = tint;
        }

        private Color GetClassTint(PlayerClass playerClass)
        {
            string className = playerClass != null ? playerClass.className : string.Empty;
            switch (className.Trim().ToLowerInvariant())
            {
                case "fighter":
                    return fighterTint;
                case "wizard":
                    return wizardTint;
                case "ranger":
                    return rangerTint;
                default:
                    return normieTint;
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null)
            {
                return;
            }

            // On the ladder (near the column, or mid-mount) the climb states own the
            // avatar — driven directly here, not via transitions, so it can't be delayed
            // by exit-time or fire climb_top mid-ladder.
            bool climbActive = climbing && (climbNear || mounting);
            if (climbActive)
            {
                // Entering the climb pose: kick the loop immediately (fixes the late start).
                if (!climbAnimActive)
                {
                    animator.CrossFadeInFixedTime("climb_loop", 0.05f, 0);
                    climbAnimActive = true;
                }

                // Freeze on the current frame while latched/idle; play while moving up/down
                // or during the top mount. (animator.speed is global, but nothing else on
                // this avatar animates during a climb.)
                bool frozen = !mounting && Mathf.Abs(climbVerticalSign) < 0.01f;
                animator.speed = frozen ? 0f : 1f;

                // Keep the locomotion params quiet so Any State→jumping can't grab us, and
                // pin isClimbing true so any leftover climb transitions stay dormant.
                animator.SetBool("isClimbing", true);
                animator.SetBool("isJumping", false);
                animator.SetBool("touchGround", false);
                animator.SetFloat("xVelocity", 0f);
                return;
            }

            // Just left the ladder: restore normal speed and blend back to the locomotion
            // tree (so we don't hold the last climb_top frame).
            if (climbAnimActive)
            {
                climbAnimActive = false;
                animator.speed = 1f;
                animator.SetBool("isClimbing", false);
                animator.CrossFadeInFixedTime("Movement", 0.08f, 0);
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
