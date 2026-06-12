using UnityEngine;
using IdleTime.Player;

namespace IdleTime.CameraRig
{
    /// <summary>
    /// Orthographic follow camera with a dead-zone. The camera only scrolls once the
    /// target leaves a rectangular "slack" region around screen centre — so small
    /// hops and back-and-forth nudges don't jitter the view, but walking toward an
    /// edge (or climbing/falling) pushes the camera along. Optionally clamped to a
    /// world rectangle so the level never shows past its edges.
    ///
    /// Runs in LateUpdate: the player moves its kinematic body in FixedUpdate, so the
    /// camera reads the settled position after all movement for the frame is done.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to follow. If empty, the player's ClickToMove2D is found at Start.")]
        [SerializeField] private Transform target;

        [Header("Dead-zone (world units, half-extents from screen centre)")]
        [Tooltip("Horizontal slack: the player can roam this far left/right of centre before the camera scrolls.")]
        [SerializeField] private float horizontalDeadzone = 3f;
        [Tooltip("Vertical slack: the player can roam this far up/down of centre before the camera scrolls.")]
        [SerializeField] private float verticalDeadzone = 2f;

        [Header("Following")]
        [Tooltip("If false the camera Y is locked (classic side-scroller). Horizontal still follows.")]
        [SerializeField] private bool followVertical = true;
        [Tooltip("Approx. seconds for the camera to catch up once the target leaves the dead-zone. 0 = snap.")]
        [SerializeField] private float smoothTime = 0.18f;

        [Header("World Bounds (optional)")]
        [Tooltip("Clamp the camera so its view stays inside the rectangle below.")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 boundsMin = new Vector2(-50f, -10f);
        [SerializeField] private Vector2 boundsMax = new Vector2(50f, 20f);

        private Camera cam;
        private float fixedZ;
        private Vector3 velocity;   // SmoothDamp state

        private void Awake()
        {
            cam = GetComponent<Camera>();
            fixedZ = transform.position.z;
        }

        private void Start()
        {
            if (target == null)
            {
                ClickToMove2D player = FindFirstObjectByType<ClickToMove2D>();
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        /// <summary>Re-point the camera at a new target (e.g. after a level reload spawns a fresh avatar).</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 camPos = transform.position;
            Vector3 targetPos = target.position;

            // Resolve the dead-zone: hold position while the target is inside the box,
            // otherwise pull the nearest edge of the box up to the target.
            float desiredX = ResolveAxis(camPos.x, targetPos.x, horizontalDeadzone);
            float desiredY = followVertical
                ? ResolveAxis(camPos.y, targetPos.y, verticalDeadzone)
                : camPos.y;

            if (useBounds)
            {
                ClampToBounds(ref desiredX, ref desiredY);
            }

            Vector3 desired = new Vector3(desiredX, desiredY, fixedZ);
            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(camPos, desired, ref velocity, smoothTime)
                : desired;
        }

        // Returns the camera-centre value needed to keep `target` within `half` of it.
        private static float ResolveAxis(float center, float target, float half)
        {
            if (target > center + half) return target - half;
            if (target < center - half) return target + half;
            return center;
        }

        private void ClampToBounds(ref float x, ref float y)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // When the level is narrower/shorter than the view, centre on it instead of clamping inside-out.
            if (boundsMax.x - boundsMin.x > halfWidth * 2f)
            {
                x = Mathf.Clamp(x, boundsMin.x + halfWidth, boundsMax.x - halfWidth);
            }
            else
            {
                x = (boundsMin.x + boundsMax.x) * 0.5f;
            }

            if (boundsMax.y - boundsMin.y > halfHeight * 2f)
            {
                y = Mathf.Clamp(y, boundsMin.y + halfHeight, boundsMax.y - halfHeight);
            }
            else
            {
                y = (boundsMin.y + boundsMax.y) * 0.5f;
            }
        }

#if UNITY_EDITOR
        // Yellow = dead-zone, cyan = world bounds. Visible when the camera is selected.
        private void OnDrawGizmosSelected()
        {
            Vector3 c = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(c.x, c.y, 0f),
                new Vector3(horizontalDeadzone * 2f, verticalDeadzone * 2f, 0f));

            if (useBounds)
            {
                Gizmos.color = Color.cyan;
                Vector3 size = new Vector3(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 0f);
                Vector3 mid = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0f);
                Gizmos.DrawWireCube(mid, size);
            }
        }
#endif
    }
}
