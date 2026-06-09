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

        public float MaxHP => playerClass != null ? playerClass.baseHP + playerClass.hpPerLevel * (level - 1) : 0;
        public float MaxMP => playerClass != null ? playerClass.baseMP + playerClass.mpPerLevel * (level - 1) : 0;
        public int Str => playerClass != null ? playerClass.baseStr + Mathf.RoundToInt(playerClass.strPerLevel * (level - 1)) : 0;
        public int Agi => playerClass != null ? playerClass.baseAgi + Mathf.RoundToInt(playerClass.agiPerLevel * (level - 1)) : 0;
        public int Wis => playerClass != null ? playerClass.baseWis + Mathf.RoundToInt(playerClass.wisPerLevel * (level - 1)) : 0;
        public int Luk => playerClass != null ? playerClass.baseLuk + Mathf.RoundToInt(playerClass.lukPerLevel * (level - 1)) : 0;

        public string ClassName => playerClass != null ? playerClass.className : "None";

        public void Initialize()
        {
            currentHP = MaxHP;
            currentMP = MaxMP;
        }
    }
}
