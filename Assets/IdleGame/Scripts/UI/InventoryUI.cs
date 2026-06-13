using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] InventorySlotUI[] slots;

        [Header("Paging — author these in the prefab and wire them here")]
        [Tooltip("Previous-page button. Assign your own button from the inventory prefab. " +
                 "Leave all three paging fields empty to fall back to auto-built placeholder controls.")]
        [SerializeField] Button prevButton;
        [Tooltip("Next-page button. Assign your own button from the inventory prefab.")]
        [SerializeField] Button nextButton;
        [Tooltip("Optional page indicator label, e.g. shows \"1/2\".")]
        [SerializeField] TMP_Text pageLabel;
        [Tooltip("When no buttons are wired above, build placeholder ◀ ▶ controls at runtime. " +
                 "Turn this off once you've authored your own in the prefab.")]
        [SerializeField] bool buildPlaceholderControls = true;

        // One page = however many slot views the prefab wires (16). The backing
        // Inventory.MaxSlots (32) spans PageCount pages.
        int PageSize => slots != null ? slots.Length : 0;
        int PageCount => PageSize > 0 ? Mathf.CeilToInt((float)Inventory.MaxSlots / PageSize) : 1;

        // Static so the current page survives closing/reopening the overlay and room changes
        // within a session (a fresh InventoryUI instance picks up where the last left off).
        static int _page;

        void Awake()
        {
            Debug.Log($"[InventoryUI] Awake — {slots.Length} slot views, {PageCount} page(s)");
            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, PageCount - 1));   // guard if page count shrank
            ApplyPageIndices();
            SetupPaging();
        }

        // Prefer prefab-authored buttons; only auto-build when none are wired. A single-page
        // inventory hides any controls entirely.
        void SetupPaging()
        {
            bool anyWired = prevButton != null || nextButton != null || pageLabel != null;
            if (!anyWired && buildPlaceholderControls && PageCount > 1)
                BuildPagingControls();

            if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null) nextButton.onClick.AddListener(NextPage);

            bool show = PageCount > 1;
            if (prevButton != null) prevButton.gameObject.SetActive(show);
            if (nextButton != null) nextButton.gameObject.SetActive(show);
            if (pageLabel != null)  pageLabel.gameObject.SetActive(show);

            UpdatePageLabel();
        }

        void OnEnable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.OnInventoryChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.OnInventoryChanged -= Refresh;
        }

        // Map each slot view to its backing inventory index for the current page.
        // Re-run on every page flip so a view's drag/equip/discard hits the right slot.
        void ApplyPageIndices()
        {
            int offset = _page * PageSize;
            for (int i = 0; i < slots.Length; i++)
                slots[i].Init(offset + i);
        }

        void Refresh()
        {
            if (Inventory.Instance == null) { Debug.LogWarning("[InventoryUI] Refresh skipped — Inventory.Instance is null"); return; }

            int offset = _page * PageSize;
            for (int i = 0; i < slots.Length; i++)
            {
                int backing = offset + i;
                slots[i].Refresh(backing < Inventory.MaxSlots ? Inventory.Instance.GetSlot(backing) : null);
            }
            UpdatePageLabel();
        }

        // ── Paging ────────────────────────────────────────────────────────────────

        public void NextPage() => GoToPage(_page + 1);
        public void PrevPage() => GoToPage(_page - 1);

        void GoToPage(int page)
        {
            page = Mathf.Clamp(page, 0, PageCount - 1);
            if (page == _page) return;
            _page = page;
            TooltipManager.Instance?.Hide();   // hovered slot now shows a different item
            ApplyPageIndices();
            Refresh();
        }

        void UpdatePageLabel()
        {
            if (pageLabel != null) pageLabel.text = $"{_page + 1}/{PageCount}";
        }

        // Fallback only: self-builds a small ◀ [1/2] ▶ cluster pinned to the top-right corner
        // when no buttons are wired in the prefab. Assigns into the serialized fields so the
        // rest of the class treats authored and auto-built controls identically.
        void BuildPagingControls()
        {
            var bar = new GameObject("PagingControls", typeof(RectTransform));
            var barRt = (RectTransform)bar.transform;
            barRt.SetParent(transform, false);
            barRt.anchorMin = barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(1f, 1f);
            barRt.anchoredPosition = new Vector2(-6f, -6f);
            barRt.sizeDelta = new Vector2(170f, 44f);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            prevButton = BuildArrow(barRt, "PrevPage", "◀");

            var labelGO = new GameObject("PageLabel", typeof(RectTransform));
            labelGO.transform.SetParent(barRt, false);
            pageLabel = labelGO.AddComponent<TextMeshProUGUI>();
            pageLabel.fontSize = 28f;
            pageLabel.alignment = TextAlignmentOptions.Center;
            pageLabel.raycastTarget = false;
            labelGO.AddComponent<LayoutElement>().preferredWidth = 60f;

            nextButton = BuildArrow(barRt, "NextPage", "▶");
        }

        Button BuildArrow(RectTransform parent, string name, string glyph)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);

            var btn = go.AddComponent<Button>();

            go.AddComponent<LayoutElement>().preferredWidth = 44f;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGO.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text = glyph;
            t.fontSize = 28f;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            return btn;
        }
    }
}
