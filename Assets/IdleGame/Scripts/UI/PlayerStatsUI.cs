using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using IdleTime.Core;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] TextMeshProUGUI characterNameText;
    [SerializeField] TextMeshProUGUI classNameText;

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
        if (PlayerManager.Instance == null) return;
        PlayerManager.Instance.OnActiveCharacterChanged += Refresh;
        PlayerManager.Instance.OnStatsChanged += Refresh;
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
}
