using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    // Per-character gathering progress: a level + XP per skill. Save-shaped exactly like
    // SkillRegistry — a string-keyed list that JsonUtility serialises directly — so it
    // embeds straight into CharacterData / CharacterSaveData. Keyed by enum NAME so the
    // file survives enum reordering.
    [Serializable]
    public class GatheringRegistry
    {
        [SerializeField] private List<GatheringSaveEntry> entries = new();

        // Runtime lookup rebuilt from entries on first access after deserialization.
        [NonSerialized] private Dictionary<GatheringSkillType, GatheringSaveEntry> _map;

        public int GetLevel(GatheringSkillType type) => GetOrCreate(type).level;
        public float GetXp(GatheringSkillType type) => GetOrCreate(type).xp;

        // Adds XP and rolls as many level-ups as the total allows (capped at the
        // definition's maxLevel). Returns true if at least one level was gained.
        public bool AddXp(GatheringSkillType type, float amount, GatheringSkillDefinition def)
        {
            var e = GetOrCreate(type);
            e.xp += Mathf.Max(0f, amount);

            bool leveled = false;
            if (def != null)
            {
                while (e.level < def.maxLevel && e.xp >= def.XpToNext(e.level))
                {
                    e.xp -= def.XpToNext(e.level);
                    e.level++;
                    leveled = true;
                }
                if (e.level >= def.maxLevel) e.xp = 0f;   // cap: no overflow XP past max
            }
            return leveled;
        }

        private GatheringSaveEntry GetOrCreate(GatheringSkillType type)
        {
            EnsureMap();
            if (!_map.TryGetValue(type, out var e))
            {
                e = new GatheringSaveEntry { skillId = type.ToString(), level = 1, xp = 0f };
                _map[type] = e;
                entries.Add(e);
            }
            return e;
        }

        private void EnsureMap()
        {
            if (_map != null) return;
            _map = new Dictionary<GatheringSkillType, GatheringSaveEntry>();
            foreach (var e in entries)
                if (Enum.TryParse(e.skillId, out GatheringSkillType t))
                    _map[t] = e;
        }
    }

    [Serializable]
    public class GatheringSaveEntry
    {
        public string skillId;
        public int level = 1;
        public float xp;
    }
}
