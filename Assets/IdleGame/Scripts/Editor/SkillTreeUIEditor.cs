using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillTreeUI))]
public class SkillTreeUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ui = (SkillTreeUI)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layout Preview (Editor Only)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Spawns the node prefabs at the positions stored in the Preview Tree so you can see the " +
            "full skill layout while editing. Preview objects are not saved into the scene.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Preview"))
            {
                ui.EditorBuildPreview();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Clear Preview"))
            {
                ui.EditorClearPreview();
                SceneView.RepaintAll();
            }
        }
    }
}
