using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using IdleTime.Core;
using IdleTime.Player;
using IdleTime.CameraRig;
using IdleTime.Interactions;

namespace IdleTime.EditorTools
{
    // Menu IdleTime ▸ Check Scene Rig. Validates the OPEN scene against the brain/body
    // split (see SYSTEMS_WALKTHROUGH §6½):
    //   • BRAIN — the global manager singletons. They come from the bootstrapped
    //             GameSystems prefab (Resources/GameSystems) at runtime, so a room scene
    //             should NOT contain them. Anything found here is an authored leftover to
    //             remove. We instead validate the prefab itself carries the full brain.
    //   • BODY  — the per-room pieces a scene must place for itself: Player, Camera, the
    //             UI managers that reference scene UI (UIManager, ItemDragManager), the
    //             PlayerStatsUI banner, and the EventSystem. PortalNavHUD is omitted on
    //             purpose — it auto-creates.
    //
    // On top of presence it validates that the pieces are actually WIRED, so a room fails
    // the check here instead of silently at playtest:
    //   • UI    — UIManager's overlay refs assigned, the toggle buttons it wires by name
    //             present, ItemDragManager.rootCanvas set, and a live GraphicRaycaster.
    //   • MAP   — a Grid carrying Ground + Secondary + Ladder tilemaps (Background optional).
    //   • PORTS — any PortalController has room + destination set, all portals share one
    //             room identity, and each destination scene is enabled in Build Settings.
    public static class SceneRigChecker
    {
        const string RigResourcePath = "GameSystems";   // Resources/GameSystems.prefab

        // Global singletons supplied by the bootstrapped GameSystems prefab.
        static readonly (System.Type type, string label)[] Brain =
        {
            (typeof(PlayerManager), "PlayerManager"),
            (typeof(SaveManager), "SaveManager"),
            (typeof(Inventory), "Inventory"),
            (typeof(EquipmentManager), "EquipmentManager"),
            (typeof(SkillManager), "SkillManager"),
            (typeof(IdleTime.UI.TooltipManager), "TooltipManager"),
            (typeof(ScreenFader), "ScreenFader"),
            (typeof(DeathSequenceController), "DeathSequenceController"),
        };

        // Per-room pieces the scene must place for itself (exactly one each). UIManager and
        // ItemDragManager live here, not in the brain, because both hold references to the
        // scene's own UI (overlays / root canvas) and must rebuild per room.
        static readonly (System.Type type, string label)[] Body =
        {
            (typeof(ClickToMove2D), "Player (ClickToMove2D)"),
            (typeof(CameraFollow2D), "Camera (CameraFollow2D)"),
            (typeof(UIManager), "UIManager"),
            (typeof(IdleTime.UI.ItemDragManager), "ItemDragManager"),
            (typeof(PlayerStatsUI), "PlayerStatsUI banner"),
            (typeof(EventSystem), "EventSystem"),
        };

        [MenuItem("IdleTime/Check Scene Rig")]
        static void Check() => ReportRig();

        // Logs a full pass/fail report for the active scene. Public so the room builder can
        // run it on a freshly stamped room.
        public static void ReportRig()
        {
            int problems = 0;

            // BRAIN: should be bootstrapped, not authored into the room.
            foreach (var (type, label) in Brain)
            {
                int count = CountInScene(type);
                if (count > 0)
                {
                    problems++;
                    Debug.LogWarning($"[Rig] '{label}' is in this scene ({count}×) — it should come from the " +
                                     "bootstrapped GameSystems prefab, not the room. Remove it from the scene.");
                }
            }

            // BODY: must be present exactly once.
            foreach (var (type, label) in Body)
            {
                int count = CountInScene(type);
                if (count == 0) { problems++; Debug.LogWarning($"[Rig] Missing body piece: {label}."); }
                else if (count > 1) { problems++; Debug.LogWarning($"[Rig] {count}× {label} in this scene — expected exactly 1."); }
            }

            problems += ValidateRigPrefab();
            problems += ValidateUiWiring();
            problems += ValidateTilemaps();
            problems += ValidatePortals();
            problems += CheckLadderSetup();

            string scene = SceneManager.GetActiveScene().name;
            if (problems == 0)
                Debug.Log($"[Rig] '{scene}' matches the room model — scene body present, no stray brain, GameSystems prefab valid. Good to Play.");
            else
                Debug.LogWarning($"[Rig] '{scene}': {problems} issue(s) — see warnings above.");
        }

        static int CountInScene(System.Type type) =>
            Object.FindObjectsByType(type, FindObjectsInactive.Include).Length;

        // GameBootstrap spawns Resources/GameSystems at launch, so make sure it exists and
        // carries the full brain — otherwise rooms boot without their managers.
        static int ValidateRigPrefab()
        {
            var prefab = Resources.Load<GameObject>(RigResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Rig] No '{RigResourcePath}' prefab under a Resources/ folder — GameBootstrap can't " +
                                 "spawn the managers. Make GameSystems a prefab at Resources/GameSystems.prefab.");
                return 1;
            }

