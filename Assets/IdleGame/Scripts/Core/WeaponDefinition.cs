using UnityEngine;

namespace IdleTime.Core
{
    public enum WeaponType { Melee, Ranged }

    [CreateAssetMenu(fileName = "NewWeapon", menuName = "IdleTime/Weapon")]
    public class WeaponDefinition : ItemDefinition
    {
        public WeaponType weaponType;

        [Tooltip("Base damage this weapon contributes to the hit-damage formula. Added to " +
                 "Attack as the damage base, then scaled by any Weapon Power skill/gear bonuses.")]
        public int baseWeaponPower;
    }
}
