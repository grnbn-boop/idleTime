using System.Text;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Formats a skill node into tooltip rich-text: name, level, per-level effect,
    // current total, description, and prerequisites (green = met, red = unmet).
    public static class SkillTooltips
    {
        public static string Describe(SkillNodeEntry entry, CharacterData character)
        {
            var skill = entry?.skill;
            if (skill == null) return "";

            int level = character != null ? character.skills.GetLevel(skill) : 0;

            var sb = new StringBuilder();
            sb.Append($"<b>{skill.skillName}</b>");
            sb.Append($"\n<size=80%><color=#9AA0A6>Level {level} / {skill.maxLevel}</color></size>");

            string effect = EffectLabel(skill.effectType);
            if (effect != null && skill.effectValuePerLevel != 0f)
            {
                bool pct = skill.IsPercentEffect;
                sb.Append($"\n{Format(skill.effectValuePerLevel, pct)} {effect} per level");
                if (level > 0)
                    sb.Append($"\n<size=85%>Current: {Format(skill.effectValuePerLevel * level, pct)} {effect}</size>");
            }

            if (!string.IsNullOrEmpty(skill.description))
                sb.Append($"\n<size=85%><i>{skill.description}</i></size>");

            if (entry.prerequisites != null && entry.prerequisites.Count > 0)
            {
                sb.Append("\n<size=80%>Requires: ");
                bool first = true;
                foreach (var prereq in entry.prerequisites)
                {
                    if (prereq == null) continue;
                    if (!first) sb.Append(", ");
                    bool met = character != null && character.skills.IsUnlocked(prereq);
                    sb.Append($"<color=#{(met ? "6FCF6F" : "E06666")}>{prereq.skillName}</color>");
                    first = false;
                }
                sb.Append("</size>");
            }

            return sb.ToString();
        }

        static string EffectLabel(SkillEffectType type) => type switch
        {
            SkillEffectType.BonusAttack     => "Attack",
            SkillEffectType.BonusDefense    => "Defense",
            SkillEffectType.BonusMaxHP      => "Max HP",
            SkillEffectType.BonusMaxMP      => "Max MP",
            SkillEffectType.BonusAccuracy   => "Accuracy",
            SkillEffectType.BonusStr        => "STR",
            SkillEffectType.BonusDex        => "DEX",
            SkillEffectType.BonusWis        => "WIS",
            SkillEffectType.BonusLuk        => "LUK",
            SkillEffectType.BonusCritChance => "Crit Chance",
            SkillEffectType.BonusCritDamage => "Crit Damage",
            SkillEffectType.BonusMoveSpeed  => "Move Speed",
            SkillEffectType.BonusDropRate   => "Drop Rate",
            SkillEffectType.BonusXPGain     => "EXP Gain",
            SkillEffectType.BonusBossDamage => "Boss Damage",
            SkillEffectType.BonusMpRegen    => "MP Regen",
            SkillEffectType.BonusDamage     => "Damage",
            _                               => null,
        };

        // Percent effects store a fraction (0.05) — show it as +5%. Flat effects show the raw value.
        static string Format(float v, bool percent) => percent
            ? (v >= 0 ? "+" : "") + (v * 100f).ToString("0.##") + "%"
            : (v >= 0 ? "+" : "") + v.ToString("0.##");
    }
}
