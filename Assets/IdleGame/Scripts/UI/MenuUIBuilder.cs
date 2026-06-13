using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace IdleTime.UI
{
    // Shared runtime scaffolding for the blocked-in front-end menus (title screen,
    // character select). These scenes follow the project's self-building-canvas
    // convention (see PortalNavHUD / TooltipManager): the scene holds one controller
    // GameObject plus a pre-wired EventSystem, and the controller assembles its Canvas,
    // background, and widgets in code — so there's no hand-authored UI hierarchy to
    // maintain while the art is blocked in with flat-colour "cubes".
    public static class MenuUIBuilder
    {
        // Project pixel font, loaded from Assets/IdleGame/Resources/Fonts.
        public const string FontResourcePath = "Fonts/BoldPixels SDF";
        static TMP_FontAsset cachedFont;
        static bool fontLookupDone;

        public static readonly Color Accent     = new Color(0.20f, 0.55f, 0.32f);
        public static readonly Color Panel       = new Color(0.16f, 0.20f, 0.26f);

        public static TMP_FontAsset Font
        {
            get
            {
                if (!fontLookupDone)
                {
                    cachedFont = Resources.Load<TMP_FontAsset>(FontResourcePath);
                    fontLookupDone = true;
                }
                return cachedFont;
            }
        }

        // A full-screen Screen-Space-Overlay canvas scaled for 1080p (matches the rest of
        // the game's UI scaler convention). Guarantees a camera so the scene isn't black
        // and an EventSystem so widgets receive input even though the scene body is empty.
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder = 0)
        {
            EnsureCamera();
            EnsureEventSystem();

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureCamera()
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c.isActiveAndEnabled) return;

            var go = new GameObject("Menu Camera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cam.orthographic = true;
            go.tag = "MainCamera";
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            // No EventSystem authored in the scene — make a minimal one. The new Input
            // System module assigns its own default UI actions, which is enough for the
            // menus' clicks and the account-name field.
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ── Widgets ────────────────────────────────────────────────────────────────

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent, string text, float size,
            FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(
            Transform parent, string label, Vector2 size, Color background, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = background;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go.transform, label, size.y * 0.42f, FontStyles.Bold);
            Stretch(txt.rectTransform);
            return btn;
        }

        // A single-line TMP input field, assembled with the viewport / placeholder /
        // text children TMP_InputField expects.
        public static TMP_InputField CreateInputField(Transform parent, string placeholder, Vector2 size)
        {
            var go = new GameObject("InputField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.14f);

            var input = go.AddComponent<TMP_InputField>();

            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var areaRect = (RectTransform)area.transform;
            Stretch(areaRect, 16, 8, 16, 8);
            area.AddComponent<RectMask2D>();

            float fontSize = size.y * 0.42f;
            var placeholderText = CreateText(area.transform, placeholder, fontSize, FontStyles.Italic, TextAlignmentOptions.Left);
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            Stretch(placeholderText.rectTransform);

            var text = CreateText(area.transform, "", fontSize, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 20;
            return input;
        }

        // ── Layout helpers ───────────────────────────────────────────────────────

        // Stretch a RectTransform to fill its parent, optionally inset by the given margins.
        public static RectTransform Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        // Anchor + place a RectTransform at a normalised point in its parent.
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
