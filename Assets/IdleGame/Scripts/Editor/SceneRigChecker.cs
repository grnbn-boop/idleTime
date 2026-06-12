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
    // Menu IdleTime ▸ Check Scene Rig. Verifies the OPEN scene has exactly one of every
    // piece a playable room needs — the DontDestroyOnLoad manager singletons plus the
    // per-scene Player / Camera / UI / EventSystem. Catches a room built by hand that's
    // missing the GameSystems prefab, or one that ended up with two EventSystems. (The
    // singletons self-dedupe at RUNTIME, but a duplicate inside one scene is an authoring
    // mistake worth surfacing before Play.)
    public static class SceneRigChecker
    {
        [MenuItem("IdleTime/Check Scene Rig")]
        static void Check()
        {
            (System.Type type, string label)[] required =
            {
                // Persistent singletons (the GameSystems prefab)
                (typeof(PlayerManager), "PlayerManager"),
                (typeof(SaveManager), "SaveManager"),
                (typeof(Inventory), "Inventory"),
                (typeof(EquipmentManager), "EquipmentManager"),
                (typeof(SkillManager), "SkillManager"),
                (typeof(IdleTime.UI.ItemDragManager), "ItemDragManager"),
                (typeof(IdleTime.UI.TooltipManager), "TooltipManager"),
                (typeof(ScreenFader), "ScreenFader"),
                (typeof(DeathSequenceController), "DeathSequenceController"),
                (typeof(UIManager), "UIManager"),
                // Per-scene rig
                (typeof(ClickToMove2D), "Player (ClickToMove2D)"),
                (typeof(CameraFollow2D), "Camera (CameraFollow2D)"),
                (typeof(IdleTime.UI.PortalNavHUD), "Portal Nav HUD"),
                (typeof(EventSystem), "EventSystem"),
            };

            int missing = 0, dupes = 0;
            foreach (var (type, label) in required)
            {
                int count = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                if (count == 0)
                {
                    missing++;
                    Debug.LogWarning($"[Rig] Missing: {label}.");
                }
                else if (count > 1)
                {
                    dupes++;
                    Debug.LogWarning($"[Rig] {count}× {label} in this scene — expected exactly 1.");
                }
            }

            int ladderIssues = CheckLadderSetup();

            string scene = SceneManager.GetActiveScene().name;
            if (missing == 0 && dupes == 0 && ladderIssues == 0)
                Debug.Log($"[Rig] '{scene}' has the full rig — 1 of each required piece. Good to Play.");
            else
                Debug.LogWarning($"[Rig] '{scene}': {missing} missing, {dupes} duplicated, {ladderIssues} ladder issue(s) — see warnings above.");
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