            int missing = 0;
            foreach (var (type, label) in Brain)
            {
                if (prefab.GetComponentInChildren(type, true) == null)
                {
                    missing++;
                    Debug.LogWarning($"[Rig] GameSystems prefab is missing '{label}' — add it so the bootstrapped brain is complete.");
                }
            }
            return missing;
        }

        // ── UI wiring ──────────────────────────────────────────────────────────────
        // Catches the class of bug where a click reaches a button but nothing happens:
        // unassigned overlay references, missing toggle buttons (UIManager wires them by
        // name), an unassigned drag canvas, or no raycaster to receive clicks at all.
        static int ValidateUiWiring()
        {
            int issues = 0;

            var ui = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                // inventory/equip/stat are dereferenced unconditionally in UIManager.Awake →
                // required; skills is null-guarded → optional.
                foreach (var (field, label, required) in new (string, string, bool)[]
                {
                    ("inventoryOverlay", "Inventory overlay", true),
                    ("equipOverlay",     "Equipment overlay", true),
                    ("statOverlay",      "Stats overlay",     true),
                    ("skillOverlay",     "Skills overlay",    false),
                })
                {
                    var prop = so.FindProperty(field);
                    if (prop == null) continue;   // field renamed in code → skip rather than false-warn
                    if (required && prop.objectReferenceValue == null)
                    {
                        Debug.LogWarning($"[Rig] UIManager.{field} ({label}) is unassigned — its toggle would NullReference / do nothing.", ui);
                        issues++;
                    }
                }

                // UIManager wires these toggles by GameObject name at runtime, so a missing or
                // renamed button silently breaks that overlay (exactly the drag-in prefab bug).
                foreach (var name in new[]
                {
                    UIManager.InventoryButtonName, UIManager.EquipmentButtonName,
                    UIManager.SkillsButtonName,    UIManager.StatsButtonName,
                })
                {
                    if (FindButtonByName(name) == null)
                    {
                        Debug.LogWarning($"[Rig] No UI Button named '{name}' in the scene — UIManager wires its toggles by name, so that overlay can't be opened.");
                        issues++;
                    }
                }
            }

            // ItemDragManager logs an error and aborts the drag if rootCanvas is null.
            var drag = Object.FindAnyObjectByType<IdleTime.UI.ItemDragManager>(FindObjectsInactive.Include);
            if (drag != null)
            {
                var prop = new SerializedObject(drag).FindProperty("rootCanvas");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    Debug.LogWarning("[Rig] ItemDragManager.rootCanvas is unassigned — item drags will error at runtime.", drag);
                    issues++;
                }
            }

            // Something must be able to raycast UI clicks, or no button works.
            bool anyRaycaster = false;
            foreach (var gr in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include))
                if (gr.enabled && gr.gameObject.activeInHierarchy) { anyRaycaster = true; break; }
            if (!anyRaycaster)
            {
                Debug.LogWarning("[Rig] No active GraphicRaycaster on any Canvas — UI buttons can't receive clicks.");
                issues++;
            }

            return issues;
        }

        static Button FindButtonByName(string name)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
                if (b.name == name) return b;
            return null;
        }

        // ── Tilemap layers ───────────────────────────────────────────────────────────
        // Every room needs a Grid carrying its layers. Ground/Secondary/Ladder are required;
        // Background is supported-if-present. Ground is matched by name OR by being a
        // collidable non-ladder tilemap; Ladder by its physics layer; the rest by name.
        static int ValidateTilemaps()
        {
            var grid = Object.FindAnyObjectByType<Grid>(FindObjectsInactive.Include);
            if (grid == null)
            {
                Debug.LogWarning("[Rig] No Grid in the scene — a room needs a Grid with its tilemap layers (Ground, Secondary, Ladder, optional Background).");
                return 1;
            }

            var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
            if (tilemaps.Length == 0)
            {
                Debug.LogWarning("[Rig] The Grid has no Tilemaps — add the room's terrain and layers.", grid);
                return 1;
            }

            int ladderLayer = LayerMask.NameToLayer("Ladder");
            bool hasGround = false, hasSecondary = false, hasBackground = false, hasLadder = false;

            foreach (var tm in tilemaps)
            {
                string n = tm.name.ToLowerInvariant();
                bool onLadderLayer = ladderLayer >= 0 && tm.gameObject.layer == ladderLayer;

                if (onLadderLayer) hasLadder = true;
                else if (n.Contains("background") || n.Contains("backdrop")) hasBackground = true;
                else if (n.Contains("secondary") || n.Contains("detail") || n.Contains("foreground")) hasSecondary = true;
                else if (n.Contains("ground") || n.Contains("terrain") || n.Contains("world") || tm.TryGetComponent<TilemapCollider2D>(out _)) hasGround = true;
            }

            int issues = 0;
            if (!hasGround)
            {
                Debug.LogWarning("[Rig] No Ground/Terrain tilemap (a collidable non-ladder tilemap, or one named Ground/Terrain) — the player has nothing to stand on.", grid);
                issues++;
            }
            if (!hasSecondary)
            {
                Debug.LogWarning("[Rig] No Secondary tilemap — add the secondary detail layer (name a tilemap 'Secondary').", grid);
                issues++;
            }
            if (!hasLadder)
            {
                Debug.LogWarning("[Rig] No Ladder tilemap (a tilemap on the 'Ladder' layer) — add the ladder layer.", grid);
                issues++;
            }
            if (!hasBackground)
                Debug.Log("[Rig] (info) No Background tilemap — optional. Add one named 'Background' if the room needs a backdrop.");

            return issues;
        }

        // ── Portals ────────────────────────────────────────────────────────────────
        // Portals are optional per room, but any that exist must be fully wired: room +
        // destination set (CLAUDE.md rule), all portals share one room identity, and each
        // destination targets a scene that's actually loadable.
        static int ValidatePortals()
        {
            var portals = Object.FindObjectsByType<PortalController>(FindObjectsInactive.Include);
            if (portals.Length == 0) return 0;   // leaf/dead-end rooms are allowed

            var buildScenes = EnabledBuildSceneNames();
            var distinctRooms = new HashSet<RoomDefinition>();
            int issues = 0;

            foreach (var portal in portals)
            {
                var so = new SerializedObject(portal);
                var room = so.FindProperty("room")?.objectReferenceValue as RoomDefinition;
                var dest = so.FindProperty("destination")?.objectReferenceValue as RoomDefinition;

                if (room == null)
                {
                    Debug.LogWarning($"[Rig] Portal '{portal.name}' has no 'room' assigned — it can't resolve its kill pool / identity.", portal);
                    issues++;
                }
                else distinctRooms.Add(room);

                if (dest == null)
                {
                    Debug.LogWarning($"[Rig] Portal '{portal.name}' has no 'destination' assigned — it leads nowhere.", portal);
                    issues++;
                }
                else if (!string.IsNullOrWhiteSpace(dest.sceneName) && !buildScenes.Contains(dest.sceneName))
                {
                    Debug.LogWarning($"[Rig] Portal '{portal.name}' → '{dest.name}' targets scene '{dest.sceneName}', which isn't enabled in Build Settings — it can't load at runtime.", portal);
                    issues++;
                }
            }

            if (distinctRooms.Count > 1)
            {
                Debug.LogWarning($"[Rig] Portals here reference {distinctRooms.Count} different rooms — a room scene should have one identity (all portals share the same 'room').");
                issues++;
            }

            return issues;
        }

        static HashSet<string> EnabledBuildSceneNames()
        {
            var names = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    names.Add(Path.GetFileNameWithoutExtension(scene.path));
            return names;
        }

        // The one silent footgun in the ladder setup: if the player's terrainMask still
        // includes the ladder tilemap's layer, the ground/wall casts treat ladders as
        // solid and walk-through breaks with no error. Surface it before Play.
        static int CheckLadderSetup()
        {
            var player = Object.FindAnyObjectByType<ClickToMove2D>(FindObjectsInactive.Include);
            if (player == null) return 0;   // no player; the rig check above already flagged it

            int ladderLayer = LayerMask.NameToLayer("Ladder");

            // Only meaningful if there's actually a ladder tilemap in the scene.
            Tilemap ladderTilemap = null;
            foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
            {
                if (ladderLayer >= 0 && tilemap.gameObject.layer == ladderLayer)
                {
                    ladderTilemap = tilemap;
                    break;
                }
            }

            if (ladderTilemap == null)
                return 0;   // no ladders in this scene — nothing to check

            int issues = 0;

            // Player's terrainMask must exclude the ladder layer or walk-through breaks.
            int playerMask = ReadMask(player, "terrainMask");
            if (playerMask != 0 && (playerMask & (1 << ladderLayer)) != 0)
            {
                Debug.LogWarning($"[Rig] Player's Terrain Mask includes the 'Ladder' layer — ladders will block walk-through. " +
                                 $"Uncheck 'Ladder' in {player.name}'s Terrain Mask.", player);
                issues++;
            }

            // The nav graph's terrainMask must ALSO exclude the ladder layer, or ladder
            // tiles get scanned as walkable ground and the routing graph is wrong.
            var nav = Object.FindAnyObjectByType<IdleTime.Navigation.TileNavGraph>(FindObjectsInactive.Include);
            if (nav != null)
            {
                int navMask = ReadMask(nav, "terrainMask");
                if (navMask != 0 && (navMask & (1 << ladderLayer)) != 0)
                {
                    Debug.LogWarning($"[Rig] TileNavGraph's Terrain Mask includes the 'Ladder' layer — ladder tiles will be " +
                                     $"treated as walkable ground. Uncheck 'Ladder' in {nav.name}'s Terrain Mask.", nav);
                    issues++;
                }
            }

            return issues;
        }

        static int ReadMask(Object component, string field)
        {
            var prop = new SerializedObject(component).FindProperty(field);
            return prop != null ? prop.intValue : 0;   // 0 (field renamed) → caller skips
        }
    }
}
