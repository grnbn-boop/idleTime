using UnityEngine;

namespace IdleTime.Core
{
    public enum WeaponType { Melee, Ranged }

    [CreateAssetMenu(fileName = "NewWeapon", menuName = "IdleTime/Weapon")]
    public class WeaponDefinition : ItemDefinition
    {
        public WeaponType weaponType;
    }
}
