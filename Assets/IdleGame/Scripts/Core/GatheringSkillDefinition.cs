using UnityEngine;

namespace IdleTime.Core
{
    // Tuning + presentation for one gathering skill. Authored as an asset under a
    // Resources/Gathering folder so GatheringManager can load them with no scene
    // wiring (run "IdleTime ▸ Create Gathering Skill Definitions" to stamp the three).
    // If none exist, GatheringManager builds equivalent runtime defaults, so the system
    // works out of the box.
    [CreateAssetMenu(fileName = "NewGatheringSkill", menuName = "IdleTime/Gathering Skill")]
    public class GatheringSkillDefinition : ScriptableObject
    {
        public GatheringSkillType type = GatheringSkillType.Woodcutting;
        public string displayName = "Woodcutting";
        [Tooltip("Floats over the player's head while gathering this skill. Leave empty to use a coloured placeholder square.")]
        public Sprite icon;
        [Tooltip("Tint of the placeholder head-icon / VFX when no icon sprite is set.")]
        public Color placeholderColor = Color.white;

        [Header("Stat Scaling")]
        [Tooltip("Primary stat that improves this skill's success chance.")]
        public PrimaryStat buffStat = PrimaryStat.Wis;
        [Tooltip("Success chance added per point of the buff stat (0.01 = +1% per point).")]
        public float statWeight = 0.01f;
        [Tooltip("Success chance added per gathering level above 1 (0.005 = +0.5% per level).")]
        public float levelWeight = 0.005f;

        [Header("Success Clamp")]
        [Range(0f, 1f)] public float minSuccessChance = 0.05f;
        [Range(0f, 1f)] public float maxSuccessChance = 0.95f;

        [Header("Levelling")]
        public int maxLevel = 99;
        [Tooltip("XP granted per successful gather.")]
        public int xpPerGather = 10;
        [Tooltip("XP needed to reach level+1 = this × current level.")]
        public int xpToLevelBase = 50;

        [Tooltip("Marks a skill that's wired but not fully designed yet (e.g. Crafting). " +
                 "Purely informational — shown in tooltips/logs.")]
        public bool isStub;

        // XP required to advance FROM the given level to the next.
        public int XpToNext(int level) => xpToLevelBase * Mathf.Max(1, level);
    }
}
