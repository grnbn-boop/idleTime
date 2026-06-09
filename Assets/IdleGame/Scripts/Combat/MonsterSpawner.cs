using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Combat
{
    [Serializable]
    public class SpawnZone
    {
        public Vector2 center;
        public Vector2 size = new Vector2(4f, 3f);

        public float XMin => center.x - size.x * 0.5f;
        public float XMax => center.x + size.x * 0.5f;
        public float YTop => center.y + size.y * 0.5f;
    }

    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private int maxSpawnCount = 3;
        [SerializeField] private List<SpawnZone> spawnZones = new List<SpawnZone>();
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 30f;
        [SerializeField] private float minSpawnSeparation = 1.5f;

        // Tracks X positions used in this spawn batch to prevent stacking
        private readonly List<float> usedSpawnX = new List<float>();

        private void Start()
        {
            usedSpawnX.Clear();
            for (int i = 0; i < maxSpawnCount; i++)
                SpawnMonster();
        }

        private void SpawnMonster()
        {
            if (monsterPrefab == null)
            {
                Debug.LogWarning("[MonsterSpawner] No monster prefab assigned.");
                return;
            }

            if (spawnZones.Count == 0)
            {
                Debug.LogWarning("[MonsterSpawner] No spawn zones defined.");
                return;
            }

            SpawnZone zone = spawnZones[UnityEngine.Random.Range(0, spawnZones.Count)];
            Vector2? spawnPoint = FindFloorInZone(zone);

            if (!spawnPoint.HasValue)
            {
                Debug.LogWarning("[MonsterSpawner] Could not find a valid floor position in any spawn zone.");
                return;
            }

            usedSpawnX.Add(spawnPoint.Value.x);

            GameObject go = Instantiate(monsterPrefab, spawnPoint.Value, Quaternion.identity);

            MonsterAI ai = go.GetComponent<MonsterAI>();
            ai?.Initialize(spawnPoint.Value.y, zone.XMin, zone.XMax);
        }

        private Vector2? FindFloorInZone(SpawnZone zone)
        {
            // Sample random X positions; reject any that are too close to an already-used position
            for (int i = 0; i < 20; i++)
            {
                float x = UnityEngine.Random.Range(zone.XMin, zone.XMax);

                bool tooClose = false;
                foreach (float usedX in usedSpawnX)
                {
                    if (Mathf.Abs(x - usedX) < minSpawnSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, zone.YTop), Vector2.down, groundCheckDistance, groundLayer);
                if (hit.collider != null)
                    return hit.point;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnZones == null) return;

            foreach (SpawnZone zone in spawnZones)
            {
                Gizmos.color = new Color(1f, 0.55f, 0f, 0.25f);
                Gizmos.DrawCube(zone.center, zone.size);
                Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
                Gizmos.DrawWireCube(zone.center, zone.size);
            }
        }
    }
}
