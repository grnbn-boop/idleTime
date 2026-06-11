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
        public event Action<int> OnGoldRolled;
        public event Action OnAttack;

        [Header("Animation")]
        [SerializeField] private string deathAnimationState = "die";
        [SerializeField] private float deathAnimationDuration = 0.6f;

        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private Rigidbody2D rb;
        private Animator animator;

        private void Awake()
        {
            CurrentHealth = data.maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();

            // Enemies should not physically block the player or each other;
            // combat uses direct method calls so a trigger collider is sufficient.
            if (col != null) col.isTrigger = true;
        }

        // Rolls hit chance against this monster's min/max accuracy, then applies a
        // varied (and possibly crit) hit. Crit chance/multiplier are resolved by the
        // caller (DEX/STR via StatFormulas). Bosses take bonusDamageMultiplier (WIS) on top.
        // Returns damage dealt (0 on miss); `crit` reports whether the hit critically struck.
        public float ReceiveAttack(int playerAccuracy, int playerAttack, float damageMultiplier, float critChance, float critMultiplier, float bossDamageMultiplier, out bool crit)
        {
            crit = false;
            if (!IsAlive) return 0f;
            if (!CombatMath.RollHit(playerAccuracy, data.minAccuracy, data.maxAccuracy)) return 0f;

            int damage = CombatMath.PlayerHitDamage(playerAttack, damageMultiplier, critChance, critMultiplier, out crit);
            if (data.isBoss) damage = Mathf.RoundToInt(damage * bossDamageMultiplier);
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

            PlayDeathAnimation();

            // Disable combat physics immediately, but keep visuals alive long enough
            // for the death animation to be seen.
            if (col != null) col.enabled = false;
            if (rb != null) rb.simulated = false;

            List<(ItemDefinition, int)> drops = RollLoot();
            OnLootRolled?.Invoke(drops);

            int gold = RollGold();
            if (gold > 0) OnGoldRolled?.Invoke(gold);

            OnDeath?.Invoke(this);

            StartCoroutine(DeathAndRespawnRoutine());
        }

        private List<(ItemDefinition item, int quantity)> RollLoot()
        {
            var drops = new List<(ItemDefinition, int)>();

            if (data.lootTable == null || data.lootTable.Length == 0)
            {
                Debug.LogWarning($"[Loot] {data.monsterName}: loot table is null or empty — no drops possible.");
                return drops;
            }

            // Luck scales every drop chance (capped at 100%). Falls back to ×1 if there's
            // no active character (e.g. test scene with no PlayerManager).
            float dropMultiplier = IdleTime.Core.PlayerManager.Instance?.ActiveCharacter?.DropRateMultiplier ?? 1f;

            Debug.Log($"[Loot] {data.monsterName}: rolling {data.lootTable.Length} loot entr(ies) (drop ×{dropMultiplier:F2})");
            foreach (LootEntry entry in data.lootTable)
            {
                if (entry.item == null)
                {
                    Debug.LogWarning($"[Loot] {data.monsterName}: a loot entry has a null ItemDefinition — assign it in the MonsterData asset.");
                    continue;
                }

                float chance = Mathf.Clamp01(entry.dropChance * dropMultiplier);
                float roll = UnityEngine.Random.value;
                bool hit = roll <= chance;
                Debug.Log($"[Loot]   {entry.item.itemName}: roll={roll:F3} vs chance={chance:F3} => {(hit ? "DROP" : "miss")}");

                if (hit)
                    drops.Add((entry.item, entry.quantity));
            }

            Debug.Log($"[Loot] {data.monsterName}: {drops.Count} item(s) dropped");
            return drops;
        }

        // Inclusive roll between the monster's gold min/max. Returns 0 when the monster
        // has no gold reward configured, so callers can skip spawning a coin.
        private int RollGold()
        {
            int min = Mathf.Max(0, data.goldRewardMin);
            int max = Mathf.Max(min, data.goldRewardMax);
            if (max <= 0) return 0;
            return UnityEngine.Random.Range(min, max + 1);
        }

        private IEnumerator DeathAndRespawnRoutine()
        {
            float visibleDeathDuration = PlayableDeathDuration;
            if (visibleDeathDuration > 0f)
                yield return new WaitForSeconds(visibleDeathDuration);

            if (spriteRenderer != null) spriteRenderer.enabled = false;

            float remainingRespawnTime = Mathf.Max(0f, data.respawnTime - visibleDeathDuration);
            if (remainingRespawnTime > 0f)
                yield return new WaitForSeconds(remainingRespawnTime);

            CurrentHealth = data.maxHealth;
            IsAlive = true;

            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (col != null) col.enabled = true;
            if (rb != null) rb.simulated = true;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            OnRespawn?.Invoke(this);
        }

        private float PlayableDeathDuration
        {
            get
            {
                if (animator == null || string.IsNullOrEmpty(deathAnimationState))
                    return 0f;

                return animator.HasState(0, Animator.StringToHash(deathAnimationState))
                    ? Mathf.Max(0f, deathAnimationDuration)
                    : 0f;
            }
        }

        private void PlayDeathAnimation()
        {
            if (PlayableDeathDuration <= 0f) return;

            if (spriteRenderer != null) spriteRenderer.enabled = true;
            animator.Play(deathAnimationState, 0, 0f);
        }
    }
}
