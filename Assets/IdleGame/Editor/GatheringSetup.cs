using IdleTime.Core;
using IdleTime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleTime.Editor
{
    // One-shot setup helpers for the gathering system.
    public static class GatheringSetup
    {
        private const string FolderParent = "Assets/IdleGame/Resources";
        private const string Folder = "Assets/IdleGame/Resources/Gathering";

        [MenuItem("IdleTime/Create Gathering Skill Definitions")]
        public static void CreateDefinitions()
        {
            if (!AssetDatabase.IsValidFolder(FolderParent))
                AssetDatabase.CreateFolder("Assets/IdleGame", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(FolderParent, "Gathering");

            Create(GatheringSkillType.Woodcutting, "Woodcutting", PrimaryStat.Wis, new Color(0.35f, 0.65f, 0.25f), false);
            Create(GatheringSkillType.Mining,      "Mining",      PrimaryStat.Str, new Color(0.6f, 0.6f, 0.66f), false);
            Create(GatheringSkillType.Crafting,    "Crafting",    PrimaryStat.Dex, new Color(0.65f, 0.5f, 0.3f), true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GatheringSetup] Created gathering skill definitions under {Folder}.");
        }

        private static void Create(GatheringSkillType type, string name, PrimaryStat stat, Color color, bool stub)
        {
            string path = $"{Folder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<GatheringSkillDefinition>(path) != null) return;   // don't clobber edits

            var d = ScriptableObject.CreateInstance<GatheringSkillDefinition>();
            d.type = type;
            d.displayName = name;
            d.buffStat = stat;
            d.placeholderColor = color;
            d.isStub = stub;
            AssetDatabase.CreateAsset(d, path);
        }

        [MenuItem("IdleTime/Add Skill Stats Tab To Stats Overlay")]
        public static void AddStatsTab()
        {
            var uiManager = Object.FindAnyObjectByType<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("[GatheringSetup] No UIManager in the open scene — open a room scene first.");
                return;
            }

            var so = new SerializedObject(uiManager);
            var overlayProp = so.FindProperty("statOverlay");
            var overlay = overlayProp != null ? overlayProp.objectReferenceValue as GameObject : null;
            if (overlay == null)
            {
                Debug.LogError("[GatheringSetup] UIManager.statOverlay is not assigned — can't add the tab controller.");
                return;
            }

            if (overlay.GetComponent<StatsTabController>() != null)
            {
                Debug.Log("[GatheringSetup] Stat overlay already has a StatsTabController.");
                return;
            }

            Undo.AddComponent<StatsTabController>(overlay);
            EditorSceneManager.MarkSceneDirty(overlay.scene);
            Debug.Log($"[GatheringSetup] Added StatsTabController to '{overlay.name}'. Save the scene to keep it.");
        }
    }
}
