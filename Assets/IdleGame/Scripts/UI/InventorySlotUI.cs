using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class InventorySlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image    iconImage;
        [SerializeField] TMP_Text countLabel;

        int   _slotIndex = -1;
        float _lastClickTime;
        const float DoubleClickThreshold = 0.3f;

        public void Init(int slotIndex) => _slotIndex = slotIndex;

        // The count label's rect (TMP default 200×50) is far larger than the ~31px cell.
        // It must never catch raycasts: when a stack enables it, it would steal
        // drags/drops aimed at neighbouring slots. The icon is the slot's hit area.
        void Awake()
        {
            if (countLabel != null) countLabel.raycastTarget = false;
        }

        public void Refresh(InventorySlot slot)
        {
            if (slot == null)  { Debug.LogError($"[InventorySlotUI] slot is null on {gameObject.name}");  return; }
            if (iconImage == null) { Debug.LogError($"[InventorySlotUI] iconImage not wired on {gameObject.name}"); return; }
            if (countLabel == null) { Debug.LogError($"[InventorySlotUI] countLabel not wired on {gameObject.name}"); return; }

            // A disabled Image silently draws nothing even with a sprite assigned —
            // re-enable so a stray scene tick can't blank the slot.
            iconImage.enabled = true;

            if (!slot.IsEmpty)
            {
                iconImage.sprite   = slot.item.icon;
                iconImage.color    = IconTint(slot.item);
                countLabel.text    = slot.count > 1 ? slot.count.ToString() : "";
                countLabel.enabled = slot.count > 1;
            }
            else
            {
                iconImage.sprite   = null;
                iconImage.color    = Color.clear;
                countLabel.enabled = false;
            }
        }

        // ── Drag out to equipment slot ────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            TooltipManager.Instance?.Hide();   // don't trail a tooltip behind the dragged item
            Debug.Log($"[Drag] OnBeginDrag fired on {gameObject.name}, slotIndex={_slotIndex}");
            if (_slotIndex < 0)
            {
                Debug.LogWarning("[Drag] Skipped — slotIndex not initialised");
                return;
            }
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                Debug.Log("[Drag] Skipped — slot null or empty");
                return;
            }
            Debug.Log($"[Drag] Starting drag for '{slot.item.itemName}' (equipSlot={slot.item.equipSlot}), ItemDragManager={ItemDragManager.Instance != null}");
            // If the icon currently shown in this slot doesn't match the item we're about to
            // grab, the slot's visual is stale (Refresh didn't repaint after the last swap) —
            // the user sees one item but grabs another.
            string shownIcon = iconImage != null ? (iconImage.sprite != null ? iconImage.sprite.name : "none") : "no-image";
            string dataIcon  = slot.item.icon != null ? slot.item.icon.name : "none";
            if (shownIcon != dataIcon)
                Debug.LogWarning($"[Drag] STALE ICON on slot {_slotIndex}: showing '{shownIcon}' but data is '{slot.item.itemName}' (icon '{dataIcon}'). Slot visual didn't refresh.");
            ItemDragManager.Instance?.BeginDrag(slot.item, _slotIndex);
        }

        public void OnDrag(PointerEventData eventData)
        {
            ItemDragManager.Instance?.MoveGhost(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log($"[Drag] OnEndDrag fired on {gameObject.name}");
            ItemDragManager.Instance?.EndDrag();
        }

        // ── Accept drops from other inventory slots (move / swap) ────────────

        public void OnDrop(PointerEventData eventData)
        {
            var dm = ItemDragManager.Instance;
            if (dm?.DraggedItem == null) return;

            if (dm.SourceEquipSlot != EquipSlot.None)
            {
                // Dragging from an equipment slot into inventory
                Debug.Log($"[Drop] Equipment {dm.SourceEquipSlot} → inventory slot {_slotIndex}");
                var character = PlayerManager.Instance?.ActiveCharacter;
                if (character == null) return;
                EquipmentManager.Instance?.UnequipToSlot(dm.SourceEquipSlot, _slotIndex, character);
                return;
            }

            int src = dm.SourceSlotIndex;
            if (src < 0 || src == _slotIndex) return;
            Debug.Log($"[Drop] Inventory swap: slot {src} ↔ slot {_slotIndex}");
            Inventory.Instance?.SwapSlots(src, _slotIndex);
        }

        // ── Double-click to equip ─────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            // Right-click sells one unit to an open shop; with no shop open it falls back
            // to discarding the whole stack to free the slot.
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (TrySellToOpenShop()) return;
                Discard();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left) return;

            float now = Time.unscaledTime;
            if (now - _lastClickTime <= DoubleClickThreshold)
            {
                _lastClickTime = 0f;
                UseOrEquip();
            }
            else
            {
                _lastClickTime = now;
            }
        }

        // Drops the slot's stack entirely. Tooltip is hovering this slot, so dismiss it.
        void Discard()
        {
            if (_slotIndex < 0) return;
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return;

            Debug.Log($"[Inventory] Discarding '{slot.item.itemName}' ×{slot.count} from slot {_slotIndex}");
            Inventory.Instance.RemoveAt(_slotIndex);
            TooltipManager.Instance?.Hide();
        }

        // ── Hover tooltip ─────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return;

            string text = ItemTooltips.Describe(slot.item, PlayerManager.Instance?.ActiveCharacter);

            // While a shop is open, telegraph that right-click sells this stack.
            var shop = ShopUI.Instance != null && ShopUI.Instance.IsOpen ? ShopUI.Instance.ActiveShop : null;
            if (shop != null)
                text += $"\n<color=#E8C84A>Right-click to sell: {shop.GetSellPrice(slot.item)}g</color>";

            TooltipManager.Instance?.Show(text);
        }

        // Sells one unit of this slot to the active shop, if the shop overlay is open.
        // Returns false (so right-click can fall back to Discard) when no shop is open.
        bool TrySellToOpenShop()
        {
            var shopUI = ShopUI.Instance;
            if (shopUI == null || !shopUI.IsOpen) return false;
            var shop = shopUI.ActiveShop;
            if (shop == null || _slotIndex < 0) return false;

            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return false;

            var item = slot.item;
            int price = shop.GetSellPrice(item);
            PlayerManager.Instance?.AddGold(price);
            Inventory.Instance.RemoveOne(_slotIndex);
            shopUI.ReportSale(item, price);

            if (Inventory.Instance.GetSlot(_slotIndex).IsEmpty) TooltipManager.Instance?.Hide();
            return true;
        }

        // Unwearable equipment (wrong class) reads at a glance via a red dim.
        Color IconTint(ItemDefinition item)
        {
            var character = PlayerManager.Instance?.ActiveCharacter;
            bool restricted = item.equipSlot != EquipSlot.None
                           && character != null
                           && !item.AllowsClass(character.playerClass);
            return restricted ? new Color(1f, 0.55f, 0.55f, 0.6f) : Color.white;
        }

        public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.Hide();

        // Debug: logs this slot's live visual state next to its data, to diagnose icons
        // that won't draw even though the sprite is assigned (alpha 0, disabled, empty
        // sprite region, zero rect, etc.). Driven by DebugCommands.
        public void DebugDumpVisual()
        {
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            string data = (slot != null && !slot.IsEmpty) ? slot.item.itemName : "<empty>";
            if (iconImage == null) { Debug.Log($"[DumpInv] slot {_slotIndex} '{data}': iconImage is NULL"); return; }
            var sp = iconImage.sprite;
            Debug.Log($"[DumpInv] slot {_slotIndex} data='{data}' | enabled={iconImage.enabled} active={iconImage.gameObject.activeInHierarchy} " +
                      $"alpha={iconImage.color.a:0.##} sprite={(sp != null ? sp.name : "NULL")} spriteRect={(sp != null ? sp.rect.size.ToString() : "-")} " +
                      $"rect={((RectTransform)iconImage.transform).rect.size} lossyScale={iconImage.transform.lossyScale}");
        }

        void OnDisable() => TooltipManager.Instance?.Hide();

        // Double-click dispatch: consumables get used, gear gets equipped.
        void UseOrEquip()
        {
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return;
            if (slot.item.itemType == ItemType.Consumable) TryUse();
            else TryEquip();
        }

        void TryEquip()
        {
            if (_slotIndex < 0) return;
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty || slot.item.equipSlot == EquipSlot.None)
            {
                Debug.Log($"[Equip] Double-click on slot {_slotIndex} did nothing — empty or not equippable (equipSlot={(slot != null && !slot.IsEmpty ? slot.item.equipSlot.ToString() : "n/a")}).");
                return;
            }

            Debug.Log($"[Equip] Double-click equip attempt: '{slot.item.itemName}' (equipSlot={slot.item.equipSlot}) from slot {_slotIndex}.");

            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null) { Debug.Log("[Equip] Aborted — no active character."); return; }
            if (EquipmentManager.Instance == null) { Debug.Log("[Equip] Aborted — no EquipmentManager in scene."); return; }
            if (!EquipmentManager.Instance.CanEquip(slot.item, character)) return; // CanEquip logs the reason

            var item = slot.item;
            Inventory.Instance.RemoveAt(_slotIndex);
            // Equip swaps any worn item back to inventory; if it fails, return ours.
            if (!EquipmentManager.Instance.Equip(item, character))
                Inventory.Instance.SetSlot(_slotIndex, item);
        }

        void TryUse()
        {
            if (_slotIndex < 0) return;
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return;

            var item = slot.item;
            var pm = PlayerManager.Instance;
            var c = pm?.ActiveCharacter;
            if (c == null) return;

            float hpRestore = item.restoreHP + item.restoreHPPercent * c.MaxHP;
            float mpRestore = item.restoreMP + item.restoreMPPercent * c.MaxMP;

            // Don't waste a consumable when it would restore nothing.
            bool wouldHelp = (hpRestore > 0f && c.currentHP < c.MaxHP)
                          || (mpRestore > 0f && c.currentMP < c.MaxMP);
            if (!wouldHelp) return;

            if (hpRestore > 0f) pm.ModifyHP(hpRestore);
            if (mpRestore > 0f) pm.ModifyMP(mpRestore);
            Inventory.Instance.RemoveOne(_slotIndex);

            // Drop the tooltip if the stack is now empty (we're still hovering the slot).
            if (Inventory.Instance.GetSlot(_slotIndex).IsEmpty) TooltipManager.Instance?.Hide();
        }
    }
}
