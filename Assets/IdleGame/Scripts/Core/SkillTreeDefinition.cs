using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewSkillTree", menuName = "IdleTime/Skill Tree")]
    public class SkillTreeDefinition : ScriptableObject
    {
        public PlayerClass playerClass;
        public List<SkillNodeEntry> nodes = new();
    }

    [Serializable]
    public class SkillNodeEntry
    {
        public SkillDefinition skill;
        // Pixel position relative to the top-left of the content area. X goes right, Y goes down (use negative Y to go up).
        public Vector2 position;
        public List<SkillDefinition> prerequisites = new();
    }
}
