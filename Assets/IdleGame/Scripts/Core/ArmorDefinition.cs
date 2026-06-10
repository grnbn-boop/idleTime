using UnityEngine;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewArmor", menuName = "IdleTime/Armor")]
    public class ArmorDefinition : ItemDefinition
    {
        public int bonusMaxHP;
    }
}
