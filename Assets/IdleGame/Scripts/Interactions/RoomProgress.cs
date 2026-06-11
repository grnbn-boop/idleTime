using System.Collections.Generic;

namespace IdleTime.Interactions
{
    // Tracks per-room kill counts and whether each room's portal has been unlocked.
    //
    // Why static rather than a field on PortalController: dying reloads the active
    // scene (see DeathSequenceController), which rebuilds every scene object — a
    // counter living on the portal would reset on death. A static store outlives a
    // scene reload, so a portal you unlocked stays unlocked when you respawn.
    //
    // Lifetime: persists for the play session (cleared on play-stop / domain reload),
    // which matches the in-memory nature of the rest of the prototype. When you wire
    // SaveManager, serialize this dictionary in SaveData and rehydrate it on load —
    // see Save/load seam note in PortalController.
    public static class RoomProgress
    {
        private class State
        {
            public int kills;
            public bool unlocked;
        }

        private static readonly Dictionary<string, State> rooms = new();

        private static State Get(string roomId)
        {
            if (!rooms.TryGetValue(roomId, out State s))
            {
                s = new State();
                rooms[roomId] = s;
            }
            return s;
        }

        public static int GetKills(string roomId) => Get(roomId).kills;

        public static bool IsUnlocked(string roomId) => Get(roomId).unlocked;

        // Records a kill and returns the new total for the room.
        public static int AddKill(string roomId)
        {
            State s = Get(roomId);
            s.kills++;
            return s.kills;
        }

        public static void SetUnlocked(string roomId, bool unlocked = true) => Get(roomId).unlocked = unlocked;

        // For a "new game" / debug reset. Not called automatically.
        public static void ResetAll() => rooms.Clear();

        public static void Reset(string roomId) => rooms.Remove(roomId);
    }
}
