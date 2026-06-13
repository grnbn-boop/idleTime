using System.Linq;
using IdleTime.Core;
using IdleTime.Interactions;
using UnityEditor;
using UnityEngine;

namespace IdleTime.Editor
{
    // Stamps reusable gathering-node prefabs (a tree for Woodcutting, a rock for Mining).
    // Designers drop copies into room scenes and set the reward item per instance in the
    // inspector. Crafting reuses the same recipe — duplicate a prefab, set its skillType to
    // Crafting and swap the sprite.
    public static class ResourceNodeBuilder
    {
        private const string TreeSprite = "Assets/IdleGame/Art/Tiles/GrassLand/Props/GrassLand_Tree.png";
        private const string RockSprite = "Assets/IdleGame/Art/Tiles/GrassLand/Props/GrassLand_Stone_3.png";

        [MenuItem("IdleTime/Build Resource Nodes")]
        public static void Build()
        {
            BuildNode("ResourceNode_Tree", TreeSprite, GatheringSkillType.Woodcutting);
            BuildNode("ResourceNode_Rock", RockSprite, GatheringSkillType.Mining);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildNode(string prefabName, string spritePath, GatheringSkillType type)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(spritePath).OfType<Sprite>().FirstOrDefault()
                            ?? AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"[ResourceNodeBuilder] No sprite found at {spritePath}.");
                return;
            }

            var root = new GameObject(prefabName);
            root.transform.localScale = new Vector3(4f, 4f, 4f);

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 8;

            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;   // walk-through; ResourceNode.Awake also enforces this

            var node = root.AddComponent<ResourceNode>();
            var so = new SerializedObject(node);
            so.FindProperty("skillType").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"Assets/IdleGame/Prefabs/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[ResourceNodeBuilder] Built {path} ({type}). Assign its reward item per instance in the scene.");
        }
    }
}
