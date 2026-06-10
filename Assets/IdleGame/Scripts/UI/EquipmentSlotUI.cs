using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class EquipmentSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] EquipSlot slot;
        [SerializeField] Image     iconImage;
        [SerializeField] TMP_Text  slotLabel;

        float _lastClickTime;
        const float DoubleClickThreshold = 0.3f;

        void Awake()
        {
            Debug.Log($"[EquipmentSlotUI] {gameObject.name} configured as slot={slot}");
        }

        public void Refresh(ItemDefinition item)
        {
            if (iconImage == null) return;
            if (item != null)
            {
                iconImage.sprite = item.icon;
                iconImage.color  = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color  = Color.clear;
            }
        }

        // ── Drag item out of equipment slot ──────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            TooltipManager.Instance?.Hide();
            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null || character.equipment.IsEmpty(slot)) return;

            var item = character.equipment.Get(slot);
            Debug.Log($"[Drag] Starting drag from equipment slot {slot} for '{item.itemName}'");
            ItemDragManager.Instance?.BeginDragFromEquipment(item, slot);
        }

        public void OnDrag(PointerEventData eventData)
        {
            ItemDragManager.Instance?.MoveGhost(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log($"[Drag] EndDrag from equipment slot {slot}");
            ItemDragManager.Instance?.EndDrag();
        }

        // ── Accept drops from inventory slots ────────────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            var dm = ItemDragManager.Instance;
            if (dm?.DraggedItem == null) return;

            // Ignore equipment-to-equipment drops
            if (dm.SourceEquipSlot != EquipSlot.None) return;

            Debug.Log($"[Drop] OnDrop fired on {gameObject.name} (slot={slot})");
            if (dm.DraggedItem.equipSlot != slot)
            {
                Debug.Log($"[Drop] Wrong slot — item wants {dm.DraggedItem.equipSlot}, this is {slot}");
                return;
            }

            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null) { Debug.Log("[Drop] No active character"); return; }
            if (EquipmentManager.Instance == null || !EquipmentManager.Instance.CanEquip(dm.DraggedItem, character))
            {
                Debug.Log($"[Drop] {character.ClassName} can't equip '{dm.DraggedItem.itemName}'");
                return;
            }

            var item     = dm.DraggedItem;
            int srcIndex = dm.SourceSlotIndex;
            Debug.Log($"[Drop] Equipping '{item.itemName}' from inventory slot {srcIndex}");
            // Equip swaps any worn item back to inventory; restore ours if it fails.
            Inventory.Instance?.RemoveAt(srcIndex);
            if (!EquipmentManager.Instance.Equip(item, character))
                Inventory.Instance?.SetSlot(srcIndex, item);
        }

        // ── Double-click to unequip ───────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime <= DoubleClickThreshold)
            {
                _lastClickTime = 0f;
                TryUnequip();
            }
            else
            {
                _lastClickTime = now;
            }
        }

        // ── Hover tooltip ─────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            var item = PlayerManager.Instance?.ActiveCharacter?.equipment.Get(slot);
            if (item == null) return;
            TooltipManager.Instance?.Show(ItemTooltips.Describe(item, PlayerManager.Instance?.ActiveCharacter));
        }

        public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.Hide();

        void OnDisable() => TooltipManager.Instance?.Hide();

        void TryUnequip()
        {
            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null) return;
            var current = character.equipment.Get(slot);
            Debug.Log($"[Equipment] TryUnequip — this slot={slot}, contains='{(current != null ? current.itemName : "nothing")}'");
            EquipmentManager.Instance?.Unequip(slot, character);
        }
    }
}
