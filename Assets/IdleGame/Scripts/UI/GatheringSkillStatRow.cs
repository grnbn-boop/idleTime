using UnityEngine;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Drop this on a single authored skill row to keep it live. It reuses the project's StatBar
    // widget (the same one the HUD HP/MP/XP bars use) for the XP bar — wire the row's StatBar and
    // optionally a name/level label. It subscribes to gathering/character changes and pushes the
    // skill's level + XP into the bar via StatBar.SetValues(xp, next).
    //
    // To add more skills: duplicate a wired row and change Type.
    public class GatheringSkillStatRow : MonoBehaviour
    {
        [SerializeField] private GatheringSkillType type;
        [Tooltip("Optional — set to the skill's display name on enable (\"  (wip)\" suffix for stubs).")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Optional — shows \"Lv N\".")]
        [SerializeField] private TMP_Text levelText;
        [Tooltip("The row's XP bar — the same StatBar widget the HUD bars use. Drives fill + value text.")]
        [SerializeField] private StatBar bar;

        public GatheringSkillType Type { get => type; set => type = value; }

        void OnEnable()
        {
            GatheringManager.OnGatheringChanged += Refresh;
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnActiveCharacterChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            GatheringManager.OnGatheringChanged -= Refresh;
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnActiveCharacterChanged -= Refresh;
        }

        public void Refresh()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            var mgr = GatheringManager.Instance;
            if (c == null || mgr == null) return;

            var def = mgr.GetDefinition(type);
            if (nameText != null && def != null)
                nameText.text = def.displayName + (def.isStub ? "  (wip)" : "");

            int level = c.gathering.GetLevel(type);
            float xp = c.gathering.GetXp(type);
            int next = def != null ? def.XpToNext(level) : 0;
            bool maxed = def != null && level >= def.maxLevel;

            if (levelText != null) levelText.text = $"Level: {level}";
            if (bar != null)
            {
                // At max level there's no "next" — fill the bar fully.
                if (maxed) bar.SetValues(1f, 1f);
                else       bar.SetValues(xp, next);
            }
        }
    }
}
