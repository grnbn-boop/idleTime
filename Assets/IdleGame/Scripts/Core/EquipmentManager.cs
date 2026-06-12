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
            if (Instance != null && Instance != this) { Destroy(transform.root.gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        // True if this character's current class may equip the item into its slot.
        public bool CanEquip(ItemDefinition item, CharacterData character)
        {
            if (item == null || character == null)
            {
                Debug.Log($"[Equip] CanEquip=false — item={(item != null ? item.itemName : "null")}, character={(character != null ? character.characterName : "null")}.");
                return false;
            }
            if (item.equipSlot == EquipSlot.None)
            {
                Debug.Log($"[Equip] CanEquip=false — '{item.itemName}' has equipSlot=None (not equippable).");
                return false;
            }
            if (!item.AllowsClass(character.playerClass))
            {
                Debug.Log($"[Equip] CanEquip=false — '{item.itemName}' is class-restricted and {character.ClassName} is not in its allowed list.");
                return false;
            }
            return true;
        }

        // Equips the item, swapping any currently-worn item in that slot back to
        // inventory. Returns false if the class can't wear it, or if a swap is needed
        // but the inventory has no room for the displaced item.
        public bool Equip(ItemDefinition item, CharacterData character)
        {
            if (!CanEquip(item, character)) return false;

            var displaced = character.equipment.Get(item.equipSlot);
            if (displaced != null && (Inventory.Instance == null || Inventory.Instance.IsFull))
            {
                // nowhere to put the old item — abort rather than destroy it
                Debug.Log($"[Equip] Can't equip '{item.itemName}' — slot {item.equipSlot} already holds '{displaced.itemName}' and the inventory is full, so there's nowhere to return it.");
                return false;
            }

            character.equipment.Set(item.equipSlot, item);
            if (displaced != null) Inventory.Instance.AddItem(displaced);

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

        // Debug: slams an item straight into its slot, discarding whatever is there —
        // no inventory round-trip, no class check, no full-inventory abort. For testing
        // that equip + UI rendering work from a known state.
        public void ForceEquip(ItemDefinition item, CharacterData character)
        {
            if (item == null || character == null || item.equipSlot == EquipSlot.None)
            {
                Debug.LogWarning($"[Equip] ForceEquip skipped — item='{(item != null ? item.itemName : "null")}', slot={(item != null ? item.equipSlot.ToString() : "n/a")}.");
                return;
            }
            character.equipment.Set(item.equipSlot, item);
            RecomputeBonuses(character);
            OnEquipmentChanged?.Invoke();
            PlayerManager.Instance?.NotifyStatsChanged();
            Debug.Log($"[Equip] Force-equipped '{item.itemName}' into {item.equipSlot} on '{character.characterName}'.");
        }

        // Strips every equipment slot without returning items to inventory. Debug/
        // maintenance hook for building a clean slate; pairs with Inventory.Clear().
        public void ClearAll(CharacterData character)
        {
            if (character == null) return;
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
                if (slot != EquipSlot.None) character.equipment.Set(slot, null);
            RecomputeBonuses(character);
            OnEquipmentChanged?.Invoke();
            PlayerManager.Instance?.NotifyStatsChanged();
            Debug.Log($"[Equipment] Cleared all slots for '{character.characterName}'.");
        }

        public void RecomputeBonuses(CharacterData character)
        {
            if (character == null) return;
            character.equipBonusAttack   = 0;
            character.equipBonusDefense  = 0;
            character.equipBonusAccuracy = 0;
            character.equipBonusMaxHP    = 0;
            character.equipBonusStr      = 0;
            character.equipBonusDex      = 0;
            character.equipBonusWis      = 0;
            character.equipBonusLuk      = 0;

            // Percent buckets — no gear source authors these yet, but CharacterData reads
            // them, so reset to 0 here. Sum them in the loop below once gear gains
            // percent fields (e.g. item.bonusMaxHPPercent).
            character.equipBonusMaxHPPercent   = 0f;
            character.equipBonusAttackPercent  = 0f;
            character.equipBonusDefensePercent = 0f;
            character.equipBonusCritChance     = 0f;
            character.equipBonusCritDamage     = 0f;
            character.equipBonusMoveSpeed      = 0f;
            character.equipBonusDropRate       = 0f;
            character.equipBonusXPGain         = 0f;
            character.equipBonusBossDamage     = 0f;
            character.equipBonusMpRegen        = 0f;
            character.equipBonusDamage         = 0f;
            character.equipWeaponPower            = 0;
            character.equipBonusWeaponPower       = 0;
            character.equipBonusWeaponPowerPercent = 0f;

            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var item = character.equipment.Get(slot);
                if (item == null) continue;
                character.equipBonusAttack   += item.bonusAttack;
                character.equipBonusDefense  += item.bonusDefense;
                character.equipBonusAccuracy += item.bonusAccuracy;
                character.equipBonusStr      += item.bonusStr;
                character.equipBonusDex      += item.bonusDex;
                character.equipBonusWis      += item.bonusWis;
                character.equipBonusLuk      += item.bonusLuk;
                if (item is ArmorDefinition armor)
                    character.equipBonusMaxHP += armor.bonusMaxHP;
                if (item is WeaponDefinition weapon)
                    character.equipWeaponPower += weapon.baseWeaponPower;
            }

            // MaxHP may have just dropped (unequipped a +MaxHP item) — keep
            // currentHP from sitting above the new ceiling.
            character.ClampVitals();
        }
    }
}
