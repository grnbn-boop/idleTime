using System;
using UnityEngine;

namespace IdleTime.Interactions
{
    // A node in the map TREE. The map is traversed in a direction — forward (deeper) or
    // back (toward the root) — and may branch, but each path stays linear. A room holds:
    //   • one BACK portal to its parent (always open), and
    //   • up to two FORWARD portals to child rooms (each gated by kills),
    // so a room has at most THREE portals. Authored as ScriptableObject assets that
    // reference each other (same data-asset pattern as SkillTreeDefinition / PlayerClass);
    // PortalController reads its gate + destination straight off this tree, so the asset
    // is the single source of truth for the map's shape and difficulty.
    [CreateAssetMenu(fileName = "Room", menuName = "IdleTime/Map/Room Definition")]
    public class RoomDefinition : ScriptableObject
    {
        [Tooltip("Stable progress key — the shared kill pool for this room. Defaults to the asset name if left empty.")]
        [SerializeField] private string roomId;

        [Tooltip("Build-settings scene name to load for this room.")]
        public string sceneName;

        [Tooltip("Friendly name shown on the portal nav HUD.")]
        public string displayName;

        [Tooltip("The room this one branches back to — its BACK portal's destination. Empty = root. The back portal is always open.")]
        public RoomDefinition back;

        [Tooltip("FORWARD exits to child rooms (each is a forward portal, gated by kills). Paths stay linear but may branch; with the back portal, keep a room to at most 3 portals total.")]
        public RoomExit[] forwardExits = Array.Empty<RoomExit>();

        public string RoomId => string.IsNullOrWhiteSpace(roomId) ? name : roomId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RoomId : displayName;

        // Kills this room's shared pool must reach for the portal leading to `dest` to
        // open. The back edge is always open (0). Returns -1 if `dest` isn't a neighbour
        // of this room — i.e. the portal was wired to a room that isn't a child or parent.
        public int KillsToReach(RoomDefinition dest)
        {
            if (dest == null) return -1;
            if (back != null && dest == back) return 0;
            if (forwardExits != null)
                foreach (var exit in forwardExits)
                    if (exit != null && exit.target == dest)
                        return Mathf.Max(0, exit.killsRequired);
            return -1;
        }

        // Total portals this room implies (back + forward), for the 3-portal cap check.
        public int PortalCount => (back != null ? 1 : 0) + (forwardExits != null ? forwardExits.Length : 0);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (PortalCount > 3)
                Debug.LogWarning($"[RoomDefinition] '{name}' implies {PortalCount} portals " +
                                 $"(back + {forwardExits.Length} forward). The design cap is 3 per room.", this);
        }
#endif
    }

    // One forward edge of the tree: which child room, and the room kill count that opens
    // the portal leading to it.
    [Serializable]
    public class RoomExit
    {
        public RoomDefinition target;
        [Min(0)] public int killsRequired = 10;
    }
}
