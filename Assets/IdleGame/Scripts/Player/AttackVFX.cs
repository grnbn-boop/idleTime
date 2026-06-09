using System.Collections;
using UnityEngine;

namespace IdleTime.Player
{
    public class AttackVFX : MonoBehaviour
    {
        [SerializeField] private AnimationClip attackClip;
        [SerializeField] private string vfxSortingLayer = "Default";
        [SerializeField] private int vfxSortingOrder = 100;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Coroutine hideRoutine;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = vfxSortingLayer;
                spriteRenderer.sortingOrder = vfxSortingOrder;
                spriteRenderer.enabled = false;
            }

            if (animator != null) animator.enabled = false;
        }

        // facingLeft is passed in explicitly by the caller so facing is always correct
        // regardless of how the parent hierarchy is structured.
        public void Play(Vector2 offset, bool facingLeft)
        {
            transform.localPosition = new Vector3(
                facingLeft ? -offset.x : offset.x,
                offset.y,
                0f
            );

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !facingLeft;
                spriteRenderer.enabled = true;
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.Play("basic_attack", 0, 0f);
            }

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterClip());
        }

        private IEnumerator HideAfterClip()
        {
            float duration = attackClip != null ? attackClip.length : 0.35f;
            yield return new WaitForSeconds(duration);
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (animator != null) animator.enabled = false;
            hideRoutine = null;
        }
    }
}
