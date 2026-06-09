using System;
using UnityEngine;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewMonster", menuName = "IdleTime/Monster")]
    public class MonsterData : ScriptableObject
    {
        public string monsterName = "Monster";

        [Header("Combat Stats")]
        public int attack = 10;
        public float maxHealth = 50f;

        [Header("Hit Requirement")]
        [Tooltip("Player accuracy stat at which hit chance is 5%. At maxAccuracy, hit chance is 100%. Scales linearly between.")]
        public int minAccuracy = 13;
        [Tooltip("Player accuracy stat at which hit chance reaches 100%.")]
        public int maxAccuracy = 38;

        [Header("Defense Requirement")]
        [Tooltip("Player defense needed to take 0 damage. Damage scales linearly down to 0 as player defense approaches this value.")]
        public int defenseToNegate = 20;

        [Header("Rewards")]
        public int xpReward = 10;

        [Header("Respawn")]
        public float respawnTime = 30f;

        [Header("Loot Table")]
        public LootEntry[] lootTable;
    }

    [Serializable]
    public class LootEntry
    {
        public ItemDefinition item;
        public int quantity = 1;
        [Range(0f, 1f)]
        public float dropChance = 0.1f;
    }
}
