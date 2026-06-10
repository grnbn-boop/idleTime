using UnityEngine;
using TMPro;

namespace IdleTime.Combat
{
    // Which side a number belongs to drives its tint:
    //   DealtToEnemy  → player hit a monster   (yellow)
    //   TakenByPlayer → player got hit          (white)
    //   Crit          → player landed a crit    (red)
    //   Miss          → player's attack whiffed (gray, "MISS")
    public enum DamagePopupType { DealtToEnemy, TakenByPlayer, Crit, Miss }

    // Spawns floating combat numbers in world space. Combat code just calls
    // DamageNumberSpawner.Show(...); the singleton is found in the scene or
    // auto-created on first use. Drop one on a GameObject to tune colors/size
    // in the inspector, or assign a custom popup prefab — otherwise numbers are
    // built procedurally from the project's default TextMeshPro font.
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Optional prefab override")]
        [Tooltip("Instantiated per number; must carry a FloatingDamageText. Leave empty to build numbers procedurally.")]
        [SerializeField] private FloatingDamageText popupPrefab;

        [Header("Colors")]
        [SerializeField] private Color dealtColor = new Color(1f, 0.92f, 0.25f);  // yellow
        [SerializeField] private Color takenColor = Color.white;
        [SerializeField] private Color critColor = new Color(1f, 0.2f, 0.15f);    // red
        [SerializeField] private Color missColor = new Color(0.7f, 0.7f, 0.7f);   // gray

        [Header("Appearance")]
        [Tooltip("Font for procedurally-built numbers. Leave empty to load BoldPixels from Resources.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float fontSize = 4f;

        // Resources path (under any Resources/ folder, no extension) to the font
        // used when none is assigned in the inspector.
        private const string DefaultFontResource = "Fonts/BoldPixels SDF";
        private bool fontResolved;
        [SerializeField] private float critFontScale = 1.4f;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 200;
        [SerializeField] private Vector2 spawnJitter = new Vector2(0.3f, 0.15f);

        private static DamageNumberSpawner instance;

        public static DamageNumberSpawner Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DamageNumberSpawner>();
                    if (instance == null)
                        instance = new GameObject("DamageNumberSpawner").AddComponent<DamageNumberSpawner>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        // Static entry point for combat code.
        public static void Show(Vector3 worldPosition, float amount, DamagePopupType type)
            => Instance.Spawn(worldPosition, amount, type);

        public void Spawn(Vector3 worldPosition, float amount, DamagePopupType type)
        {
            bool crit = type == DamagePopupType.Crit;
            Color color = type switch
            {
                DamagePopupType.Crit => critColor,
                DamagePopupType.Miss => missColor,
                DamagePopupType.TakenByPlayer => takenColor,
                _ => dealtColor,
            };

            string content = type == DamagePopupType.Miss
                ? "MISS"
                : Mathf.RoundToInt(amount).ToString();
            if (crit) content += "!";

            Vector3 spawnPos = worldPosition + new Vector3(
                Random.Range(-spawnJitter.x, spawnJitter.x),
                Random.Range(-spawnJitter.y, spawnJitter.y),
                0f);

            FloatingDamageText popup = popupPrefab != null
                ? Instantiate(popupPrefab, spawnPos, Quaternion.identity)
                : BuildProcedural(spawnPos);

            popup.Play(content, color, crit ? fontSize * critFontScale : fontSize);
        }

        // Builds a bare world-space TextMeshPro object using the project default
        // font, so the system works with zero scene/prefab wiring.
        private FloatingDamageText BuildProcedural(Vector3 position)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = position;

            var tmp = go.AddComponent<TextMeshPro>();
            TMP_FontAsset resolved = ResolveFont();
            if (resolved != null) tmp.font = resolved;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 2f); // avoid auto-culling at a zero-size rect
            tmp.sortingLayerID = SortingLayer.NameToID(sortingLayer);
            tmp.sortingOrder = sortingOrder;

            return go.AddComponent<FloatingDamageText>();
        }

        // Inspector override wins; otherwise load (once) from Resources. A null
        // result is fine — TMP then falls back to the project default font.
        private TMP_FontAsset ResolveFont()
        {
            if (font == null && !fontResolved)
            {
                font = Resources.Load<TMP_FontAsset>(DefaultFontResource);
                fontResolved = true;
                if (font == null)
                    Debug.LogWarning($"[DamageNumberSpawner] Font not found at Resources/{DefaultFontResource}; using TMP default.");
            }
            return font;
        }
    }
}
