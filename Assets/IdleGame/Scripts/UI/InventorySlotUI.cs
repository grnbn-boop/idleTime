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

        public void Refresh(InventorySlot slot)
        {
            if (slot == null)  { Debug.LogError($"[InventorySlotUI] slot is null on {gameObject.name}");  return; }
            if (iconImage == null) { Debug.LogError($"[InventorySlotUI] iconImage not wired on {gameObject.name}"); return; }
            if (countLabel == null) { Debug.LogError($"[InventorySlotUI] countLabel not wired on {gameObject.name}"); return; }

            if (!slot.IsEmpty)
            {
                iconImage.sprite   = slot.item.icon;
                iconImage.color    = Color.white;
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
            float now = Time.unscaledTime;
            if (now - _lastClickTime <= DoubleClickThreshold)
            {
                _lastClickTime = 0f;
                TryEquip();
            }
            else
            {
                _lastClickTime = now;
            }
        }

        // ── Hover tooltip ─────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty) return;
            TooltipManager.Instance?.Show(ItemTooltips.Describe(slot.item));
        }

        public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.Hide();

        void OnDisable() => TooltipManager.Instance?.Hide();

        void TryEquip()
        {
            if (_slotIndex < 0) return;
            var slot = Inventory.Instance?.GetSlot(_slotIndex);
            if (slot == null || slot.IsEmpty || slot.item.equipSlot == EquipSlot.None) return;

            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null || !character.equipment.IsEmpty(slot.item.equipSlot)) return;

            var item = slot.item;
            Inventory.Instance.RemoveAt(_slotIndex);
            EquipmentManager.Instance?.Equip(item, character);
        }
    }
}
