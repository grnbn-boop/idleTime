using System;
using System.Collections.Generic;
using UnityEngine;
using IdleTime.Combat;

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

        // Soft currency. The single source of truth for "how much gold the player has";
        // coin pickups (see ItemDefinition.currencyValue) just add to this number.
        public int gold;

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
        [NonSerialized] public int skillBonusStr;
        [NonSerialized] public int skillBonusDex;
        [NonSerialized] public int skillBonusWis;
        [NonSerialized] public int skillBonusLuk;

        // Percentage buckets (fractions: 0.1 = +10%). Percent-of-base multipliers plus
        // the additive percentages that feed the stat-derived formulas in StatFormulas.
        [NonSerialized] public float skillBonusMaxHPPercent;
        [NonSerialized] public float skillBonusAttackPercent;
        [NonSerialized] public float skillBonusDefensePercent;
        [NonSerialized] public float skillBonusCritChance;
        [NonSerialized] public float skillBonusCritDamage;
        [NonSerialized] public float skillBonusMoveSpeed;
        [NonSerialized] public float skillBonusDropRate;
        [NonSerialized] public float skillBonusXPGain;
        [NonSerialized] public float skillBonusBossDamage;
        [NonSerialized] public float skillBonusMpRegen;
        [NonSerialized] public float skillBonusDamage;

        // Weapon Power skill bonuses: flat adds to the weapon's base power, percent scales it.
        [NonSerialized] public int skillBonusWeaponPower;
        [NonSerialized] public float skillBonusWeaponPowerPercent;

        // ── Base stats (class formula) ────────────────────────────────────────
        // STR adds flat Max HP on top of the class formula, then the percent bucket scales it.

        public float MaxHP =>
            ((playerClass != null ? playerClass.baseHP + playerClass.hpPerLevel * (level - 1) : 0)
             + StatFormulas.MaxHpFromStr(Str) + skillBonusMaxHP + equipBonusMaxHP)
            * (1f + skillBonusMaxHPPercent + equipBonusMaxHPPercent);
        public float MaxMP => (playerClass != null ? playerClass.baseMP + playerClass.mpPerLevel * (level - 1) : 0) + skillBonusMaxMP;
        public int Str => BaseStr + skillBonusStr + equipBonusStr;
        public int Dex => BaseDex + skillBonusDex + equipBonusDex;
        public int Wis => BaseWis + skillBonusWis + equipBonusWis;
        public int Luk => BaseLuk + skillBonusLuk + equipBonusLuk;

        // Class-formula contribution only (no skill/gear bonuses) — the "base" line in stat breakdowns.
        public int BaseStr => playerClass != null ? playerClass.baseStr + Mathf.RoundToInt(playerClass.strPerLevel * (level - 1)) : 0;
        public int BaseDex => playerClass != null ? playerClass.baseDex + Mathf.RoundToInt(playerClass.dexPerLevel * (level - 1)) : 0;
        public int BaseWis => playerClass != null ? playerClass.baseWis + Mathf.RoundToInt(playerClass.wisPerLevel * (level - 1)) : 0;
        public int BaseLuk => playerClass != null ? playerClass.baseLuk + Mathf.RoundToInt(playerClass.lukPerLevel * (level - 1)) : 0;

        public string ClassName => playerClass != null ? playerClass.className : "None";

        // ── Equipment bonuses ─────────────────────────────────────────────────
        // Not serialized — the equipment system recomputes these whenever gear changes.

        [NonSerialized] public int equipBonusAttack;
        [NonSerialized] public int equipBonusAccuracy;
        [NonSerialized] public int equipBonusDefense;
        [NonSerialized] public int equipBonusMaxHP;
        [NonSerialized] public int equipBonusStr;
        [NonSerialized] public int equipBonusDex;
        [NonSerialized] public int equipBonusWis;
        [NonSerialized] public int equipBonusLuk;

        // Percentage buckets from gear (fractions). Mirror the skill buckets so gear can
        // grant percent bonuses too; EquipmentManager resets/sums these.
        [NonSerialized] public float equipBonusMaxHPPercent;
        [NonSerialized] public float equipBonusAttackPercent;
        [NonSerialized] public float equipBonusDefensePercent;
        [NonSerialized] public float equipBonusCritChance;
        [NonSerialized] public float equipBonusCritDamage;
        [NonSerialized] public float equipBonusMoveSpeed;
        [NonSerialized] public float equipBonusDropRate;
        [NonSerialized] public float equipBonusXPGain;
        [NonSerialized] public float equipBonusBossDamage;
        [NonSerialized] public float equipBonusMpRegen;
        [NonSerialized] public float equipBonusDamage;

        // Weapon Power from gear: equipWeaponPower is the summed base power of equipped
        // weapon(s); the bonus buckets let non-weapon gear add flat/percent on top.
        [NonSerialized] public int equipWeaponPower;
        [NonSerialized] public int equipBonusWeaponPower;
        [NonSerialized] public float equipBonusWeaponPowerPercent;

        // ── Derived combat stats ──────────────────────────────────────────────
        // Attack   = (class damage-stat + weapon bonus) × percent bucket
        // Accuracy = class accuracy-stat + weapon bonus
        // Defense  = (armor/skill bonuses) × percent bucket

        public int Attack   => Mathf.RoundToInt((GetBaseStat(playerClass?.damageStat ?? PrimaryStat.Str) + equipBonusAttack + skillBonusAttack)
                                                * (1f + skillBonusAttackPercent + equipBonusAttackPercent));
        public int Accuracy => GetBaseStat(playerClass?.accuracyStat ?? PrimaryStat.Dex) + equipBonusAccuracy + skillBonusAccuracy;
        public int Defense  => Mathf.RoundToInt((equipBonusDefense + skillBonusDefense)
                                                * (1f + skillBonusDefensePercent + equipBonusDefensePercent));

        // ── Stat-derived gameplay modifiers (STR/DEX/WIS/LUK → effects) ────────
        // Each reads its primary stat plus the matching additive percent bucket.

        public float CritChance          => StatFormulas.CritChance(Dex, skillBonusCritChance + equipBonusCritChance);
        public float CritDamageMultiplier => StatFormulas.CritDamageMultiplier(Str, skillBonusCritDamage + equipBonusCritDamage);
        public float MoveSpeedMultiplier  => StatFormulas.MoveSpeedMultiplier(Dex, skillBonusMoveSpeed + equipBonusMoveSpeed);
        public float DropRateMultiplier   => StatFormulas.DropRateMultiplier(Luk, skillBonusDropRate + equipBonusDropRate);
        public float XPMultiplier         => StatFormulas.XPMultiplier(Luk, skillBonusXPGain + equipBonusXPGain);
        public float BossDamageMultiplier => StatFormulas.BossDamageMultiplier(Wis, skillBonusBossDamage + equipBonusBossDamage);
        public float MpRegenPerSecond     => StatFormulas.MpRegenPerSecond(Wis, skillBonusMpRegen + equipBonusMpRegen);

        // Flat multiplier on all outgoing hit damage (skills/gear, not tied to a primary stat).
        public float DamageMultiplier     => Mathf.Max(0f, 1f + skillBonusDamage + equipBonusDamage);

        // Weapon Power = equipped weapon base (+ flat skill/gear bonuses), scaled by the
        // percent buckets. Feeds the hit-damage base alongside Attack. Never negative.
        public int WeaponPower => Mathf.RoundToInt(
            Mathf.Max(0, equipWeaponPower + skillBonusWeaponPower + equipBonusWeaponPower)
            * (1f + skillBonusWeaponPowerPercent + equipBonusWeaponPowerPercent));

        // Base value fed into the hit-damage formula before variance/crit/boss:
        // stat-driven Attack plus the equipped weapon's (modified) power.
        public int HitDamageBase => Attack + WeaponPower;

        public void EnsureBaseClassUnlocked()
        {
            if (unlockedClasses.Count == 0 && playerClass != null)
                unlockedClasses.Add(playerClass);
        }

        // Call only after the skill/equip bonus caches have been recomputed —
        // MaxHP/MaxMP read those caches, so filling vitals earlier bakes in
        // un-bonused maxima.
        public void ResetVitals()
        {
            currentHP = MaxHP;
            currentMP = MaxMP;
        }

        // Pull live vitals back into [0, max]. Call after anything that can lower
        // MaxHP/MaxMP (e.g. unequipping a +MaxHP item) so currentHP can't linger
        // above the new max.
        public void ClampVitals()
        {
            currentHP = Mathf.Clamp(currentHP, 0, MaxHP);
            currentMP = Mathf.Clamp(currentMP, 0, MaxMP);
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
