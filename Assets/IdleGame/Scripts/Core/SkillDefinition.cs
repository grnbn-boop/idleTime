using UnityEngine;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "IdleTime/Skill")]
    public class SkillDefinition : ScriptableObject
    {
        public string skillName = "New Skill";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        public int maxLevel = 1;

        [Header("Effect per Level")]
        public SkillEffectType effectType = SkillEffectType.None;
        public float effectValuePerLevel;

        [Tooltip("Treat effectValuePerLevel as a percentage of the base stat instead of a " +
                 "flat amount (e.g. 0.1 = +10% per level). Applies to the flat stat effects " +
                 "(Max HP, Attack, Defense). The derived effects below — crit, drop rate, " +
                 "move speed, etc. — are always percentage-based and ignore this toggle.")]
        public bool isPercentage;

        // True when the effect is inherently a percentage/multiplier (the stat-derived
        // bucket), regardless of the isPercentage toggle.
        public bool IsPercentEffect => isPercentage || IsInherentlyPercent(effectType);

        public static bool IsInherentlyPercent(SkillEffectType type) => type switch
        {
            SkillEffectType.BonusCritChance => true,
            SkillEffectType.BonusCritDamage => true,
            SkillEffectType.BonusMoveSpeed  => true,
            SkillEffectType.BonusDropRate   => true,
            SkillEffectType.BonusXPGain     => true,
            SkillEffectType.BonusBossDamage => true,
            SkillEffectType.BonusMpRegen    => true,
            SkillEffectType.BonusDamage     => true,
            _                               => false,
        };
    }

    // NOTE: serialized in assets as an int (e.g. WisUp = 8). Only ever APPEND new values
    // so existing skill assets keep pointing at the right effect.
    public enum SkillEffectType
    {
        None,           // 0
        BonusAttack,    // 1
        BonusDefense,   // 2
        BonusMaxHP,     // 3
        BonusMaxMP,     // 4
        BonusAccuracy,  // 5
        BonusStr,       // 6
        BonusDex,       // 7
        BonusWis,       // 8
        BonusLuk,       // 9

        // ── Stat-derived / percentage effects (always treated as fractions) ────
        BonusCritChance, // 10  +X% crit chance
        BonusCritDamage, // 11  +X% crit damage
        BonusMoveSpeed,  // 12  +X% movement speed
        BonusDropRate,   // 13  +X% drop rate
        BonusXPGain,     // 14  +X% EXP gain
        BonusBossDamage, // 15  +X% damage vs bosses
        BonusMpRegen,    // 16  +X% MP regen
        BonusDamage,     // 17  +X% to all outgoing hit damage

        // Honours the isPercentage toggle (like the flat stat effects above): unchecked =
        // flat Weapon Power, checked = +X% of Weapon Power.
        BonusWeaponPower, // 18  +X (flat) or +X% to Weapon Power
    }
}
