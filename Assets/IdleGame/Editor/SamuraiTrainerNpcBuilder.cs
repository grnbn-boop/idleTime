using System.IO;
using System.Linq;
using IdleTime.Interactions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace IdleTime.Editor
{
    public static class SamuraiTrainerNpcBuilder
    {
        private const string SpritePath = "Assets/IdleGame/Art/Sprites/samurai/IDLE.png";
        private const string AnimationFolder = "Assets/IdleGame/Animations/samurai";
        private const string PrefabPath = "Assets/IdleGame/Prefabs/SamuraiClassTrainer.prefab";
        private const string ClipPath = AnimationFolder + "/samurai_idle.anim";

        [MenuItem("IdleTime/Build Samurai Class Trainer NPC")]
        public static void Build()
        {
            EnsureFolder(AnimationFolder);

            Sprite[] idleSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
                .ToArray();

            if (idleSprites.Length == 0)
            {
                Debug.LogError($"[SamuraiTrainerNpcBuilder] No sliced sprites found at {SpritePath}.");
                return;
            }

            BuildIdleClip(idleSprites);
            GameObject prefabRoot = BuildPrefab(idleSprites);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SamuraiTrainerNpcBuilder] Built {PrefabPath} with {idleSprites.Length} idle frames.");
        }

        private static AnimationClip BuildIdleClip(Sprite[] sprites)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "samurai_idle", frameRate = 12f };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
            {
                frames[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            frames[frames.Length - 1] = new ObjectReferenceKeyframe
            {
                time = sprites.Length / clip.frameRate,
                value = sprites[0]
            };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static GameObject BuildPrefab(Sprite[] idleSprites)
        {
            GameObject root = new GameObject("SamuraiClassTrainer");
            root.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);

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
            collider.offset = new Vector2(0f, 0.1f);
            collider.size = new Vector2(0.55f, 0.75f);

            ClassTrainerNpc trainer = root.AddComponent<ClassTrainerNpc>();

            GameObject textObject = new GameObject("DialogueText");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textObject.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.MidlineGeoAligned;
            text.fontSize = 3f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.sizeDelta = new Vector2(3.5f, 0.75f);

            ConfigureTrainer(trainer, text, collider);
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

        private static void ConfigureTrainer(ClassTrainerNpc trainer, TextMeshPro text, Collider2D collider)
        {
            SerializedObject serialized = new SerializedObject(trainer);
            serialized.FindProperty("dialogueText").objectReferenceValue = text;
            serialized.FindProperty("clickCollider").objectReferenceValue = collider;
            serialized.FindProperty("saveImmediately").boolValue = true;

            PlayerClassReference[] classes =
            {
                new PlayerClassReference("Fighter"),
                new PlayerClassReference("Ranger"),
                new PlayerClassReference("Wizard")
            };

            SerializedProperty classChoices = serialized.FindProperty("classChoices");
            classChoices.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                classChoices.GetArrayElementAtIndex(i).objectReferenceValue = classes[i].Load();
            }

            serialized.FindProperty("startingClass").objectReferenceValue = LoadClass("Normie");
            serialized.FindProperty("optionsLocalOffset").vector2Value = new Vector2(0f, 0.42f);
            serialized.FindProperty("optionSpacing").floatValue = 0.16f;
            serialized.FindProperty("optionBoxSize").vector2Value = new Vector2(1.5f, 0.18f);
            serialized.FindProperty("optionFontSize").floatValue = 2.5f;
            serialized.FindProperty("optionSortingOrder").intValue = 31;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object LoadClass(string className) =>
            AssetDatabase.LoadAssetAtPath<Object>($"Assets/IdleGame/Data/PlayerClasses/{className}.asset");

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static int ExtractTrailingNumber(string value)
        {
            int underscore = value.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(value.Substring(underscore + 1), out int result))
                return result;
            return 0;
        }

        private readonly struct PlayerClassReference
        {
            private readonly string className;

            public PlayerClassReference(string className) => this.className = className;

            public Object Load() => LoadClass(className);
        }
    }
}
