using System.Linq;
using IdleTime.Interactions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace IdleTime.Editor
{
    /// <summary>
    /// Stamps a vendor NPC prefab (ShopNpc). Reuses the samurai IDLE sheet as placeholder
    /// art for now; designers fill the shop's stock/prices and title on the ShopNpc
    /// component in the inspector. Mirrors GenericNpcBuilder, swapping the talk-only
    /// NpcDialogue for a ShopNpc.
    /// </summary>
    public static class ShopNpcBuilder
    {
        private const string SpritePath = "Assets/IdleGame/Art/Sprites/samurai/IDLE.png";
        private const string PrefabPath = "Assets/IdleGame/Prefabs/ShopNpc.prefab";

        [MenuItem("IdleTime/Build Shop NPC")]
        public static void Build()
        {
            Sprite[] idleSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
                .ToArray();

            if (idleSprites.Length == 0)
            {
                Debug.LogError($"[ShopNpcBuilder] No sliced sprites found at {SpritePath}.");
                return;
            }

            GameObject prefabRoot = BuildPrefab(idleSprites);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ShopNpcBuilder] Built {PrefabPath} — fill the ShopNpc stock/prices in the inspector.");
        }

        private static GameObject BuildPrefab(Sprite[] idleSprites)
        {
            GameObject root = new GameObject("ShopNpc");
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

            ShopNpc shop = root.AddComponent<ShopNpc>();

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

            ConfigureShop(shop, text, collider);
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

        private static void ConfigureShop(ShopNpc shop, TextMeshPro text, Collider2D collider)
        {
            SerializedObject serialized = new SerializedObject(shop);
            serialized.FindProperty("dialogueText").objectReferenceValue = text;
            serialized.FindProperty("clickCollider").objectReferenceValue = collider;
            serialized.FindProperty("shopTitle").stringValue = "Shop";
            serialized.FindProperty("sellMultiplier").floatValue = 0.5f;

            SerializedProperty lines = serialized.FindProperty("lines");
            lines.arraySize = 1;
            lines.GetArrayElementAtIndex(0).stringValue = "Welcome! Take a look at my wares.";

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
