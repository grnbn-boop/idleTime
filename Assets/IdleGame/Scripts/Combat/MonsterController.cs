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

        private const float MinHitChance = 0.05f;

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

        // playerAccuracy: used for hit-chance roll against this monster's min/max accuracy range.
        // playerAttack:   raw attack power (Str); stub — applied as flat damage for now.
        // Returns damage dealt (0 on miss).
        public float ReceiveAttack(int playerAccuracy, int playerAttack)
        {
            if (!IsAlive) return 0f;
            if (!RollHit(playerAccuracy)) return 0f;

            float damage = CalculateDamage(playerAttack);
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

        private bool RollHit(float playerAccuracy)
        {
            // Power curve: ratio^0.6 gives fast early gains that taper near cap.
            // maxAccuracy is the AccReq (stat needed for 100% hit).
            float ratio = Mathf.Clamp01(playerAccuracy / data.maxAccuracy);
            float hitChance = Mathf.Pow(ratio, 0.6f);
            if (hitChance < MinHitChance) return false;
            return UnityEngine.Random.value <= hitChance;
        }

        private float CalculateDamage(int playerAttack)
        {
            // Stub: flat damage. Expand with monster defence stats when the combat system matures.
            return playerAttack;
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
            if (data.lootTable == null) return drops;

            foreach (LootEntry entry in data.lootTable)
            {
                if (entry.item == null) continue;
                if (UnityEngine.Random.value <= entry.dropChance)
                    drops.Add((entry.item, entry.quantity));
            }

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
