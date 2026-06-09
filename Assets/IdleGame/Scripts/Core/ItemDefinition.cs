using UnityEngine;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "IdleTime/Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemName = "Item";
        public Sprite icon;
    }
}
