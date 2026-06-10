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

        [Header("MP Regen")]
        [Tooltip("MP restored per second before Wisdom is factored in.")]
        [SerializeField] float baseMpRegenPerSecond = 0.5f;
        [Tooltip("Extra MP/sec per point of Wisdom.")]
        [SerializeField] float mpRegenPerWis = 0.1f;

        public event Action OnActiveCharacterChanged;
        public event Action OnStatsChanged;
        public event Action OnLevelUp;
        public event Action OnPlayerDeath;
        public event Action OnPlayerRespawn;

        public bool IsDead { get; private set; }

        public CharacterData ActiveCharacter => characters.Count > 0 ? characters[activeIndex] : null;
        public IReadOnlyList<CharacterData> Characters => characters;
        public int ActiveIndex => activeIndex;

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
            // Apply save files (if any) onto the Inspector-authored characters first,
            // so the recompute below derives stats from the loaded gear/skills.
            var save = SaveManager.Instance;
            if (save != null)
            {
                int savedIndex = save.LoadMasterActiveIndex();
                if (savedIndex >= 0 && savedIndex < characters.Count)
                    activeIndex = savedIndex;
                save.LoadCharacters(characters, activeIndex);
            }

            foreach (var c in characters)
            {
                c.EnsureBaseClassUnlocked();   // skill trees are gated on unlockedClasses
                RecomputeAllBonuses(c);
                c.ResetVitals();               // fill HP/MP from the now-correct maxima
            }

            OnActiveCharacterChanged?.Invoke();
        }

        public void NotifyStatsChanged() => OnStatsChanged?.Invoke();

        void Update()
        {
            TickMpRegen(Time.deltaTime);
        }

        // Passive MP regen for the active character, scaled by Wisdom. To avoid
        // refreshing the UI every frame, only fire OnStatsChanged when the displayed
        // (rounded) MP actually changes.
        void TickMpRegen(float deltaTime)
        {
            if (IsDead) return;
            var c = ActiveCharacter;
            if (c == null || c.currentMP >= c.MaxMP) return;

            int before = Mathf.RoundToInt(c.currentMP);
            float regen = baseMpRegenPerSecond + c.Wis * mpRegenPerWis;
            c.currentMP = Mathf.Min(c.MaxMP, c.currentMP + regen * deltaTime);

            if (Mathf.RoundToInt(c.currentMP) != before || c.currentMP >= c.MaxMP)
                OnStatsChanged?.Invoke();
        }

        public float MpRegenPerSecond(CharacterData c) =>
            c == null ? 0f : baseMpRegenPerSecond + c.Wis * mpRegenPerWis;

        public void SwitchCharacter(int index)
        {
            if (index < 0 || index >= characters.Count) return;

            // The shared Inventory holds the outgoing character's items — persist
            // them to that character's file, then pull the incoming character's
            // inventory from theirs.
            SaveManager.Instance?.SaveCharacter(ActiveCharacter, includeLiveInventory: true);
            activeIndex = index;
            SaveManager.Instance?.LoadInventoryFor(ActiveCharacter);

            RecomputeAllBonuses(ActiveCharacter);
            OnActiveCharacterChanged?.Invoke();
        }

        // skillBonus*/equipBonus* are [NonSerialized] caches, and gear/skill levels
        // can be authored in the Inspector (or later loaded from a save) without ever
        // passing through Equip/TryUnlock — so the caches must be rebuilt here before
        // anything reads a derived stat.
        private void RecomputeAllBonuses(CharacterData c)
        {
            EquipmentManager.Instance?.RecomputeBonuses(c);
            SkillManager.Instance?.RecomputeSkillBonuses(c);
        }

        public void ModifyHP(float delta)
        {
            if (ActiveCharacter == null) return;
            ActiveCharacter.currentHP = Mathf.Clamp(ActiveCharacter.currentHP + delta, 0, ActiveCharacter.MaxHP);
            OnStatsChanged?.Invoke();
            CheckForDeath();
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
            CheckForDeath();
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

        // ── Death & respawn ─────────────────────────────────────────────────────
        // PlayerManager only owns the *state* (IsDead) and the events. The timed
        // cinematic — death animation, fade, level reset — is driven by
        // DeathSequenceController, which calls Respawn() when it's ready.

        void CheckForDeath()
        {
            if (IsDead || ActiveCharacter == null) return;
            if (ActiveCharacter.currentHP > 0f) return;

            IsDead = true;
            OnPlayerDeath?.Invoke();
        }

        // Restores the active character to full vitals and lifts the dead state.
        // Called by DeathSequenceController after the level has been reset; also
        // safe to call directly for a checkpoint/town-heal.
        public void Respawn()
        {
            if (!IsDead) return;
            IsDead = false;

            ActiveCharacter?.ResetVitals();
            OnPlayerRespawn?.Invoke();
            OnStatsChanged?.Invoke();
        }
    }
}
