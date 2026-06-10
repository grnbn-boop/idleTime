using UnityEngine;

namespace IdleTime.Combat
{
    // Single source of truth for turning a primary stat (plus any skill/gear percent
    // bonus) into a derived gameplay value. CombatMath owns the hit/damage *mechanics*;
    // StatFormulas owns the stat → modifier *conversions*. Tune the constants here (a
    // future StatConfig ScriptableObject could replace them for designer tuning).
    //
    // Stat → effect mapping:
    //   STR → Max HP, Crit Damage
    //   DEX → Crit Chance, Movement Speed
    //   WIS → Mana Regen, Boss Damage
    //   LUK → Drop Rate, EXP Gain (hook for other RNG)
    //
    // "bonusPct" arguments are additive fractions sourced from skills/gear (0.05 = +5%).
    public static class StatFormulas
    {
        // ── STR ───────────────────────────────────────────────────────────────
        public const float HPPerStr         = 5f;     // flat Max HP per point of STR
        public const float CritDamagePerStr = 0.01f;  // +1% crit damage per STR (over base)
        public const float BaseCritMultiplier = 1.5f; // crit damage with 0 STR / no bonus

        // ── DEX ───────────────────────────────────────────────────────────────
        public const float CritChancePerDex = 0.005f; // +0.5% crit chance per DEX
        public const float MaxCritChance     = 0.75f;  // crit chance cap
        public const float MoveSpeedPerDex   = 0.01f;  // +1% movement speed per DEX

        // ── WIS ───────────────────────────────────────────────────────────────
        public const float BaseMpRegenPerSecond = 0.5f; // MP/sec before WIS
        public const float MpRegenPerWis         = 0.1f; // extra MP/sec per WIS
        public const float BossDamagePerWis      = 0.01f; // +1% damage vs bosses per WIS

        // ── LUK ───────────────────────────────────────────────────────────────
        public const float DropRatePerLuk   = 0.005f; // +0.5% drop rate per LUK
        public const float MaxDropRateBonus = 2f;     // drop-rate bonus cap (+200%)
        public const float XPGainPerLuk     = 0.005f; // +0.5% XP gain per LUK

        // ── STR derived ───────────────────────────────────────────────────────
        public static float MaxHpFromStr(int str) => str * HPPerStr;

        public static float CritDamageMultiplier(int str, float bonusPct) =>
            BaseCritMultiplier + str * CritDamagePerStr + bonusPct;

        // ── DEX derived ───────────────────────────────────────────────────────
        public static float CritChance(int dex, float bonusPct) =>
            Mathf.Clamp(dex * CritChancePerDex + bonusPct, 0f, MaxCritChance);

        public static float MoveSpeedMultiplier(int dex, float bonusPct) =>
            Mathf.Max(0f, 1f + dex * MoveSpeedPerDex + bonusPct);

        // ── WIS derived ───────────────────────────────────────────────────────
        public static float MpRegenPerSecond(int wis, float bonusPct) =>
            (BaseMpRegenPerSecond + wis * MpRegenPerWis) * (1f + bonusPct);

        public static float BossDamageMultiplier(int wis, float bonusPct) =>
            1f + wis * BossDamagePerWis + bonusPct;

        // ── LUK derived ───────────────────────────────────────────────────────
        public static float DropRateMultiplier(int luk, float bonusPct) =>
            1f + Mathf.Min(MaxDropRateBonus, luk * DropRatePerLuk) + bonusPct;

        public static float XPMultiplier(int luk, float bonusPct) =>
            Mathf.Max(0f, 1f + luk * XPGainPerLuk + bonusPct);
    }
}
