using System.Collections.Generic;
using UnityEngine;
using IdleTime.Core;

namespace IdleTime.Combat
{
    [RequireComponent(typeof(MonsterController))]
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] WorldItem worldItemPrefab;
        [SerializeField] float dropRadius = 0.5f;

        void Awake()
        {
            GetComponent<MonsterController>().OnLootRolled += SpawnDrops;
        }

        void SpawnDrops(List<(ItemDefinition item, int quantity)> drops)
        {
            foreach (var (item, quantity) in drops)
            {
                for (int i = 0; i < quantity; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * dropRadius;
                    WorldItem drop = Instantiate(worldItemPrefab, (Vector2)transform.position + offset, Quaternion.identity);
                    drop.SetItem(item);
                }
            }
        }
    }
}
