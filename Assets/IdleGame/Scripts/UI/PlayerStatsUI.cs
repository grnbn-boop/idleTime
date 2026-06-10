using System;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using IdleTime.Core;
using IdleTime.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] TextMeshProUGUI characterNameText;
    [SerializeField] TextMeshProUGUI classNameText;
    [Tooltip("The 'current' text under the Level label; shows the active character's level.")]
    [SerializeField] TextMeshProUGUI levelText;

    [Header("Bars")]
    [SerializeField] StatBar hpBar;
    [SerializeField] StatBar mpBar;
    [SerializeField] StatBar xpBar;

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI strText;
    [FormerlySerializedAs("agiText")]
    [SerializeField] TextMeshProUGUI dexText;
    [SerializeField] TextMeshProUGUI wisText;
    [SerializeField] TextMeshProUGUI lukText;

    [Header("Derived")]
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI accuracyText;
    [SerializeField] TextMeshProUGUI defenseText;

    void Start()
    {
        SetupTooltips();
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnActiveCharacterChanged += Refresh;
            PlayerManager.Instance.OnStatsChanged += Refresh;
        }
        Refresh();
    }

    void OnDestroy()
    {
        if (PlayerManager.Instance == null) return;
        PlayerManager.Instance.OnActiveCharacterChanged -= Refresh;
        PlayerManager.Instance.OnStatsChanged -= Refresh;
    }

    void Refresh()
    {
        var c = PlayerManager.Instance?.ActiveCharacter;
        if (c == null) return;

        if (characterNameText != null) characterNameText.text = c.characterName;
        if (classNameText != null) classNameText.text = c.ClassName;
        if (levelText != null) levelText.text = c.level.ToString();

        hpBar?.SetValues(c.currentHP, c.MaxHP);
        mpBar?.SetValues(c.currentMP, c.MaxMP);
        xpBar?.SetValues(c.currentXP, c.XPToNextLevel);

        if (strText != null)     strText.text     = c.Str.ToString();
        if (dexText != null)     dexText.text     = c.Dex.ToString();
        if (wisText != null)     wisText.text     = c.Wis.ToString();
        if (lukText != null)     lukText.text     = c.Luk.ToString();

        if (attackText != null)   attackText.text   = c.Attack.ToString();
        if (accuracyText != null) accuracyText.text = c.Accuracy.ToString();
        if (defenseText != null)  defenseText.text  = c.Defense.ToString();
    }

    // ── Hover tooltips (base/skill/gear breakdown) ───────────────────────────────

    void SetupTooltips()
    {
        AddTooltip(strText, () => PrimaryBreakdown("Strength",  c => c.BaseStr, c => c.skillBonusStr, c => c.equipBonusStr));
        AddTooltip(dexText, () => PrimaryBreakdown("Dexterity", c => c.BaseDex, c => c.skillBonusDex, c => c.equipBonusDex));
        AddTooltip(wisText, () => PrimaryBreakdown("Wisdom",    c => c.BaseWis, c => c.skillBonusWis, c => c.equipBonusWis));
        AddTooltip(lukText, () => PrimaryBreakdown("Luck",      c => c.BaseLuk, c => c.skillBonusLuk, c => c.equipBonusLuk));

        AddTooltip(attackText,   () => DerivedBreakdown("Attack",   c => c.Attack,   c => c.skillBonusAttack,   c => c.equipBonusAttack,   c => c.playerClass != null ? StatName(c.playerClass.damageStat)   : null));
        AddTooltip(accuracyText, () => DerivedBreakdown("Accuracy", c => c.Accuracy, c => c.skillBonusAccuracy, c => c.equipBonusAccuracy, c => c.playerClass != null ? StatName(c.playerClass.accuracyStat) : null));
        AddTooltip(defenseText,  () => DerivedBreakdown("Defense",  c => c.Defense,  c => c.skillBonusDefense,  c => c.equipBonusDefense,  c => null));
    }

    static void AddTooltip(TextMeshProUGUI text, Func<string> provider)
    {
        if (text == null) return;
        text.raycastTarget = true;   // needed to receive pointer enter/exit
        var trigger = text.GetComponent<TooltipTrigger>();
        if (trigger == null) trigger = text.gameObject.AddComponent<TooltipTrigger>();
        trigger.ContentProvider = provider;
    }

    static string PrimaryBreakdown(string name, Func<CharacterData, int> baseF, Func<CharacterData, int> skillF, Func<CharacterData, int> gearF)
    {
        var c = PlayerManager.Instance?.ActiveCharacter;
        if (c == null) return name;
        int b = baseF(c), s = skillF(c), g = gearF(c);
        var sb = new StringBuilder();
        sb.Append($"<b>{name}</b>\nBase {b}");
        if (s != 0) sb.Append($"\n{Signed(s)} skills");
        if (g != 0) sb.Append($"\n{Signed(g)} gear");
        sb.Append($"\n<b>= {b + s + g}</b>");
        return sb.ToString();
    }

    static string DerivedBreakdown(string name, Func<CharacterData, int> totalF, Func<CharacterData, int> skillF, Func<CharacterData, int> gearF, Func<CharacterData, string> sourceF)
    {
        var c = PlayerManager.Instance?.ActiveCharacter;
        if (c == null) return name;
        int total = totalF(c), s = skillF(c), g = gearF(c);
        int basePart = total - s - g;
        string source = sourceF(c);
        var sb = new StringBuilder();
        sb.Append($"<b>{name}</b>");
        if (source != null) sb.Append($"\nFrom {source} {basePart}");
        else if (basePart != 0) sb.Append($"\nBase {basePart}");
        if (s != 0) sb.Append($"\n{Signed(s)} skills");
        if (g != 0) sb.Append($"\n{Signed(g)} gear");
        sb.Append($"\n<b>= {total}</b>");
        return sb.ToString();
    }

    static string Signed(int v) => (v >= 0 ? "+" : "") + v;

    static string StatName(PrimaryStat s) => s switch
    {
        PrimaryStat.Str => "Strength",
        PrimaryStat.Dex => "Dexterity",
        PrimaryStat.Wis => "Wisdom",
        PrimaryStat.Luk => "Luck",
        _               => s.ToString(),
    };
}
