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

        // Account-wide display name, entered once on the character-select screen the
        // first time the game is launched. Its presence is what distinguishes a fresh
        // install (prompt for a name) from a returning player (skip straight to the roster).
        public string accountName = "";

        public List<string> characterNames = new();

        // UTC timestamp of the last save. AFK/offline gains will diff the next
        // session's start time against this.
        public long savedAtUtcTicks;
        public string savedAtUtc;   // human-readable copy for eyeballing the file

        // Per-room kill totals (game-wide, not per-character, so it lives on the master
        // save). Each portal's unlocked state is DERIVED from these on load — kills are
        // the single source of truth — so there's no separate unlocked flag to persist.
        public List<RoomProgressSaveEntry> rooms = new();
    }

    [Serializable]
    public class RoomProgressSaveEntry
    {
        public string roomId;
        public int kills;
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

        // AFK state: the character's assigned offline activity and the pending pile of
        // gains accrued while the game was closed (filled on load — see AfkSystem.cs).
        public AfkActivity activity = new();
        public AfkClaim afkClaim = new();

        public List<string> unlockedClassIds = new();

        // SkillRegistry is already save-shaped: a string-keyed list of
        // (skillId, level) plus unspent points, so it embeds directly.
        public SkillRegistry skills = new();

        // GatheringRegistry is likewise save-shaped (a per-skill list of level + XP).
        public GatheringRegistry gathering = new();

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
