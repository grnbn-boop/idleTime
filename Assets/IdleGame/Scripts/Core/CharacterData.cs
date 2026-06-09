using System;
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

        // ── Base stats (class formula) ────────────────────────────────────────

        public float MaxHP => playerClass != null ? playerClass.baseHP + playerClass.hpPerLevel * (level - 1) : 0;
        public float MaxMP => playerClass != null ? playerClass.baseMP + playerClass.mpPerLevel * (level - 1) : 0;
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

        // ── Derived combat stats ──────────────────────────────────────────────
        // Attack   = class damage-stat + weapon bonus
        // Accuracy = class accuracy-stat + weapon bonus
        // Defense  = armor/skill bonuses (no base contribution yet)

        public int Attack   => GetBaseStat(playerClass?.damageStat   ?? PrimaryStat.Str) + equipBonusAttack;
        public int Accuracy => GetBaseStat(playerClass?.accuracyStat ?? PrimaryStat.Dex) + equipBonusAccuracy;
        public int Defense  => equipBonusDefense;

        public void Initialize()
        {
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
