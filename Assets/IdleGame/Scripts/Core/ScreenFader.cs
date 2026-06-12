using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleTime.Core
{
    // A full-screen black overlay used for fade-to/from-black transitions. Builds
    // its own Canvas + Image at runtime, so the only setup is dropping this component
    // on a GameObject. DontDestroyOnLoad keeps the overlay (and whatever alpha it's
    // currently at) alive across a scene reload, so the screen can stay black while
    // the level resets underneath it.
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] int sortingOrder = 9999;

        Image overlay;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(transform.root.gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            BuildOverlay();
        }

        void BuildOverlay()
        {
            var canvasGO = new GameObject("FaderCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;   // above all gameplay UI
            canvasGO.AddComponent<CanvasScaler>();

            var imageGO = new GameObject("Black");
            imageGO.transform.SetParent(canvasGO.transform, false);
            overlay = imageGO.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0f);   // start fully transparent
            overlay.raycastTarget = false;               // never eat gameplay clicks

            var rt = overlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Fades the overlay to the given alpha (1 = black, 0 = clear) over duration
        // seconds. Yieldable from a sequencing coroutine.
        public IEnumerator Fade(float targetAlpha, float duration)
        {
            if (overlay == null) yield break;

            Color c = overlay.color;
            float startAlpha = c.a;

            if (duration <= 0f)
            {
                c.a = targetAlpha;
                overlay.color = c;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                overlay.color = c;
                yield return null;
            }

            c.a = targetAlpha;
            overlay.color = c;
        }

        public void SetAlpha(float alpha)
        {
            if (overlay == null) return;
            Color c = overlay.color;
            c.a = Mathf.Clamp01(alpha);
            overlay.color = c;
        }
    }
}
