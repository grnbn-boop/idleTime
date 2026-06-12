using UnityEditor;
using UnityEngine;
using IdleTime.Interactions;

namespace IdleTime.EditorTools
{
    // Custom inspector for RoomDefinition: the default fields plus a Linking section.
    // A tree edge is two-sided — a parent lists a child in Forward Exits, and that child's
    // Back must point at the parent. Keeping both ends in sync by hand drifts, so this
    // adds a one-click "Reciprocate Back-Links" that sets each forward child's Back to
    // this room. (IdleTime ▸ Validate Map reports any edge where the two sides disagree.)
    [CustomEditor(typeof(RoomDefinition))]
    public class RoomDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var room = (RoomDefinition)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Linking", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Forward Exits = this room's children (forward portals, each kill-gated). " +
                "Back = the parent this room returns to (its back portal, always open). " +
                "After adding forward exits, click below so each child's Back points here.",
                MessageType.None);

            bool hasExits = room.forwardExits != null && room.forwardExits.Length > 0;
            using (new EditorGUI.DisabledScope(!hasExits))
                if (GUILayout.Button("Reciprocate Back-Links (set each child's Back to this room)"))
                    ReciprocateBackLinks(room);
        }

        static void ReciprocateBackLinks(RoomDefinition room)
        {
            int changed = 0;
            foreach (var exit in room.forwardExits)
            {
                var child = exit != null ? exit.target : null;
                if (child == null || child == room || child.back == room) continue;

                Undo.RecordObject(child, "Set Room Back-Link");
                child.back = room;
                EditorUtility.SetDirty(child);
                changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[Map] Set Back on {changed} child room(s) to '{room.name}'.");
        }
    }
}
