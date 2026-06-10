using UnityEngine;

namespace IdleTime.Combat
{
    // Single source of truth for combat formulas: player-to-monster hit chance and
    // damage, and monster-to-player damage mitigation. Tune the constants here (a
    // future CombatConfig ScriptableObject could replace them for designer tuning).
    public static class CombatMath
    {
        // ── To-hit (player → monster) ─────────────────────────────────────────
        public const float MinHitChance   = 0.05f;   // floor at/below minAccuracy
        const float HitCurveExponent      = 0.6f;    // <1 → strong early gains, taper near cap

        // ── Player hit damage ─────────────────────────────────────────────────
        // Crit chance/damage now live in StatFormulas (DEX → chance, STR → damage);
        // PlayerHitDamage takes the resolved values so this stays pure mechanics.
        public const float DamageVariance = 0.15f; // ±15% per hit

        // Hit chance ramps from MinHitChance at/below minAcc to 100% at maxAcc,
        // eased by HitCurveExponent so early accuracy investment pays off fast.
        public static float HitChance(float accuracy, float minAcc, float maxAcc)
        {
            if (accuracy >= maxAcc) return 1f;
            if (maxAcc <= minAcc)   return MinHitChance;   // guard bad data
            float t = Mathf.Clamp01((accuracy - minAcc) / (maxAcc - minAcc));
            return Mathf.Lerp(MinHitChance, 1f, Mathf.Pow(t, HitCurveExponent));
        }

        public static bool RollHit(float accuracy, float minAcc, float maxAcc)
            => Random.value <= HitChance(accuracy, minAcc, maxAcc);

        // Flat Attack ± variance, scaled by a flat damageMultiplier (skill/gear "+% Damage"),
        // then a crit roll. Crit chance (DEX) and crit multiplier (STR) are resolved by the
        // caller via StatFormulas. Always ≥ 1 on a hit.
        public static int PlayerHitDamage(int attack, float damageMultiplier, float critChance, float critMultiplier, out bool crit)
        {
            float dmg = attack * (1f + Random.Range(-DamageVariance, DamageVariance)) * damageMultiplier;
            crit = Random.value < critChance;
            if (crit) dmg *= critMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        // ── Monster hit damage (→ player) ─────────────────────────────────────
        // Linear mitigation: full damage at 0 Defense, scaling to 0 at defenseToNegate.
        public static int MitigatedDamage(int monsterAttack, int playerDefense, int defenseToNegate)
        {
            if (defenseToNegate <= 0) return Mathf.Max(0, monsterAttack);
            float reduction = Mathf.Clamp01((float)playerDefense / defenseToNegate);
            return Mathf.Max(0, Mathf.RoundToInt(monsterAttack * (1f - reduction)));
        }
    }
}
