using System.IO;
using UnityEditor;
using UnityEngine;
using IdleTime.Core;

namespace IdleTime.EditorTools
{
    // Dev tool for inspecting and wiping save files: menu IdleTime ▸ Save Tools.
    // Deleting a character's file resets them to their Inspector-authored defaults
    // on the next Play.
    public class SaveToolsWindow : EditorWindow
    {
        [MenuItem("IdleTime/Save Tools")]
        static void Open() => GetWindow<SaveToolsWindow>("Save Tools");

        Vector2 scroll;

        void OnGUI()
        {
            EditorGUILayout.LabelField("Save folder", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(SaveManager.SaveFolder, EditorStyles.miniLabel, GUILayout.Height(16));

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!Directory.Exists(SaveManager.SaveFolder)))
                {
                    if (GUILayout.Button("Open Folder"))
                        EditorUtility.RevealInFinder(SaveManager.SaveFolder);

                    if (GUILayout.Button("Wipe All") &&
                        EditorUtility.DisplayDialog("Wipe all saves?",
                            "Deletes every save file (master + all characters). This cannot be undone.",
                            "Wipe All", "Cancel"))
                    {
                        SaveManager.WipeAll();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "The game auto-saves when Play Mode ends — wipe files after exiting Play Mode, or they will be re-created.",
                    MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save files", EditorStyles.boldLabel);

            if (!Directory.Exists(SaveManager.SaveFolder))
            {
                EditorGUILayout.HelpBox("No saves yet — they are written when Play Mode ends (or via SaveManager → Save Now).", MessageType.Info);
                return;
            }

            string[] files = Directory.GetFiles(SaveManager.SaveFolder, "*.json");
            if (files.Length == 0)
            {
                EditorGUILayout.HelpBox("Save folder exists but is empty.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(fileName);

                    if (GUILayout.Button("View", GUILayout.Width(50)))
                        EditorUtility.OpenWithDefaultApp(file);

                    if (GUILayout.Button("Reset L1", GUILayout.Width(70)) &&
                        EditorUtility.DisplayDialog("Reset to level 1?",
                            $"Reset {fileName} to a fresh level-1 character? Keeps the name and class but clears XP, skills, unlocked classes, gear, and inventory.",
                            "Reset", "Cancel"))
                    {
                        SaveManager.ResetSaveFileToLevelOne(file);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Delete", GUILayout.Width(60)) &&
                        EditorUtility.DisplayDialog("Delete save file?",
                            $"Delete {fileName}? The character resets to Inspector defaults on next Play.",
                            "Delete", "Cancel"))
                    {
                        File.Delete(file);
                        GUIUtility.ExitGUI();   // file list changed mid-layout
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
