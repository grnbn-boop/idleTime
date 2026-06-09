using UnityEngine;
using UnityEngine.Serialization;

namespace IdleTime.Core
{
    [CreateAssetMenu(fileName = "NewPlayerClass", menuName = "IdleTime/Player Class")]
    public class PlayerClass : ScriptableObject
    {
        public string className = "Beginner";

        [Header("Combat Role")]
        [Tooltip("Which primary stat scales this class's attack damage.")]
        public PrimaryStat damageStat = PrimaryStat.Str;
        [Tooltip("Which primary stat scales this class's accuracy.")]
        public PrimaryStat accuracyStat = PrimaryStat.Dex;

        [Header("Base Stats at Level 1")]
        public int baseHP = 100;
        public int baseMP = 50;
        public int baseStr = 5;
        [FormerlySerializedAs("baseAgi")] public int baseDex = 5;
        public int baseWis = 5;
        public int baseLuk = 5;

        [Header("Stat Growth per Level")]
        public float hpPerLevel = 20f;
        public float mpPerLevel = 5f;
        public float strPerLevel = 1f;
        [FormerlySerializedAs("agiPerLevel")] public float dexPerLevel = 1f;
        public float wisPerLevel = 1f;
        public float lukPerLevel = 1f;
    }
}
