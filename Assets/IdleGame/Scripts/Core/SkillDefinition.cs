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
    }

    public enum SkillEffectType
    {
        None,
        BonusAttack,
        BonusDefense,
        BonusMaxHP,
        BonusMaxMP,
        BonusAccuracy,
        BonusStr,
        BonusDex,
        BonusWis,
        BonusLuk,
    }
}
