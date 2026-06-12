using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using IdleTime.Core;
using IdleTime.Player;
using IdleTime.CameraRig;

namespace IdleTime.EditorTools
{
    // Menu IdleTime ▸ Check Scene Rig. Validates the OPEN scene against the brain/body
    // split (see SYSTEMS_WALKTHROUGH §6½):
    //   • BRAIN — the global manager singletons. They come from the bootstrapped
    //             GameSystems prefab (Resources/GameSystems) at runtime, so a room scene
    //             should NOT contain them. Anything found here is an authored leftover to
    //             remove. We instead validate the prefab itself carries the full brain.
    //   • BODY  — the per-room pieces a scene must place for itself: Player, Camera, the
    //             UI managers that reference scene UI (UIManager, ItemDragManager), and
    //             the EventSystem. PortalNavHUD is omitted on purpose — it auto-creates.
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
            problems += CheckLadderSetup();

            string scene = SceneManager.GetActiveScene().name;
            if (problems == 0)
                Debug.Log($"[Rig] '{scene}' matches the room model — scene body present, no stray brain, GameSystems prefab valid. Good to Play.");
            else
                Debug.LogWarning($"[Rig] '{scene}': {problems} issue(s) — see warnings above.");
        }

        static int CountInScene(System.Type type) =>
            Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

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

        // The one silent footgun in the ladder setup: if the player's terrainMask still
        // includes the ladder tilemap's layer, the ground/wall casts treat ladders as
        // solid and walk-through breaks with no error. Surface it before Play.
        static int CheckLadderSetup()
        {
            var player = Object.FindFirstObjectByType<ClickToMove2D>(FindObjectsInactive.Include);
            if (player == null) return 0;   // no player; the rig check above already flagged it

            int ladderLayer = LayerMask.NameToLayer("Ladder");

            // Only meaningful if there's actually a ladder tilemap in the scene.
            Tilemap ladderTilemap = null;
            foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
            var nav = Object.FindFirstObjectByType<IdleTime.Navigation.TileNavGraph>(FindObjectsInactive.Include);
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
