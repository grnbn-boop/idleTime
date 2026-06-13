namespace IdleTime.Core
{
    // The player-wide gathering/profession skills (distinct from the ability "skills"
    // managed by SkillManager/SkillRegistry — those modify combat stats; these are
    // levelled by performing world actions). Serialized in saves by NAME, so reorder
    // freely but never rename a member without a migration. Append-only for new skills.
    public enum GatheringSkillType
    {
        Woodcutting,   // buffed by Wisdom
        Mining,        // buffed by Strength
        Crafting,      // buffed by Dexterity — stubbed for now
    }
}
