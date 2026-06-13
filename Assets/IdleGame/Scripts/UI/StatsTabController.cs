using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IdleTime.UI
{
    // Inventory-style page toggle for the stats overlay. The overlay background stays put; these
    // buttons only swap which child container is shown inside it — e.g. a "Stats" container
    // (your combat stat texts) and a "Skills" container (the gathering skill rows).

    public class StatsTabController : MonoBehaviour
    {
        [Header("Pages — author each as a child container and wire it here, in order")]
        [SerializeField] private GameObject[] pages;
        [Tooltip("Optional label per page (drives the page-label text). e.g. { \"Stats\", \"Skills\" }.")]
        [SerializeField] private string[] pageNames = { "Stats", "Skills" };

        [Header("Paging controls — wire your own, or leave empty for auto-built ◀ ▶")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text pageLabel;
        [Tooltip("When no buttons are wired above, build a placeholder ◀ [label] ▶ cluster at " +
                 "runtime. Turn this off once you've authored your own.")]
        [SerializeField] private bool buildPlaceholderControls = true;

        private readonly List<GameObject> resolvedPages = new();
        private int page;

        void Awake()
        {
            if (pages != null)
                foreach (var go in pages)
                    if (go != null) resolvedPages.Add(go);

            if (resolvedPages.Count == 0)
            {
                Debug.LogWarning("[StatsTabController] No pages wired — assign your page containers to 'pages'.");
                return;
            }

            SetupPaging();
            ShowPage(0);
        }

        // Prefer prefab-authored buttons; only auto-build when none are wired. A single-page
        // overlay hides any controls entirely.
        private void SetupPaging()
        {
            bool anyWired = prevButton != null || nextButton != null || pageLabel != null;
            if (!anyWired && buildPlaceholderControls && resolvedPages.Count > 1)
                BuildPagingControls();

            if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null) nextButton.onClick.AddListener(NextPage);

            bool show = resolvedPages.Count > 1;
            if (prevButton != null) prevButton.gameObject.SetActive(show);
            if (nextButton != null) nextButton.gameObject.SetActive(show);
            if (pageLabel != null)  pageLabel.gameObject.SetActive(show);
        }

        private void ShowPage(int index)
        {
            page = Mathf.Clamp(index, 0, resolvedPages.Count - 1);
            for (int i = 0; i < resolvedPages.Count; i++)
                resolvedPages[i].SetActive(i == page);
            UpdateLabel();
        }

        public void NextPage() => ShowPage(page + 1 >= resolvedPages.Count ? 0 : page + 1);
        public void PrevPage() => ShowPage(page - 1 < 0 ? resolvedPages.Count - 1 : page - 1);

        private void UpdateLabel()
        {
            if (pageLabel == null) return;
            pageLabel.text = page < pageNames.Length ? pageNames[page] : $"{page + 1}/{resolvedPages.Count}";
        }

        // Fallback only: self-builds a small ◀ [label] ▶ cluster pinned to the top-centre when no
        // buttons are wired.
        private void BuildPagingControls()
        {
            var bar = new GameObject("StatsTabControls", typeof(RectTransform));
            var barRt = (RectTransform)bar.transform;
            barRt.SetParent(transform, false);
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = new Vector2(0f, -8f);
            barRt.sizeDelta = new Vector2(240f, 40f);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            prevButton = BuildArrow(barRt, "PrevTab", "◀");

            var labelGO = new GameObject("TabLabel", typeof(RectTransform));
            labelGO.transform.SetParent(barRt, false);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 140f;
            pageLabel = tmp;

            nextButton = BuildArrow(barRt, "NextTab", "▶");
        }

        private Button BuildArrow(RectTransform parent, string name, string glyph)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
            var btn = go.AddComponent<Button>();
            go.AddComponent<LayoutElement>().preferredWidth = 40f;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGO.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text = glyph;
            t.fontSize = 24f;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return btn;
        }
    }
}
