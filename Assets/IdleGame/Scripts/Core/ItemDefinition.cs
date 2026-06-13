using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    public enum ItemType { Misc, Weapon, Armor, Consumable }
    public enum EquipSlot { None, Helmet, Chest, Legs, MainHand, OffHand, Ring, Necklace }

    [CreateAssetMenu(fileName = "NewItem", menuName = "IdleTime/Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemName = "Item";
        public Sprite icon;
        public ItemType itemType = ItemType.Misc;
        [TextArea] public string description;

        [Header("Value")]
        [Tooltip("Reference worth of one unit, in gold. Shops derive the sell price from " +
                 "this (baseValue × the shop's sell multiplier) and may fall back to it for " +
                 "the buy price when a stock entry leaves its price at 0.")]
        public int baseValue;

        [Header("Currency — used when this item is a coin")]
        [Tooltip("Gold this coin is worth. > 0 makes the item soft currency: picking it up " +
                 "adds to the player's gold instead of taking an inventory slot.")]
        public int currencyValue;

        public bool IsCurrency => currencyValue > 0;

        // Equipment — only used when equipSlot != None
        public EquipSlot equipSlot = EquipSlot.None;

        [Tooltip("Classes that may equip this item. Leave empty = any class.")]
        public List<PlayerClass> allowedClasses = new();
        public int bonusAttack;
        public int bonusDefense;
        public int bonusAccuracy;

        [Header("Primary Stat Bonuses")]
        public int bonusStr;
        public int bonusDex;
        public int bonusWis;
        public int bonusLuk;

        [Header("Consumable — used when itemType == Consumable")]
        public int restoreHP;
        public int restoreMP;

        // Empty allowedClasses = usable by everyone. Otherwise the class must be listed.
        public bool AllowsClass(PlayerClass playerClass)
        {
            if (allowedClasses == null || allowedClasses.Count == 0) return true;
            return playerClass != null && allowedClasses.Contains(playerClass);
        }

        public bool IsClassRestricted => allowedClasses != null && allowedClasses.Count > 0;
        [Tooltip("Fraction of Max HP restored on use (0.25 = 25%). Added on top of the flat amount.")]
        [Range(0f, 1f)] public float restoreHPPercent;
        [Tooltip("Fraction of Max MP restored on use (0.25 = 25%). Added on top of the flat amount.")]
        [Range(0f, 1f)] public float restoreMPPercent;
    }
}
