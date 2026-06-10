using UnityEngine;
using TMPro;

namespace IdleTime.Combat
{
    // A single floating combat number: pops in, rises, fades, and destroys
    // itself. Lives on a world-space object carrying a TextMeshPro renderer.
    // Spawned and configured by DamageNumberSpawner.
    [DisallowMultipleComponent]
    public class FloatingDamageText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;

        [Header("Motion")]
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float riseSpeed = 1.6f;
        [SerializeField] private float driftX = 0.4f;     // random horizontal drift so stacked hits fan out

        [Header("Pop")]
        [SerializeField] private float popScale = 1.25f;  // brief overshoot on spawn
        [SerializeField] private float popInTime = 0.12f;

        private float elapsed;
        private float horizontalDrift;

        private void Awake()
        {
            if (text == null) text = GetComponentInChildren<TextMeshPro>();
        }

        public void Play(string content, Color color, float fontSize)
        {
            if (text == null) text = GetComponentInChildren<TextMeshPro>();
            if (text != null)
            {
                text.text = content;
                text.color = color;
                text.fontSize = fontSize;
                // The first TMP created at runtime defers mesh generation, so the
                // very first popup would otherwise render blank — force it now.
                text.ForceMeshUpdate();
            }

            elapsed = 0f;
            horizontalDrift = Random.Range(-driftX, driftX);
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // Rise with a touch of horizontal drift.
            transform.position += new Vector3(horizontalDrift * Time.deltaTime,
                                              riseSpeed * Time.deltaTime, 0f);

            // Pop in, then settle back to 1x.
            float scale = elapsed < popInTime
                ? Mathf.Lerp(0f, popScale, elapsed / popInTime)
                : Mathf.Lerp(popScale, 1f, Mathf.Clamp01((elapsed - popInTime) / (lifetime * 0.4f)));
            transform.localScale = Vector3.one * scale;

            // Fade out over the back half of the lifetime.
            if (text != null)
            {
                Color c = text.color;
                c.a = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);
                text.color = c;
            }

            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
