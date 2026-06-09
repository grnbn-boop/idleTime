using System.Collections.Generic;
using UnityEngine;
using IdleTime.Core;

namespace IdleTime.Combat
{
    [RequireComponent(typeof(MonsterController))]
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] WorldItem worldItemPrefab;

        [Tooltip("Speed (units/s) of the initial launch impulse.")]
        [SerializeField] float launchSpeed = 5f;

        [Tooltip("Total fan angle in degrees, centred straight up. " +
                 "80° means items spread between 50° and 130°.")]
        [SerializeField] float arcSpreadDeg = 80f;

        void Awake()
        {
            GetComponent<MonsterController>().OnLootRolled += SpawnDrops;
        }

        void SpawnDrops(List<(ItemDefinition item, int quantity)> drops)
        {
            if (drops.Count == 0)
            {
                Debug.Log($"[LootDropper] {name}: no drops to spawn (empty list from RollLoot).");
                return;
            }

            if (worldItemPrefab == null)
            {
                Debug.LogError($"[LootDropper] {name}: worldItemPrefab is not assigned — " +
                               "items rolled but cannot spawn. Assign it on the monster prefab.");
                return;
            }

            // Flatten quantities so each physical item gets its own arc angle
            var allItems = new List<ItemDefinition>();
            foreach (var (item, qty) in drops)
                for (int i = 0; i < qty; i++)
                    allItems.Add(item);

            int total = allItems.Count;

            for (int i = 0; i < total; i++)
            {
                // Evenly fan items across arcSpreadDeg, centred on 90° (straight up).
                // Single items shoot almost straight up with a tiny random wobble.
                float t        = total > 1 ? (float)i / (total - 1) : 0.5f;
                float angleDeg = (90f - arcSpreadDeg * 0.5f) + arcSpreadDeg * t
                                 + Random.Range(-4f, 4f); // small random wobble per item
                float rad      = angleDeg * Mathf.Deg2Rad;
                Vector2 vel    = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * launchSpeed;

                WorldItem drop = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
                drop.SetItem(allItems[i]);
                drop.Launch(vel);
                Debug.Log($"[LootDropper] {name}: launching {allItems[i].itemName} at {angleDeg:F1}°");
            }
        }
    }
}
