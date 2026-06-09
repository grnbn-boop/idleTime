using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    [Serializable]
    public class SkillRegistry
    {
        public int availableSkillPoints = 5;

        [SerializeField] private List<SkillSaveEntry> savedSkills = new();

        // Runtime-only lookup rebuilt from savedSkills on first access after deserialization.
        [NonSerialized] private Dictionary<string, int> _levels;

        public int GetLevel(SkillDefinition skill)
        {
            EnsureDict();
            return _levels.TryGetValue(skill.name, out int v) ? v : 0;
        }

        public bool IsUnlocked(SkillDefinition skill) => GetLevel(skill) > 0;

        public bool TryUnlock(SkillDefinition skill)
        {
            if (availableSkillPoints <= 0) return false;
            int current = GetLevel(skill);
            if (current >= skill.maxLevel) return false;
            EnsureDict();
            _levels[skill.name] = current + 1;
            SyncList();
            availableSkillPoints--;
            return true;
        }

        private void EnsureDict()
        {
            if (_levels != null) return;
            _levels = new Dictionary<string, int>();
            foreach (var e in savedSkills)
                _levels[e.skillId] = e.level;
        }

        private void SyncList()
        {
            savedSkills.Clear();
            foreach (var kv in _levels)
                savedSkills.Add(new SkillSaveEntry { skillId = kv.Key, level = kv.Value });
        }
    }

    [Serializable]
    public class SkillSaveEntry
    {
        public string skillId;
        public int level;
    }
}
