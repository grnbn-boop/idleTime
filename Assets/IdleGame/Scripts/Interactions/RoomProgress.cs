using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Interactions
{
    // Tracks per-room kill counts. A "room" is keyed by roomId, and every portal in that
    // room reads the SAME count and gates on its own killsRequired — so one room can hold
    // several portals with different thresholds (e.g. an easy exit at 10 kills and a boss
    // gate at 25) that all advance from the one shared total. "Unlocked" is therefore not
    // stored here: it's derived per-portal as kills >= that portal's killsRequired.
    //
    // Why static rather than a field on PortalController: dying reloads the active scene
    // (see DeathSequenceController), which rebuilds every scene object — a counter living
    // on the portal would reset on death. A static store outlives a scene reload.
    //
    // Lifetime: persists for the play session, and is now serialized across sessions too —
    // SaveManager captures it into MasterSaveData and rehydrates it (BeforeSceneLoad) on a
    // cold boot, so "3 kills left" is still 3 kills left after a quit/relaunch.
    public static class RoomProgress
    {
        private static readonly Dictionary<string, int> kills = new();

        // Per-death dedup: several portals in one room all react to the same
        // MonsterController.OnAnyDeath, but the shared total must move once per death, not
        // once per portal. We remember which (room, death) pairs were counted this frame.
        private static int dedupFrame = -1;
        private static readonly HashSet<string> countedThisFrame = new();

        public static int GetKills(string roomId) =>
            !string.IsNullOrEmpty(roomId) && kills.TryGetValue(roomId, out int k) ? k : 0;

        // Unconditional increment (test/debug). Gameplay goes through AddKillOnce.
        public static int AddKill(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return 0;
            int k = GetKills(roomId) + 1;
            kills[roomId] = k;
            return k;
        }

        // Counts a single death toward the room exactly once, even when multiple portals
        // in the room each call it for the same OnAnyDeath. `deathToken` identifies the
        // death (the monster's instance id); a repeat (roomId, token) within the same frame
        // is ignored, so N portals → one increment. Returns the room's new total.
        public static int AddKillOnce(string roomId, int deathToken)
        {
            if (string.IsNullOrEmpty(roomId)) return 0;

            if (Time.frameCount != dedupFrame)
            {
                dedupFrame = Time.frameCount;
                countedThisFrame.Clear();
            }

            return countedThisFrame.Add(roomId + "#" + deathToken)
                ? AddKill(roomId)
                : GetKills(roomId);
        }

        // ── Persistence bridge (SaveManager) ─────────────────────────────────────
        public static IReadOnlyCollection<string> RoomIds => kills.Keys;

        public static void Restore(string roomId, int killCount)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            kills[roomId] = killCount < 0 ? 0 : killCount;
        }

        // For a "new game" / debug reset. Not called automatically by gameplay.
        public static void ResetAll()
        {
            kills.Clear();
            countedThisFrame.Clear();
            dedupFrame = -1;
        }

        public static void Reset(string roomId)
        {
            if (!string.IsNullOrEmpty(roomId)) kills.Remove(roomId);
        }
    }
}
