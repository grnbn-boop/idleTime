using UnityEngine;

namespace IdleTime.Core
{
    public enum ItemType { Misc, Weapon, Armor, Consumable }

    [CreateAssetMenu(fileName = "NewItem", menuName = "IdleTime/Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemName = "Item";
        public Sprite icon;
        public ItemType itemType = ItemType.Misc;
        [TextArea] public string description;
    }
}
