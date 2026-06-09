using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        [SerializeField] List<CharacterData> characters = new();
        [SerializeField] int activeIndex = 0;

        public event Action OnActiveCharacterChanged;
        public event Action OnStatsChanged;
        public event Action OnLevelUp;

        public CharacterData ActiveCharacter => characters.Count > 0 ? characters[activeIndex] : null;
        public IReadOnlyList<CharacterData> Characters => characters;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            foreach (var c in characters)
                c.Initialize();

            OnActiveCharacterChanged?.Invoke();
        }

        public void SwitchCharacter(int index)
        {
            if (index < 0 || index >= characters.Count) return;
            activeIndex = index;
            OnActiveCharacterChanged?.Invoke();
        }

        public void ModifyHP(float delta)
        {
            if (ActiveCharacter == null) return;
            ActiveCharacter.currentHP = Mathf.Clamp(ActiveCharacter.currentHP + delta, 0, ActiveCharacter.MaxHP);
            OnStatsChanged?.Invoke();
        }

        public void ModifyMP(float delta)
        {
            if (ActiveCharacter == null) return;
            ActiveCharacter.currentMP = Mathf.Clamp(ActiveCharacter.currentMP + delta, 0, ActiveCharacter.MaxMP);
            OnStatsChanged?.Invoke();
        }

        public void SetHP(float value)
        {
            if (ActiveCharacter == null) return;
            ActiveCharacter.currentHP = Mathf.Clamp(value, 0, ActiveCharacter.MaxHP);
            OnStatsChanged?.Invoke();
        }

        public void SetMP(float value)
        {
            if (ActiveCharacter == null) return;
            ActiveCharacter.currentMP = Mathf.Clamp(value, 0, ActiveCharacter.MaxMP);
            OnStatsChanged?.Invoke();
        }

        public void GainXP(float amount)
        {
            var c = ActiveCharacter;
            if (c == null) return;

            c.currentXP += amount;

            bool leveledUp = false;
            while (c.currentXP >= c.XPToNextLevel)
            {
                c.currentXP -= c.XPToNextLevel;
                c.level++;
                SkillManager.Instance?.GainSkillPoints(c, 1);
                leveledUp = true;
            }

            if (leveledUp) OnLevelUp?.Invoke();
            OnStatsChanged?.Invoke();
        }
    }
}
