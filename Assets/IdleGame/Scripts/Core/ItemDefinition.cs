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

        // Equipment — only used when equipSlot != None
        public EquipSlot equipSlot = EquipSlot.None;
        public int bonusAttack;
        public int bonusDefense;
        public int bonusAccuracy;

        [Header("Primary Stat Bonuses")]
        public int bonusStr;
        public int bonusDex;
        public int bonusWis;
        public int bonusLuk;
    }
}
