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
        public event Action OnClassChanged;
        public event Action OnLevelUp;
        public event Action OnPlayerDeath;
        public event Action OnPlayerRespawn;
        public event Action OnGoldChanged;

        public bool IsDead { get; private set; }

        public CharacterData ActiveCharacter => characters.Count > 0 ? characters[activeIndex] : null;
        public IReadOnlyList<CharacterData> Characters => characters;
        public int ActiveIndex => activeIndex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
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

            // Accrue offline gains now that stats are valid (gathering chance reads STR/WIS).
            save?.ApplyOfflineGains(characters);

            OnActiveCharacterChanged?.Invoke();
        }

        public void NotifyStatsChanged() => OnStatsChanged?.Invoke();

        public bool SetActiveCharacterClass(PlayerClass newClass, bool saveImmediately = true)
        {
            var c = ActiveCharacter;
            if (c == null || newClass == null) return false;

            if (c.playerClass == newClass)
            {
                return false;
            }

            c.playerClass = newClass;
            if (!c.unlockedClasses.Contains(newClass))
            {
                c.unlockedClasses.Add(newClass);
            }

            RecomputeAllBonuses(c);
            c.ClampVitals();

            OnClassChanged?.Invoke();
            OnStatsChanged?.Invoke();

            if (saveImmediately)
            {
                SaveManager.Instance?.SaveCharacter(c, includeLiveInventory: true);
            }

            return true;
        }

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
            float regen = c.MpRegenPerSecond;   // WIS-scaled, from StatFormulas
            c.currentMP = Mathf.Min(c.MaxMP, c.currentMP + regen * deltaTime);

            if (Mathf.RoundToInt(c.currentMP) != before || c.currentMP >= c.MaxMP)
                OnStatsChanged?.Invoke();
        }

        public float MpRegenPerSecond(CharacterData c) => c?.MpRegenPerSecond ?? 0f;

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

        // ── Gold ────────────────────────────────────────────────────────────────
        // Per-character soft currency. Reads/writes ActiveCharacter.gold and fires
        // OnGoldChanged so the HUD can refresh without polling.

        public int Gold => ActiveCharacter?.gold ?? 0;

        public void AddGold(int amount)
        {
            var c = ActiveCharacter;
            if (c == null || amount == 0) return;
            // Clamp at 0 so a negative add can't push the total below empty.
            c.gold = Mathf.Max(0, c.gold + amount);
            OnGoldChanged?.Invoke();
        }

        public bool CanAffordGold(int amount) => ActiveCharacter != null && ActiveCharacter.gold >= amount;

        // Spends nothing and returns false if the active character can't pay, so callers
        // can gate a purchase on it: `if (PlayerManager.Instance.TrySpendGold(price)) { buy... }`.
        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || !CanAffordGold(amount)) return false;
            AddGold(-amount);
            return true;
        }

        public bool CanAffordMP(float amount) => ActiveCharacter != null && ActiveCharacter.currentMP >= amount;

        // The MP drain that future abilities/spells call. Spends nothing and returns
        // false if the active character can't pay, so callers can gate their effect on
        // it: `if (PlayerManager.Instance.TrySpendMP(cost)) { cast... }`.
        public bool TrySpendMP(float amount)
        {
            if (amount < 0f || !CanAffordMP(amount)) return false;
            ModifyMP(-amount);
            return true;
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

            // Luck scales EXP gained (see StatFormulas.XPMultiplier).
            c.currentXP += amount * c.XPMultiplier;

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
