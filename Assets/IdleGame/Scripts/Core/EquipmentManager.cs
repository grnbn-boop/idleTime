using System;
using UnityEngine;

namespace IdleTime.Core
{
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        public event Action OnEquipmentChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool Equip(ItemDefinition item, CharacterData character)
        {
            if (item == null || character == null) return false;
            if (item.equipSlot == EquipSlot.None) return false;

            character.equipment.Set(item.equipSlot, item);
            RecomputeBonuses(character);
            OnEquipmentChanged?.Invoke();
            PlayerManager.Instance?.NotifyStatsChanged();
            return true;
        }

        // Moves the item in slot back to inventory. Returns false if inventory is full.
        public bool Unequip(EquipSlot slot, CharacterData character)
        {
            if (character == null) return false;
            var item = character.equipment.Get(slot);
            if (item == null) return false;
            if (Inventory.Instance == null || Inventory.Instance.IsFull) return false;

            Inventory.Instance.AddItem(item);
            character.equipment.Set(slot, null);
            RecomputeBonuses(character);
            OnEquipmentChanged?.Invoke();
            PlayerManager.Instance?.NotifyStatsChanged();
            return true;
        }

        // Unequips to a specific inventory slot. If that slot has a matching item, swaps.
        public bool UnequipToSlot(EquipSlot equipSlot, int inventoryIndex, CharacterData character)
        {
            if (character == null) return false;
            var item = character.equipment.Get(equipSlot);
            if (item == null) return false;
            if (Inventory.Instance == null) return false;

            var targetSlot = Inventory.Instance.GetSlot(inventoryIndex);
            ItemDefinition displaced = null;

            if (!targetSlot.IsEmpty)
            {
                if (targetSlot.item.equipSlot != equipSlot) return false;
                displaced = targetSlot.item;
            }

            Inventory.Instance.SetSlot(inventoryIndex, item);
            character.equipment.Set(equipSlot, displaced);
            RecomputeBonuses(character);
            OnEquipmentChanged?.Invoke();
            PlayerManager.Instance?.NotifyStatsChanged();
            return true;
        }

        public void RecomputeBonuses(CharacterData character)
        {
            if (character == null) return;
            character.equipBonusAttack   = 0;
            character.equipBonusDefense  = 0;
            character.equipBonusAccuracy = 0;
            character.equipBonusMaxHP    = 0;

            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var item = character.equipment.Get(slot);
                if (item == null) continue;
                character.equipBonusAttack   += item.bonusAttack;
                character.equipBonusDefense  += item.bonusDefense;
                character.equipBonusAccuracy += item.bonusAccuracy;
                if (item is ArmorDefinition armor)
                    character.equipBonusMaxHP += armor.bonusMaxHP;
            }
        }
    }
}
