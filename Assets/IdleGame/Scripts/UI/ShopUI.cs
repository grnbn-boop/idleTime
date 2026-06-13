using System.Collections.Generic;
using IdleTime.Core;
using IdleTime.Interactions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleTime.UI
{
    /// <summary>
    /// Screen-space vendor overlay. A self-building, lazily-created singleton (mirrors
    /// TooltipManager) so a ShopNpc can open it with no scene wiring. Renders the active
    /// shop's stock as a clickable icon grid; left-click buys one. Selling is driven from
    /// the player's own inventory slots (right-click) while this overlay is open — see
    /// InventorySlotUI, which reads <see cref="ActiveShop"/>.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        const int SortingOrder = 5000;   // above gameplay UI, below the tooltip (10000)

        ShopNpc _activeShop;
        RectTransform _panel;
        TMP_Text _title;
        TMP_Text _status;
        RectTransform _grid;
        readonly List<GameObject> _slotObjects = new();

        public ShopNpc ActiveShop => _activeShop;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public bool IsOpenFor(ShopNpc shop) => IsOpen && _activeShop == shop;

        // Lazily spin up the overlay the first time a shop is opened.
        public static ShopUI Ensure()
        {
            if (Instance == null)
                new GameObject("ShopUI").AddComponent<ShopUI>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
            HideImmediate();
        }

        void Update()
        {
            if (!IsOpen) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Keep the panel floating over the shopkeeper's head as he / the camera moves.
            TrackShopkeeper();
        }

        // Converts the active shop's head anchor (world) to a screen point and pins the
        // bottom-pivoted panel there, clamped so the panel stays fully on-screen.
        void TrackShopkeeper()
        {
            if (_activeShop == null || _panel == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(_activeShop.OverlayWorldAnchor);

            float w = _panel.rect.width * _panel.lossyScale.x;
            float h = _panel.rect.height * _panel.lossyScale.y;
            float halfW = w * 0.5f;

            float x = Mathf.Clamp(screen.x, halfW, Mathf.Max(halfW, Screen.width - halfW));
            float y = Mathf.Clamp(screen.y, 0f, Mathf.Max(0f, Screen.height - h));   // pivot is bottom-centre
            _panel.position = new Vector3(x, y, 0f);
        }

        // ── Open / close ────────────────────────────────────────────────────────

        public void Open(ShopNpc shop)
        {
            if (shop == null) return;
            _activeShop = shop;
            _title.text = shop.Title;
            _status.text = "";
            Populate(shop);
            _panel.gameObject.SetActive(true);
            TrackShopkeeper();   // position over the NPC before the first rendered frame
        }

        public void Close()
        {
            _activeShop = null;
            TooltipManager.Instance?.Hide();
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        // ── Transactions ──────────────────────────────────────────────────────────

        public void Buy(ShopNpc.ShopEntry entry)
        {
            if (entry == null || entry.item == null || _activeShop == null) return;
            var pm = PlayerManager.Instance;
            var inv = Inventory.Instance;
            if (pm == null || inv == null) return;

            int price = _activeShop.GetBuyPrice(entry);

            // Refuse before spending if the bag can't hold it.
            if (!inv.CanAccept(entry.item)) { Report("Inventory full"); return; }
            if (!pm.TrySpendGold(price)) { Report($"Not enough gold ({price}g)"); return; }

            // Guaranteed to succeed after CanAccept; refund defensively if something raced.
            if (!inv.AddItem(entry.item)) { pm.AddGold(price); Report("Inventory full"); return; }
            Report($"Bought {entry.item.itemName} (-{price}g)");
        }

        // Called by InventorySlotUI when the player right-clicks a stack while the shop is open.
        public void ReportSale(ItemDefinition item, int price) =>
            Report($"Sold {item.itemName} (+{price}g)");

        void Report(string message)
        {
            if (_status != null) _status.text = message;
        }

        // ── Build ───────────────────────────────────────────────────────────────

        void Build()
        {
            var canvasGO = new GameObject("ShopCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Bottom-centre pivot so the panel floats *above* the world anchor point
            // (the shopkeeper's head); TrackShopkeeper() drives its screen position each frame.
            var panelGO = new GameObject("ShopPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);
            _panel = (RectTransform)panelGO.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.sizeDelta = new Vector2(560f, 360f);
            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.07f, 0.1f, 0.96f);

            // Title.
            _title = MakeLabel(_panel, "Title", 28f, TextAlignmentOptions.Center);
            var titleRt = (RectTransform)_title.transform;
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(-20f, 40f);

            // Close button (top-right).
            BuildCloseButton(_panel);

            // Item grid.
            var gridGO = new GameObject("Grid", typeof(RectTransform));
            gridGO.transform.SetParent(_panel, false);
            _grid = (RectTransform)gridGO.transform;
            _grid.anchorMin = new Vector2(0f, 0f); _grid.anchorMax = new Vector2(1f, 1f);
            _grid.offsetMin = new Vector2(16f, 44f);   // leave room for status line
            _grid.offsetMax = new Vector2(-16f, -56f); // leave room for title
            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(72f, 92f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.UpperLeft;

            // Status line (bottom).
            _status = MakeLabel(_panel, "Status", 18f, TextAlignmentOptions.Center);
            var statusRt = (RectTransform)_status.transform;
            statusRt.anchorMin = new Vector2(0f, 0f); statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.anchoredPosition = new Vector2(0f, 10f);
            statusRt.sizeDelta = new Vector2(-20f, 28f);
            _status.color = new Color(0.8f, 0.85f, 0.9f, 1f);
        }

        void Populate(ShopNpc shop)
        {
            foreach (var go in _slotObjects)
                if (go != null) Destroy(go);
            _slotObjects.Clear();

            var stock = shop.Stock;
            if (stock == null) return;

            for (int i = 0; i < stock.Count; i++)
            {
                var entry = stock[i];
                if (entry == null || entry.item == null) continue;

                var slotGO = new GameObject($"ShopSlot_{i}", typeof(RectTransform));
                slotGO.transform.SetParent(_grid, false);

                // Cell background doubles as the raycast hit area — pointer events on the
                // icon/price bubble up to the ShopSlotUI handler on this root.
                var cellBg = slotGO.AddComponent<Image>();
                cellBg.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);

                var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                var iconRt = (RectTransform)icon.transform;
                iconRt.SetParent(slotGO.transform, false);
                iconRt.anchorMin = new Vector2(0.5f, 1f); iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                iconRt.anchoredPosition = new Vector2(0f, -4f);
                iconRt.sizeDelta = new Vector2(64f, 64f);
                icon.sprite = entry.item.icon;
                icon.preserveAspect = true;

                var price = MakeLabel((RectTransform)slotGO.transform, "Price", 16f, TextAlignmentOptions.Center);
                var priceRt = (RectTransform)price.transform;
                priceRt.anchorMin = new Vector2(0f, 0f); priceRt.anchorMax = new Vector2(1f, 0f);
                priceRt.pivot = new Vector2(0.5f, 0f);
                priceRt.anchoredPosition = Vector2.zero;
                priceRt.sizeDelta = new Vector2(0f, 22f);
                price.text = $"{shop.GetBuyPrice(entry)}g";
                price.color = new Color(0.9f, 0.78f, 0.25f, 1f);
                price.raycastTarget = false;

                var slot = slotGO.AddComponent<ShopSlotUI>();
                slot.Bind(this, entry);

                _slotObjects.Add(slotGO);
            }
        }

        void BuildCloseButton(RectTransform parent)
        {
            var go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(28f, 28f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.4f, 0.15f, 0.15f, 0.95f);
            go.AddComponent<Button>().onClick.AddListener(Close);

            var x = MakeLabel(rt, "X", 18f, TextAlignmentOptions.Center);
            var xRt = (RectTransform)x.transform;
            xRt.anchorMin = Vector2.zero; xRt.anchorMax = Vector2.one;
            xRt.offsetMin = xRt.offsetMax = Vector2.zero;
            x.text = "✕";
            x.raycastTarget = false;
        }

        static TMP_Text MakeLabel(RectTransform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            return t;
        }
    }

    /// <summary>
    /// One purchasable cell in the shop grid. Hover shows the item tooltip + buy price;
    /// left-click buys one unit through <see cref="ShopUI"/>.
    /// </summary>
    public class ShopSlotUI : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        ShopUI _shop;
        ShopNpc.ShopEntry _entry;

        public void Bind(ShopUI shop, ShopNpc.ShopEntry entry)
        {
            _shop = shop;
            _entry = entry;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                _shop?.Buy(_entry);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_entry?.item == null || _shop == null) return;
            string body = ItemTooltips.Describe(_entry.item, PlayerManager.Instance?.ActiveCharacter);
            TooltipManager.Instance?.Show($"{body}\n<color=#E8C84A>Buy: {_shop.ActiveShop?.GetBuyPrice(_entry) ?? _entry.price}g</color>");
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) =>
            TooltipManager.Instance?.Hide();
    }
}
