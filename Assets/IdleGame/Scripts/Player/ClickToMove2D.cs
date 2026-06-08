using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleTime.Player
{
    public sealed class ClickToMove2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stopDistance = 0.05f;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool flipSpriteToDirection = true;

        private SpriteRenderer spriteRenderer;
        private float targetX;
        private bool hasTarget;

        private Animator animator;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            targetX = transform.position.x;
            animator = GetComponent<Animator>();

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            ReadClickTarget();
            MoveTowardTarget();
        }

        private void ReadClickTarget()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
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
            targetX = worldPosition.x;
            hasTarget = true;

            if (flipSpriteToDirection && spriteRenderer != null)
            {
                spriteRenderer.flipX = targetX < transform.position.x;
            }
        }

        private void MoveTowardTarget()
        {
            if (!hasTarget)
            {
                return;
            }

            Vector3 position = transform.position;
            float nextX = Mathf.MoveTowards(position.x, targetX, moveSpeed * Time.deltaTime);
            transform.position = new Vector3(nextX, position.y, position.z);

            if (animator != null)
            {
                float xVelocity = Mathf.Abs(nextX - position.x) / Time.deltaTime;
                animator.SetFloat("xVelocity", xVelocity);
            }

            if (Mathf.Abs(transform.position.x - targetX) <= stopDistance)
            {
                transform.position = new Vector3(targetX, position.y, position.z);
                hasTarget = false;

                if (animator != null)
                {
                    animator.SetFloat("xVelocity", 0f);
                }
            }
        }
    }
}
