using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    [Serializable]
    public class CharacterData
    {
        public string characterName = "Character";
        public PlayerClass playerClass;
        public int level = 1;
        public float currentHP;
        public float currentMP;
        public float currentXP;

        public float XPToNextLevel => 100f * level;

        // All class trees this character has access to (starts with their base class).
        public List<PlayerClass> unlockedClasses = new();

        // Per-character skill state
        public SkillRegistry skills = new();

        // Per-character equipped gear
        public EquipmentSlots equipment = new();

        // ── Skill bonuses (recomputed by SkillManager whenever skills change) ───

        [NonSerialized] public int skillBonusAttack;
        [NonSerialized] public int skillBonusDefense;
        [NonSerialized] public int skillBonusMaxHP;
        [NonSerialized] public int skillBonusMaxMP;
        [NonSerialized] public int skillBonusAccuracy;

        // ── Base stats (class formula) ────────────────────────────────────────

        public float MaxHP => (playerClass != null ? playerClass.baseHP + playerClass.hpPerLevel * (level - 1) : 0) + skillBonusMaxHP + equipBonusMaxHP;
        public float MaxMP => (playerClass != null ? playerClass.baseMP + playerClass.mpPerLevel * (level - 1) : 0) + skillBonusMaxMP;
        public int Str => playerClass != null ? playerClass.baseStr + Mathf.RoundToInt(playerClass.strPerLevel * (level - 1)) : 0;
        public int Dex => playerClass != null ? playerClass.baseDex + Mathf.RoundToInt(playerClass.dexPerLevel * (level - 1)) : 0;
        public int Wis => playerClass != null ? playerClass.baseWis + Mathf.RoundToInt(playerClass.wisPerLevel * (level - 1)) : 0;
        public int Luk => playerClass != null ? playerClass.baseLuk + Mathf.RoundToInt(playerClass.lukPerLevel * (level - 1)) : 0;

        public string ClassName => playerClass != null ? playerClass.className : "None";

        // ── Equipment bonuses ─────────────────────────────────────────────────
        // Not serialized — the equipment system recomputes these whenever gear changes.

        [NonSerialized] public int equipBonusAttack;
        [NonSerialized] public int equipBonusAccuracy;
        [NonSerialized] public int equipBonusDefense;
        [NonSerialized] public int equipBonusMaxHP;

        // ── Derived combat stats ──────────────────────────────────────────────
        // Attack   = class damage-stat + weapon bonus
        // Accuracy = class accuracy-stat + weapon bonus
        // Defense  = armor/skill bonuses (no base contribution yet)

        public int Attack   => GetBaseStat(playerClass?.damageStat   ?? PrimaryStat.Str) + equipBonusAttack   + skillBonusAttack;
        public int Accuracy => GetBaseStat(playerClass?.accuracyStat ?? PrimaryStat.Dex) + equipBonusAccuracy + skillBonusAccuracy;
        public int Defense  => equipBonusDefense + skillBonusDefense;

        public void Initialize()
        {
            if (unlockedClasses.Count == 0 && playerClass != null)
                unlockedClasses.Add(playerClass);
            currentHP = MaxHP;
            currentMP = MaxMP;
        }

        private int GetBaseStat(PrimaryStat stat) => stat switch
        {
            PrimaryStat.Str => Str,
            PrimaryStat.Dex => Dex,
            PrimaryStat.Wis => Wis,
            PrimaryStat.Luk => Luk,
            _               => 0,
        };
    }
}
