using System;
using System.Collections.Generic;

namespace IdleTime.Core
{
    // ── Master save: one per game ─────────────────────────────────────────────
    // Game-wide state that doesn't belong to any single character.

    [Serializable]
    public class MasterSaveData
    {
        public int version = 1;
        public int activeIndex;
        public List<string> characterNames = new();

        // UTC timestamp of the last save. AFK/offline gains will diff the next
        // session's start time against this.
        public long savedAtUtcTicks;
        public string savedAtUtc;   // human-readable copy for eyeballing the file
    }

    // ── Character save: one per character ────────────────────────────────────
    // All ids are asset names (ItemDefinition.name / PlayerClass.name), resolved
    // back to assets through SaveManager's databases on load.

    [Serializable]
    public class CharacterSaveData
    {
        public string characterName;
        public string classId;
        public int level = 1;
        public float currentXP;
        public int gold;

        // Activity stub — no activity system exists yet. When AFK gains land,
        // these record what the character was doing and since when, so offline
        // progress can be computed on load.
        public string currentActivity = "Idle";
        public long activityStartedUtcTicks;

        public List<string> unlockedClassIds = new();

        // SkillRegistry is already save-shaped: a string-keyed list of
        // (skillId, level) plus unspent points, so it embeds directly.
        public SkillRegistry skills = new();

        public List<EquipmentSaveEntry> equipment = new();
        public List<InventorySaveEntry> inventory = new();
    }

    [Serializable]
    public class EquipmentSaveEntry
    {
        public string slot;     // EquipSlot enum name
        public string itemId;
    }

    [Serializable]
    public class InventorySaveEntry
    {
        public int index;
        public string itemId;
        public int count;
    }
}
