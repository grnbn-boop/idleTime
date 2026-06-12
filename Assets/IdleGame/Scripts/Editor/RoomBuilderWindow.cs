using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IdleTime.Interactions;

namespace IdleTime.EditorTools
{
    // Menu IdleTime ▸ New Room From Template. Scene-per-room means every room needs the
    // same shared rig (the GameSystems prefab of DontDestroyOnLoad singletons, plus Player,
    // Camera, UI Canvas + Portal Nav HUD, EventSystem). Rather than rebuild that each time,
    // you assemble it once into a TEMPLATE scene; this tool copies that template to a new
    // room scene, registers it in Build Settings, and creates a paired RoomDefinition so
    // the new room drops straight into the map tree. You then paint the level and place
    // portals.
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
                "The template is a scene holding the shared rig: the GameSystems prefab " +
                "(managers / fader / tooltip), Player, Camera, UI Canvas + Portal Nav HUD, and " +
                "EventSystem. Save it before stamping rooms — copies use the saved file.",
                MessageType.Info);

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
                      "Next: paint the level, then place + wire its portals (set each portal's Room + Destination).");
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
