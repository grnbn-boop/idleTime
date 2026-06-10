using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IdleTime.Core;

namespace IdleTime.Combat
{
    public class MonsterController : MonoBehaviour
    {
        public MonsterData data;

        public bool IsAlive { get; private set; } = true;
        public float CurrentHealth { get; private set; }

        public event Action<MonsterController> OnDeath;
        public event Action<MonsterController> OnRespawn;
        public event Action<List<(ItemDefinition item, int quantity)>> OnLootRolled;
        public event Action OnAttack;

        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private Rigidbody2D rb;

        private void Awake()
        {
            CurrentHealth = data.maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            rb = GetComponent<Rigidbody2D>();

            // Enemies should not physically block the player or each other;
            // combat uses direct method calls so a trigger collider is sufficient.
            if (col != null) col.isTrigger = true;
        }

        // Rolls hit chance against this monster's min/max accuracy, then applies a
        // varied (and possibly crit) hit. Returns damage dealt (0 on miss); `crit`
        // reports whether the hit critically struck (for VFX/log).
        public float ReceiveAttack(int playerAccuracy, int playerAttack, int playerLuk, out bool crit)
        {
            crit = false;
            if (!IsAlive) return 0f;
            if (!CombatMath.RollHit(playerAccuracy, data.minAccuracy, data.maxAccuracy)) return 0f;

            int damage = CombatMath.PlayerHitDamage(playerAttack, playerLuk, out crit);
            ApplyDamage(damage);
            return damage;
        }

        public void ApplyDamage(float damage)
        {
            if (!IsAlive) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (CurrentHealth <= 0f) Die();
        }

        public void TriggerAttack()
        {
            OnAttack?.Invoke();
        }

        private void Die()
        {
            IsAlive = false;

            // Disable visuals and physics but keep the GO active so the respawn coroutine can run
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (col != null) col.enabled = false;
            if (rb != null) rb.simulated = false;

            List<(ItemDefinition, int)> drops = RollLoot();
            OnLootRolled?.Invoke(drops);
            OnDeath?.Invoke(this);

            StartCoroutine(RespawnRoutine());
        }

        private List<(ItemDefinition item, int quantity)> RollLoot()
        {
            var drops = new List<(ItemDefinition, int)>();

            if (data.lootTable == null || data.lootTable.Length == 0)
            {
                Debug.LogWarning($"[Loot] {data.monsterName}: loot table is null or empty — no drops possible.");
                return drops;
            }

            Debug.Log($"[Loot] {data.monsterName}: rolling {data.lootTable.Length} loot entr(ies)");
            foreach (LootEntry entry in data.lootTable)
            {
                if (entry.item == null)
                {
                    Debug.LogWarning($"[Loot] {data.monsterName}: a loot entry has a null ItemDefinition — assign it in the MonsterData asset.");
                    continue;
                }

                float roll = UnityEngine.Random.value;
                bool hit = roll <= entry.dropChance;
                Debug.Log($"[Loot]   {entry.item.itemName}: roll={roll:F3} vs chance={entry.dropChance:F3} => {(hit ? "DROP" : "miss")}");

                if (hit)
                    drops.Add((entry.item, entry.quantity));
            }

            Debug.Log($"[Loot] {data.monsterName}: {drops.Count} item(s) dropped");
            return drops;
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(data.respawnTime);

            CurrentHealth = data.maxHealth;
            IsAlive = true;

            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (col != null) col.enabled = true;
            if (rb != null) rb.simulated = true;

            OnRespawn?.Invoke(this);
        }
    }
}
