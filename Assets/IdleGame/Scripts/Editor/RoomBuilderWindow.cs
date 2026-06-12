using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IdleTime.Interactions;

namespace IdleTime.EditorTools
{
    // Menu IdleTime ▸ New Room From Template. Scene-per-room means every room needs the
    // same scene BODY — Player, Camera, UI Canvas (+ UIManager + ItemDragManager), and the
    // EventSystem. It does NOT need GameSystems: that's the persistent brain, bootstrapped
    // once from Resources/GameSystems by GameBootstrap (see SYSTEMS_WALKTHROUGH §6½). So the
    // TEMPLATE scene should hold the body only — assemble it once, then this tool copies it
    // to a new room scene, registers it in Build Settings, and creates a paired
    // RoomDefinition so the new room drops straight into the map tree. After creating, it
    // runs Check Scene Rig on the new room so you immediately see it's complete; you then
    // paint the level (Grid), place the spawner + portals, and wire each portal.
    public class RoomBuilderWindow : EditorWindow
    {
        const string TemplatePrefKey = "IdleTime.RoomBuilder.TemplateGuid";
        const string FolderPrefKey = "IdleTime.RoomBuilder.RoomsFolder";

        SceneAsset template;
        string roomName = "Room02";
        string roomsFolder = "Assets/Scenes/Rooms";
        bool addToBuild = true;
        bool createDefinition = true;
        bool openAfter = true;

        [MenuItem("IdleTime/New Room From Template")]
        static void Open() => GetWindow<RoomBuilderWindow>("New Room");

        void OnEnable()
        {
            string guid = EditorPrefs.GetString(TemplatePrefKey, "");
            if (!string.IsNullOrEmpty(guid))
                template = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid));
            roomsFolder = EditorPrefs.GetString(FolderPrefKey, roomsFolder);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            template = (SceneAsset)EditorGUILayout.ObjectField("Template Scene", template, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(TemplatePrefKey,
                    template != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template)) : "");

            EditorGUILayout.HelpBox(
                "The template is a scene holding the room BODY only: Player, Camera, UI Canvas " +
                "(+ UIManager + ItemDragManager), and EventSystem. Do NOT put GameSystems in it — " +
                "the managers are bootstrapped from Resources/GameSystems at Play. Save the template " +
                "before stamping rooms — copies use the saved file.",
                MessageType.Info);

            // The brain is bootstrapped from a Resources prefab; without it, stamped rooms boot
            // with a body but no managers. Surface that here rather than at first Play.
            if (Resources.Load<GameObject>("GameSystems") == null)
                EditorGUILayout.HelpBox(
                    "No 'GameSystems' prefab found under a Resources/ folder. GameBootstrap spawns it " +
                    "at launch — make GameSystems a prefab at Resources/GameSystems.prefab, or rooms " +
                    "will start without their managers.",
                    MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New Room", EditorStyles.boldLabel);
            roomName = EditorGUILayout.TextField("Room Name", roomName);

            EditorGUI.BeginChangeCheck();
            roomsFolder = EditorGUILayout.TextField("Rooms Folder", roomsFolder);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(FolderPrefKey, roomsFolder);

            addToBuild = EditorGUILayout.Toggle("Add to Build Settings", addToBuild);
            createDefinition = EditorGUILayout.Toggle("Create RoomDefinition", createDefinition);
            openAfter = EditorGUILayout.Toggle("Open After Create", openAfter);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!CanCreate()))
                if (GUILayout.Button("Create Room", GUILayout.Height(28)))
                    CreateRoom();

            if (template == null)
                EditorGUILayout.HelpBox("Assign a template scene to begin.", MessageType.Warning);
        }

        bool CanCreate() => template != null && !string.IsNullOrWhiteSpace(roomName);

        void CreateRoom()
        {
            // Don't clobber unsaved work in the open scene before we (maybe) switch to the new one.
            if (openAfter && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string safe = roomName.Trim();
            string templatePath = AssetDatabase.GetAssetPath(template);
            EnsureFolder(roomsFolder);
            string scenePath = $"{roomsFolder}/{safe}.unity";

            if (File.Exists(scenePath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite room?",
                        $"{scenePath} already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
                AssetDatabase.DeleteAsset(scenePath);
            }

            if (!AssetDatabase.CopyAsset(templatePath, scenePath))
            {
                Debug.LogError($"[RoomBuilder] Failed to copy '{templatePath}' → '{scenePath}'.");
                return;
            }
            AssetDatabase.ImportAsset(scenePath);

            if (addToBuild) AddSceneToBuild(scenePath);

            RoomDefinition def = createDefinition ? CreateRoomDefinition(safe, scenePath) : null;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openAfter) EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (def != null) { Selection.activeObject = def; EditorGUIUtility.PingObject(def); }

            Debug.Log($"[RoomBuilder] Created room '{safe}' at {scenePath}" +
                      $"{(addToBuild ? " · added to Build Settings" : "")}" +
                      $"{(def != null ? " · made RoomDefinition" : "")}. " +
                      "Next: paint the level (Grid), place the MonsterSpawner + portals, and wire each portal (Room + Destination).");

            // Verify the stamped room matches the brain/body model (body present, no stray
            // GameSystems, Resources prefab valid). Only meaningful once the room is open.
            if (openAfter) SceneRigChecker.ReportRig();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];   // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void AddSceneToBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath) return;   // already registered

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static RoomDefinition CreateRoomDefinition(string sceneName, string scenePath)
        {
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{sceneName}.asset");

            var def = ScriptableObject.CreateInstance<RoomDefinition>();
            def.sceneName = sceneName;     // build scene name == file name (LoadScene by name)
            def.displayName = sceneName;
            AssetDatabase.CreateAsset(def, assetPath);
            return def;
        }
    }
}
