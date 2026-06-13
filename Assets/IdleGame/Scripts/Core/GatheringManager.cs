using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleTime.Core
{
    public enum GatherOutcome { Failed, Success }

    // The global "brain" for gathering. Unlike the GameSystems-prefab managers it needs
    // no inspector wiring (it loads its tuning from Resources/Gathering or falls back to
    // code defaults), so it self-bootstraps as its own DontDestroyOnLoad singleton at
    // launch — same RuntimeInitializeOnLoadMethod approach GameBootstrap uses for the rig.
    public class GatheringManager : MonoBehaviour
    {
        public static GatheringManager Instance { get; private set; }

        [SerializeField] private GatheringSkillDefinition[] definitions;

        // Fired whenever a character's gathering level/XP changes, so the stats UI refreshes.
        public static event Action OnGatheringChanged;

        const string ResourcesSubfolder = "Gathering";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;   // already placed in a scene/prefab
            var go = new GameObject("GatheringManager");
            go.AddComponent<GatheringManager>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            // Dedup on gameObject (not transform.root): this manager is its own root when
            // self-bootstrapped, and if someone later drops it onto GameSystems it's a
            // child whose own GameObject is safe to destroy — never the whole rig.
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (definitions == null || definitions.Length == 0)
                definitions = Resources.LoadAll<GatheringSkillDefinition>(ResourcesSubfolder);
            if (definitions == null || definitions.Length == 0)
                definitions = BuildDefaultDefinitions();
        }

        public IReadOnlyList<GatheringSkillDefinition> Definitions => definitions;

        public GatheringSkillDefinition GetDefinition(GatheringSkillType type)
        {
            if (definitions == null) return null;
            foreach (var d in definitions)
                if (d != null && d.type == type) return d;
            return null;
        }

        // Clamped chance a single gather attempt succeeds: the node's base chance plus the
        // buff stat and skill level contributions from the definition.
        public float SuccessChance(CharacterData c, GatheringSkillDefinition def, float nodeBaseChance)
        {
            if (def == null || c == null) return Mathf.Clamp01(nodeBaseChance);
            int stat = StatValue(c, def.buffStat);
            int level = c.gathering.GetLevel(def.type);
            float chance = nodeBaseChance + stat * def.statWeight + (level - 1) * def.levelWeight;
            return Mathf.Clamp(chance, def.minSuccessChance, def.maxSuccessChance);
        }

        // Rolls one gather attempt and grants XP on a winning roll. The reward item is NOT
        // added to the inventory here: on success the caller pops it out of the node as a
        // floor drop (a WorldItem, same as monster loot), so a full bag never cancels the
        // pull.
        public GatherOutcome TryGather(CharacterData c, GatheringSkillType type, float nodeBaseChance)
        {
            if (c == null) return GatherOutcome.Failed;

            var def = GetDefinition(type);
            float chance = SuccessChance(c, def, nodeBaseChance);
            if (UnityEngine.Random.value > chance) return GatherOutcome.Failed;

            if (def != null) c.gathering.AddXp(type, def.xpPerGather, def);
            OnGatheringChanged?.Invoke();
            return GatherOutcome.Success;
        }

        static int StatValue(CharacterData c, PrimaryStat stat) => stat switch
        {
            PrimaryStat.Str => c.Str,
            PrimaryStat.Dex => c.Dex,
            PrimaryStat.Wis => c.Wis,
            PrimaryStat.Luk => c.Luk,
            _               => 0,
        };

        // Code fallback so gathering works before any .asset definitions are authored.
        // Woodcutting→Wis, Mining→Str, Crafting→Dex (Crafting flagged as a stub).
        static GatheringSkillDefinition[] BuildDefaultDefinitions()
        {
            return new[]
            {
                MakeDefault(GatheringSkillType.Woodcutting, "Woodcutting", PrimaryStat.Wis, new Color(0.35f, 0.65f, 0.25f)),
                MakeDefault(GatheringSkillType.Mining,      "Mining",      PrimaryStat.Str, new Color(0.6f, 0.6f, 0.66f)),
                MakeDefault(GatheringSkillType.Crafting,    "Crafting",    PrimaryStat.Dex, new Color(0.65f, 0.5f, 0.3f), stub: true),
            };
        }

        static GatheringSkillDefinition MakeDefault(GatheringSkillType type, string name, PrimaryStat stat, Color color, bool stub = false)
        {
            var d = ScriptableObject.CreateInstance<GatheringSkillDefinition>();
            d.name = name + "Skill";
            d.type = type;
            d.displayName = name;
            d.buffStat = stat;
            d.placeholderColor = color;
            d.isStub = stub;
            return d;
        }
    }
}
