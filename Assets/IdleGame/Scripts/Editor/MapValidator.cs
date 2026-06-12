using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using IdleTime.Interactions;

namespace IdleTime.EditorTools
{
    // Menu IdleTime ▸ Validate Map. Walks every RoomDefinition asset as a tree and logs
    // the authoring mistakes that only bite at playtest: rooms unreachable from a root,
    // back-links that don't match the parent's forward exits, multi-parent nodes (a tree
    // node has one parent), the 3-portal cap, scene names missing from Build Settings,
    // duplicate room ids, and empty/self exits. Warnings ping the offending asset.
    public static class MapValidator
    {
        [MenuItem("IdleTime/Validate Map")]
        static void Validate()
        {
            var rooms = LoadAllRooms();
            if (rooms.Count == 0)
            {
                Debug.Log("[Map] No RoomDefinition assets found — nothing to validate.");
                return;
            }

            var buildScenes = EnabledBuildSceneNames();
            int issues = 0;
            void Warn(string message, Object ctx) { issues++; Debug.LogWarning("[Map] " + message, ctx); }

            // ── Per-room checks ────────────────────────────────────────────────────
            var byId = new Dictionary<string, RoomDefinition>();
            var roots = new List<RoomDefinition>();
            var parentCount = new Dictionary<RoomDefinition, int>();   // forward in-degree

            foreach (var room in rooms)
            {
                // Duplicate room ids share a kill pool unintentionally.
                string id = room.RoomId;
                if (byId.TryGetValue(id, out var clash))
                    Warn($"Duplicate roomId '{id}' on '{room.name}' and '{clash.name}' — they'd share one kill pool.", room);
                else
                    byId[id] = room;

                if (room.back == null) roots.Add(room);

                if (room.PortalCount > 3)
                    Warn($"'{room.name}' implies {room.PortalCount} portals (back + forward) — the design cap is 3 per room.", room);

                // Scene must be loadable.
                if (string.IsNullOrWhiteSpace(room.sceneName))
                    Warn($"'{room.name}' has no sceneName set.", room);
                else if (!buildScenes.Contains(room.sceneName))
                    Warn($"'{room.name}' sceneName '{room.sceneName}' is not an enabled scene in Build Settings.", room);

                // Forward exits: no nulls, no self-edges; tally in-degree of targets.
                if (room.forwardExits != null)
                {
                    foreach (var exit in room.forwardExits)
                    {
                        if (exit == null || exit.target == null)
                        {
                            Warn($"'{room.name}' has a forward exit with no target.", room);
                            continue;
                        }
                        if (exit.target == room)
                            Warn($"'{room.name}' has a forward exit to itself.", room);

                        parentCount.TryGetValue(exit.target, out int n);
                        parentCount[exit.target] = n + 1;
                    }
                }

                // back must be reciprocated by a forward edge on the parent.
                if (room.back != null && !HasForwardExitTo(room.back, room))
                    Warn($"'{room.name}'.back = '{room.back.name}', but '{room.back.name}' has no forward exit back to '{room.name}'.", room);
            }

            // ── Tree-shape checks ──────────────────────────────────────────────────
            foreach (var kv in parentCount)
                if (kv.Value > 1)
                    Warn($"'{kv.Key.name}' is a forward target of {kv.Value} rooms — a tree node has at most one parent.", kv.Key);

            if (roots.Count == 0)
                Warn("No root room — every room has a 'back', so there's no entry point (or there's a cycle).", null);
            else if (roots.Count > 1)
                Debug.Log($"[Map] {roots.Count} root rooms (no 'back'): that's fine if you intend separate maps; one root = one connected tree.");

            // Reachability from the roots via forward exits.
            var reachable = new HashSet<RoomDefinition>();
            foreach (var root in roots) VisitForward(root, reachable);
            foreach (var room in rooms)
                if (!reachable.Contains(room))
                    Warn($"'{room.name}' is unreachable — no forward path from any root reaches it.", room);

            // ── Summary ────────────────────────────────────────────────────────────
            if (issues == 0)
                Debug.Log($"[Map] Validation passed — {rooms.Count} room(s), {roots.Count} root(s), no issues.");
            else
                Debug.LogWarning($"[Map] Validation finished — {rooms.Count} room(s), {issues} issue(s) logged above (click a warning to ping the asset).");
        }

        static List<RoomDefinition> LoadAllRooms()
        {
            var list = new List<RoomDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition"))
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room != null) list.Add(room);
            }
            return list;
        }

        static HashSet<string> EnabledBuildSceneNames()
        {
            var names = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    names.Add(Path.GetFileNameWithoutExtension(scene.path));
            return names;
        }

        static bool HasForwardExitTo(RoomDefinition from, RoomDefinition target)
        {
            if (from.forwardExits == null) return false;
            foreach (var exit in from.forwardExits)
                if (exit != null && exit.target == target) return true;
            return false;
        }

        static void VisitForward(RoomDefinition node, HashSet<RoomDefinition> visited)
        {
            if (node == null || !visited.Add(node)) return;   // Add false = already visited (also stops cycles)
            if (node.forwardExits == null) return;
            foreach (var exit in node.forwardExits)
                if (exit != null) VisitForward(exit.target, visited);
        }
    }
}
