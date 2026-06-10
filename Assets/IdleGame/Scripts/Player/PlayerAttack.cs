using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IdleTime.Core;
using IdleTime.Combat;

namespace IdleTime.Player
{
    // Runs before ClickToMove2D (order -1) so it can suppress clicks that land on monsters.
    [DefaultExecutionOrder(-1)]
    public class PlayerAttack : MonoBehaviour
    {
        [Tooltip("Attack range when the main hand holds a melee weapon (or is empty).")]
        [SerializeField] private float meleeRange = 2f;
        [Tooltip("Attack range when the main hand holds a ranged weapon.")]
        [SerializeField] private float rangedRange = 6f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private Vector2 attackVFXOffset = Vector2.zero;
        [SerializeField] private LayerMask monsterLayer;
        [SerializeField] private ClickToMove2D movement;
        [SerializeField] private AttackVFX attackVFX;
        [SerializeField] private Camera worldCamera;

        private MonsterController target;
        private float attackTimer;
        private readonly List<Collider2D> overlapResults = new List<Collider2D>();
        private ContactFilter2D monsterFilter;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (movement == null) movement = GetComponent<ClickToMove2D>();
            if (attackVFX == null) attackVFX = GetComponentInChildren<AttackVFX>();
            // Disable any child Animators so VFX objects don't auto-play at startup.
            // AttackVFX.Awake also does this when the script is present; this is the fallback.
            Animator playerAnimator = GetComponent<Animator>();
            foreach (Animator anim in GetComponentsInChildren<Animator>())
            {
                if (anim != playerAnimator) anim.enabled = false;
            }

            if (attackVFX == null)
                Debug.LogWarning("[PlayerAttack] No AttackVFX found in children. Add the AttackVFX script to the VFX child object and assign the attack clip.");

            // Explicitly include triggers so we hit the monsters' trigger colliders.
            // Layer mask is applied only if one has actually been configured.
            monsterFilter = new ContactFilter2D { useTriggers = true };
            if (monsterLayer.value != 0)
            {
                monsterFilter.useLayerMask = true;
                monsterFilter.layerMask = monsterLayer;
            }
        }

        private void Update()
        {
            if (PlayerManager.Instance != null && PlayerManager.Instance.IsDead)
            {
                ClearTarget();   // drop the fight while downed
                return;
            }

            ReadClickForTarget();
            UpdateAttackLoop();
        }

        private void ReadClickForTarget()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            Vector2 worldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            overlapResults.Clear();
            Physics2D.OverlapPoint(worldPos, monsterFilter, overlapResults);

            MonsterController mc = null;
            foreach (Collider2D col in overlapResults)
            {
                var candidate = col.GetComponent<MonsterController>();
                if (candidate != null && candidate.IsAlive) { mc = candidate; break; }
            }

            if (mc != null)
            {
                SetTarget(mc);
                movement?.SuppressNextClick();
                return;
            }

            // Clicked somewhere other than a live monster — drop the current target.
            ClearTarget();
        }

        private void UpdateAttackLoop()
        {
            if (target == null || !target.IsAlive) return;

            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > CurrentAttackRange())
            {
                // Chase: update the move target every frame so the player follows a moving monster.
                movement?.SetMoveTarget(target.transform.position.x);
                return;
            }

            movement?.ClearMoveTarget();
            movement?.FaceDirection(target.transform.position.x);

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                PerformAttack();
            }
        }

        // Range is driven by the equipped main-hand weapon's type: ranged weapons let
        // the player stop and fire from further away. Unarmed / non-weapon → melee range.
        private float CurrentAttackRange()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c != null && c.equipment.Get(EquipSlot.MainHand) is WeaponDefinition weapon)
                return weapon.weaponType == WeaponType.Ranged ? rangedRange : meleeRange;
            return meleeRange;
        }

        private void PerformAttack()
        {
            CharacterData character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null) return;

            float damage = target.ReceiveAttack(character.Accuracy, character.Attack, character.Luk, out bool crit);
            if (damage > 0f)
            {
                DamageNumberSpawner.Show(target.transform.position + Vector3.up,
                    damage, crit ? DamagePopupType.Crit : DamagePopupType.DealtToEnemy);
                Debug.Log($"[Combat] Player → {target.gameObject.name}: {damage}{(crit ? " CRIT!" : "")} dmg  (HP: {target.CurrentHealth}/{target.data.maxHealth})  [ACC:{character.Accuracy} ATK:{character.Attack} LUK:{character.Luk}]");
            }
            else
            {
                DamageNumberSpawner.Show(target.transform.position + Vector3.up, 0f, DamagePopupType.Miss);
                Debug.Log($"[Combat] MISS — Player → {target.gameObject.name}  [ACC:{character.Accuracy}]");
            }

            bool facingLeft = target.transform.position.x < transform.position.x;
            attackVFX?.Play(attackVFXOffset, facingLeft);
        }

        private void SetTarget(MonsterController mc)
        {
            if (target == mc) return;

            if (target != null)
            {
                target.OnDeath -= OnTargetDied;
                target.OnRespawn -= OnTargetRespawned;
            }

            target = mc;
            target.OnDeath += OnTargetDied;
            target.OnRespawn += OnTargetRespawned;
            attackTimer = 0f; // First swing lands as soon as the player is in range.
        }

        private void ClearTarget()
        {
            if (target == null) return;
            target.OnDeath -= OnTargetDied;
            target.OnRespawn -= OnTargetRespawned;
            target = null;
        }

        private void OnTargetDied(MonsterController mc)
        {
            PlayerManager.Instance?.GainXP(mc.data.xpReward);
            ClearTarget();
        }

        private void OnTargetRespawned(MonsterController mc) => ClearTarget();
    }
}
