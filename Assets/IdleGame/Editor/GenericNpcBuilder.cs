using System.Linq;
using IdleTime.Interactions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace IdleTime.Editor
{
    /// <summary>
    /// Stamps a single reusable talk-only NPC prefab (GenericNpc). Designers drop
    /// copies into room scenes and override the name/lines/sprite per instance in
    /// the inspector. Reuses the samurai IDLE sheet as placeholder art for now;
    /// future NPCs follow the same setup by repointing <see cref="SpritePath"/>.
    ///
    /// Quest-giver wish: when an NPC needs to hand out a quest, subclass
    /// NpcDialogue the same way ClassTrainerNpc does and add the quest hook in
    /// OnDialogueFinished. Generic NPCs stay talk-only.
    /// </summary>
    public static class GenericNpcBuilder
    {
        private const string SpritePath = "Assets/IdleGame/Art/Sprites/samurai/IDLE.png";
        private const string PrefabPath = "Assets/IdleGame/Prefabs/GenericNpc.prefab";

        [MenuItem("IdleTime/Build Generic NPC")]
        public static void Build()
        {
            Sprite[] idleSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
                .ToArray();

            if (idleSprites.Length == 0)
            {
                Debug.LogError($"[GenericNpcBuilder] No sliced sprites found at {SpritePath}.");
                return;
            }

            GameObject prefabRoot = BuildPrefab(idleSprites);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenericNpcBuilder] Built {PrefabPath} with {idleSprites.Length} idle frames.");
        }

        private static GameObject BuildPrefab(Sprite[] idleSprites)
        {
            GameObject root = new GameObject("GenericNpc");
            root.transform.localScale = new Vector3(6f, 6f, 6f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSprites[0];
            renderer.sortingOrder = 9;

            NpcIdleAnimator idleAnimator = root.AddComponent<NpcIdleAnimator>();
            ConfigureIdleAnimator(idleAnimator, renderer, idleSprites);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.offset = new Vector2(0f, 0.4f);
            collider.size = new Vector2(0.3f, 0.5f);

            NpcDialogue dialogue = root.AddComponent<NpcDialogue>();

            GameObject textObject = new GameObject("DialogueText");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textObject.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.MidlineGeoAligned;
            text.fontSize = 7f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.sizeDelta = new Vector2(3.5f, 0.75f);

            ConfigureDialogue(dialogue, text, collider);
            return root;
        }

        private static void ConfigureIdleAnimator(NpcIdleAnimator idleAnimator, SpriteRenderer renderer, Sprite[] idleSprites)
        {
            SerializedObject serialized = new SerializedObject(idleAnimator);
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("framesPerSecond").floatValue = 12f;
            serialized.FindProperty("playOnEnable").boolValue = true;

            SerializedProperty frames = serialized.FindProperty("frames");
            frames.arraySize = idleSprites.Length;
            for (int i = 0; i < idleSprites.Length; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = idleSprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDialogue(NpcDialogue dialogue, TextMeshPro text, Collider2D collider)
        {
            SerializedObject serialized = new SerializedObject(dialogue);
            serialized.FindProperty("dialogueText").objectReferenceValue = text;
            serialized.FindProperty("clickCollider").objectReferenceValue = collider;

            SerializedProperty lines = serialized.FindProperty("lines");
            lines.arraySize = 2;
            lines.GetArrayElementAtIndex(0).stringValue = "Oh, hello there.";
            lines.GetArrayElementAtIndex(1).stringValue = "Mind how you go out there.";

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int ExtractTrailingNumber(string value)
        {
            int underscore = value.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(value.Substring(underscore + 1), out int result))
                return result;
            return 0;
        }
    }
}
