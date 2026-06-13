using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    // ── AFK / offline-gains system ────────────────────────────────────────────
    // The idea: each character has an assigned ACTIVITY (idle, a gathering skill, or
    // combat). While the game is closed, every character accrues that activity's gains
    // into a CLAIM PILE, capped at MaxOfflineSeconds. On next launch SaveManager diffs
    // the master save's timestamp against "now", fills each pile, and the player collects
    // it (IdleOn-style) — a full bag just leaves items in the pile.
    //
    // This file holds the save-shaped data and the headless accrual maths only. Who SETS
    // the activity (GatheringController today; an assignment UI later) and who COLLECTS a
    // pile (SaveManager.CollectClaim) live elsewhere. Combat accrual is deliberately a
    // stub until the offline kill-rate / loot-table model is designed.

    public enum AfkActivityKind { Idle, Gathering, Combat }

    // What a character is "doing" while away. Gathering snapshots the node's tuning at
    // assignment time so accrual is computable with no scene/node present (an alt parked
    // on mining isn't loaded in any room). Combat fields are reserved for Phase 2.
    [Serializable]
    public class AfkActivity
    {
        public AfkActivityKind kind = AfkActivityKind.Idle;

        // ── Gathering snapshot ──
        public GatheringSkillType gatherSkill;
        public string rewardItemId = "";     // ItemDefinition.name; empty = trains XP only
        public float gatherInterval = 1.5f;  // seconds per attempt (from ResourceNode)
        public float baseSuccessChance = 0.5f;

        // ── Combat snapshot (reserved — Phase 2) ──
        public string combatRoomId = "";

        public bool IsIdle => kind == AfkActivityKind.Idle;

        public void SetIdle()
        {
            kind = AfkActivityKind.Idle;
            rewardItemId = "";
            combatRoomId = "";
        }

        public void SetGathering(GatheringSkillType skill, string rewardId, float interval, float baseChance)
        {
            kind = AfkActivityKind.Gathering;
            gatherSkill = skill;
            rewardItemId = rewardId ?? "";
            gatherInterval = Mathf.Max(0.1f, interval);
            baseSuccessChance = baseChance;
            combatRoomId = "";
        }
    }

    [Serializable]
    public class AfkRewardEntry
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class AfkGatherXpEntry
    {
        public GatheringSkillType skill;
        public float xp;
    }

    // A pending pile of offline gains waiting to be collected. Accrual merges new windows
    // into whatever is already pending (an un-collected pile survives across launches).
    [Serializable]
    public class AfkClaim
    {
        public bool hasPending;
        public long fromUtcTicks;       // start of the earliest un-collected window
        public long toUtcTicks;         // end of the most recent window ("now" at accrual)
        public double secondsAccrued;   // total real seconds folded into this pile (after cap)

        public List<AfkRewardEntry> items = new();
        public int gold;
        public float characterXp;       // reserved for combat (Phase 2)
        public List<AfkGatherXpEntry> gatherXp = new();

        public void AddItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;
            foreach (var e in items)
                if (e.itemId == itemId) { e.count += count; return; }
            items.Add(new AfkRewardEntry { itemId = itemId, count = count });
        }

        public void AddGatherXp(GatheringSkillType skill, float xp)
        {
            if (xp <= 0f) return;
            foreach (var e in gatherXp)
                if (e.skill == skill) { e.xp += xp; return; }
            gatherXp.Add(new AfkGatherXpEntry { skill = skill, xp = xp });
        }

        public void Clear()
        {
            hasPending = false;
            fromUtcTicks = 0;
            toUtcTicks = 0;
            secondsAccrued = 0;
            items.Clear();
            gold = 0;
            characterXp = 0f;
            gatherXp.Clear();
        }
    }

    // Headless accrual: given a character, its activity, and an offline window, fold the
    // expected gains into its claim pile. Uses expected values (attempts × success chance)
    // rather than rolling thousands of RNG attempts, matching the live loop's average.
    public static class AfkProgress
    {
        // 8-hour cap on a single offline window (the player's chosen ceiling).
        public const double MaxOfflineSeconds = 8 * 60 * 60;

        // Offline gains run at full rate for now; lower this to nerf AFK vs active play.
        public const float OfflineEfficiency = 1f;

        // Clamps a raw elapsed span (seconds) to the offline window the game will pay out.
        public static double ClampWindow(double rawSeconds) =>
            Math.Max(0.0, Math.Min(rawSeconds, MaxOfflineSeconds));

        // Accrues `seconds` of `c.activity` into `c.afkClaim`. Returns true if anything was
        // added. `gm` resolves gathering definitions/success chance; falls back to the
        // singleton. Does NOT resolve item assets — the pile stores itemIds, resolved only
        // when the player collects (SaveManager.CollectClaim).
        public static bool Accrue(CharacterData c, double seconds, GatheringManager gm = null)
        {
            if (c == null || c.activity == null || c.afkClaim == null) return false;
            seconds = ClampWindow(seconds);
            if (seconds <= 0.0) return false;

            gm ??= GatheringManager.Instance;
            bool added = c.activity.kind switch
            {
                AfkActivityKind.Gathering => AccrueGathering(c, seconds, gm),
                AfkActivityKind.Combat    => false, // Phase 2: needs an offline kill-rate model
                _                         => false,
            };

            if (!added) return false;

            var claim = c.afkClaim;
            long now = DateTime.UtcNow.Ticks;
            long windowStart = now - (long)(seconds * TimeSpan.TicksPerSecond);
            if (!claim.hasPending) claim.fromUtcTicks = windowStart;
            else claim.fromUtcTicks = Math.Min(claim.fromUtcTicks, windowStart);
            claim.toUtcTicks = now;
            claim.secondsAccrued += seconds;
            claim.hasPending = true;
            return true;
        }

        static bool AccrueGathering(CharacterData c, double seconds, GatheringManager gm)
        {
            var act = c.activity;
            var def = gm != null ? gm.GetDefinition(act.gatherSkill) : null;

            float chance = gm != null
                ? gm.SuccessChance(c, def, act.baseSuccessChance)
                : Mathf.Clamp01(act.baseSuccessChance);

            int attempts = (int)(seconds / Mathf.Max(0.1f, act.gatherInterval));
            int successes = Mathf.RoundToInt(attempts * chance * OfflineEfficiency);
            if (successes <= 0) return false;

            var claim = c.afkClaim;
            if (!string.IsNullOrEmpty(act.rewardItemId))
                claim.AddItem(act.rewardItemId, successes);

            float xpPer = def != null ? def.xpPerGather : 0f;
            if (xpPer > 0f) claim.AddGatherXp(act.gatherSkill, successes * xpPer);

            return true;
        }
    }
}
