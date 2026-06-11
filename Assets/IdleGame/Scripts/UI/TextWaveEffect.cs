using UnityEngine;
using TMPro;

namespace IdleTime.UI
{
    // Gives a TextMeshPro label a lazy up-and-down sway, offset per letter so the
    // word ripples like a slow spline rather than bobbing as a block. Works on both
    // world-space (TextMeshPro) and canvas (TextMeshProUGUI) since it targets TMP_Text.
    //
    // Drop it on the same GameObject as the text. Re-applies every LateUpdate after
    // TMP regenerates its mesh, so it survives text content changes (e.g. the portal
    // counter ticking down).
    [RequireComponent(typeof(TMP_Text))]
    public class TextWaveEffect : MonoBehaviour
    {
        [Tooltip("Vertical sway height, in TMP local units.")]
        [SerializeField] private float amplitude = 6f;

        [Tooltip("Sway cycles per second.")]
        [SerializeField] private float frequency = 0.8f;

        [Tooltip("Phase shift between adjacent letters (radians). Higher = more pronounced ripple.")]
        [SerializeField] private float perLetterPhase = 0.5f;

        private TMP_Text tmp;

        private void Awake() => tmp = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (tmp == null) return;

            tmp.ForceMeshUpdate();
            TMP_TextInfo textInfo = tmp.textInfo;
            int charCount = textInfo.characterCount;
            if (charCount == 0) return;

            for (int i = 0; i < charCount; i++)
            {
                TMP_CharacterInfo c = textInfo.characterInfo[i];
                if (!c.isVisible) continue;

                int materialIndex = c.materialReferenceIndex;
                int vertexIndex = c.vertexIndex;
                Vector3[] verts = textInfo.meshInfo[materialIndex].vertices;

                float wave = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + i * perLetterPhase) * amplitude;
                Vector3 offset = new Vector3(0f, wave, 0f);

                verts[vertexIndex + 0] += offset;
                verts[vertexIndex + 1] += offset;
                verts[vertexIndex + 2] += offset;
                verts[vertexIndex + 3] += offset;
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
                meshInfo.mesh.vertices = meshInfo.vertices;
                tmp.UpdateGeometry(meshInfo.mesh, i);
            }
        }
    }
}
