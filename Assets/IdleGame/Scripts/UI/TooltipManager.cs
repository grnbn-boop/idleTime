using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleTime.UI
{
    // A single floating tooltip panel shown next to the cursor. Builds its own
    // Canvas/panel/label at runtime and follows the mouse while visible. Generic on
    // purpose: anything can show a tooltip via a TooltipTrigger, so this is reused
    // across the UI, not tied to stats. DontDestroyOnLoad so it survives scene reloads.
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [SerializeField] int sortingOrder = 10000;     // above gameplay UI and the fader
        [SerializeField] Vector2 cursorOffset = new Vector2(16f, -16f);
        [Tooltip("Seconds the cursor must rest on an element before the tooltip appears.")]
        [SerializeField] float showDelay = 0.5f;
        [Tooltip("Grace period after leaving an element before the tooltip closes — lets you move to an adjacent item and swap instantly instead of flickering.")]
        [SerializeField] float hideGrace = 0.1f;

        [Header("Appearance (applied at startup)")]
        [SerializeField] float fontSize = 22f;
        [SerializeField] Color textColor = Color.white;
        [SerializeField] Color backgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        [Tooltip("Inner padding around the text, in pixels.")]
        [SerializeField] int paddingHorizontal = 10;
        [SerializeField] int paddingVertical = 8;
        [Tooltip("Optional font override; leave empty to use the TMP default.")]
        [SerializeField] TMP_FontAsset fontAsset;
        [Tooltip("Wrap text past this width (px). 0 = never wrap (panel grows to fit).")]
        [SerializeField] float maxWidth = 0f;

        RectTransform panel;
        TextMeshProUGUI label;
        bool visible;
        Coroutine pendingShow;
        Coroutine pendingHide;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(transform.root.gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            Build();
            Hide();
        }

        void Build()
        {
            var canvasGO = new GameObject("TooltipCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var bg = panelGO.AddComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;            // tooltip never eats pointer events
            panel = panelGO.GetComponent<RectTransform>();
            panel.pivot = new Vector2(0f, 1f);   // top-left pinned to the cursor

            var layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(paddingHorizontal, paddingHorizontal, paddingVertical, paddingVertical);
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(panelGO.transform, false);
            label = textGO.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.color = textColor;
            if (fontAsset != null) label.font = fontAsset;
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = maxWidth > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;   // off → panel grows to fit one line per \n
            if (maxWidth > 0f)
                textGO.AddComponent<LayoutElement>().preferredWidth = maxWidth;
        }

        public void Show(string content)
        {
            if (panel == null || string.IsNullOrEmpty(content)) return;

            CancelPendingHide();   // leaving then re-entering within the grace cancels the close

            // Already up (e.g. moved straight to an adjacent item): swap content now,
            // no second delay.
            if (visible)
            {
                if (pendingShow != null) { StopCoroutine(pendingShow); pendingShow = null; }
                Display(content);
                return;
            }

            if (pendingShow != null) StopCoroutine(pendingShow);
            pendingShow = StartCoroutine(ShowAfterDelay(content));
        }

        IEnumerator ShowAfterDelay(string content)
        {
            // Realtime so it still counts down if the game is paused (timeScale 0).
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);
            pendingShow = null;
            Display(content);
        }

        void Display(string content)
        {
            label.text = content;
            panel.gameObject.SetActive(true);
            visible = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);   // size now so first-frame clamp is correct
            UpdatePosition();
        }

        public void Hide()
        {
            // A not-yet-shown tooltip is dropped outright.
            if (pendingShow != null) { StopCoroutine(pendingShow); pendingShow = null; }
            if (!visible) { HideImmediate(); return; }

            // Visible: defer the close so a Show() from the next element can cancel it
            // and swap in place (visible stays true during the grace).
            CancelPendingHide();
            pendingHide = StartCoroutine(HideAfterGrace());
        }

        IEnumerator HideAfterGrace()
        {
            if (hideGrace > 0f) yield return new WaitForSecondsRealtime(hideGrace);
            pendingHide = null;
            HideImmediate();
        }

        void HideImmediate()
        {
            visible = false;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void CancelPendingHide()
        {
            if (pendingHide != null) { StopCoroutine(pendingHide); pendingHide = null; }
        }

        void Update()
        {
            if (visible) UpdatePosition();
        }

        void UpdatePosition()
        {
            if (Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();

            float w = panel.rect.width;
            float h = panel.rect.height;
            float x = Mathf.Clamp(mouse.x + cursorOffset.x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(mouse.y + cursorOffset.y, h, Screen.height);   // pivot is top-left
            panel.position = new Vector3(x, y, 0f);
        }
    }
}
