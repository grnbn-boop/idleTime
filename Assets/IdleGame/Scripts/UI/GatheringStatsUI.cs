using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Optional self-building "Skill Stats" page: one row per gathering skill showing name, level
    // and an XP bar, built from the live GatheringManager definitions. Standalone (no prefab
    // wiring). This is the auto-built fallback; the hand-authored flow uses GatheringSkillStatRow
    // + StatBar on prefab rows instead.
    public class GatheringStatsUI : MonoBehaviour
    {
        private class Row
        {
            public GatheringSkillType type;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI levelText;
            public Image fill;
            public TextMeshProUGUI xpText;
        }

        private readonly List<Row> rows = new();
        private bool built;

        void OnEnable()
        {
            EnsureBuilt();
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

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            var layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var defs = GatheringManager.Instance?.Definitions;
            if (defs == null) return;
            foreach (var def in defs)
                if (def != null) rows.Add(BuildRow(def));
        }

        private Row BuildRow(GatheringSkillDefinition def)
        {
            var rowGO = new GameObject(def.displayName + "Row", typeof(RectTransform));
            rowGO.transform.SetParent(transform, false);
            rowGO.AddComponent<LayoutElement>().preferredHeight = 56f;

            var v = rowGO.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            // Top line: "Woodcutting            Lv 3"
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(rowGO.transform, false);
            var h = header.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true;

            var nameText = MakeText(header.transform, def.displayName + (def.isStub ? "  (wip)" : ""), 24f, TextAlignmentOptions.MidlineLeft);
            var levelText = MakeText(header.transform, "Lv 1", 24f, TextAlignmentOptions.MidlineRight);

            // XP bar.
            var barGO = new GameObject("XPBar", typeof(RectTransform));
            barGO.transform.SetParent(rowGO.transform, false);
            barGO.AddComponent<LayoutElement>().preferredHeight = 16f;
            var barBg = barGO.AddComponent<Image>();
            barBg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(barGO.transform, false);
            var fillRt = (RectTransform)fillGO.transform;
            // Anchor-driven fill: anchorMax.x is the fraction (set in Refresh), so it tracks
            // the bar width automatically and is correct on the very first layout pass.
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fill = fillGO.AddComponent<Image>();
            fill.color = new Color(0.35f, 0.65f, 0.95f, 1f);

            var xpText = MakeText(barGO.transform, "0 / 0", 13f, TextAlignmentOptions.Center);
            xpText.raycastTarget = false;
            var xpRt = (RectTransform)xpText.transform;
            xpRt.anchorMin = Vector2.zero; xpRt.anchorMax = Vector2.one;
            xpRt.offsetMin = xpRt.offsetMax = Vector2.zero;

            return new Row { type = def.type, nameText = nameText, levelText = levelText, fill = fill, xpText = xpText };
        }

        private static TextMeshProUGUI MakeText(Transform parent, string content, float size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            return t;
        }

        private void Refresh()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            var mgr = GatheringManager.Instance;
            if (c == null || mgr == null) return;

            foreach (var row in rows)
            {
                var def = mgr.GetDefinition(row.type);
                int level = c.gathering.GetLevel(row.type);
                float xp = c.gathering.GetXp(row.type);
                int next = def != null ? def.XpToNext(level) : 0;
                bool maxed = def != null && level >= def.maxLevel;

                row.levelText.text = $"Lv {level}";
                if (maxed)
                {
                    if (row.fill != null) SetFill(row.fill, 1f);
                    row.xpText.text = "MAX";
                }
                else
                {
                    float frac = next > 0 ? Mathf.Clamp01(xp / next) : 0f;
                    if (row.fill != null) SetFill(row.fill, frac);
                    row.xpText.text = $"{Mathf.FloorToInt(xp)} / {next}";
                }
            }
        }

        // Drives the fill width via its right anchor, independent of the measured bar width.
        private static void SetFill(Image fill, float frac)
        {
            var rt = (RectTransform)fill.transform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
