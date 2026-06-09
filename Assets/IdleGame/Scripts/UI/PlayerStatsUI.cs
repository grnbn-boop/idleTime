using UnityEngine;
using TMPro;
using IdleTime.Core;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] TextMeshProUGUI characterNameText;
    [SerializeField] TextMeshProUGUI classNameText;

    [Header("Bars")]
    [SerializeField] StatBar hpBar;
    [SerializeField] StatBar mpBar;

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI strText;
    [SerializeField] TextMeshProUGUI agiText;
    [SerializeField] TextMeshProUGUI wisText;
    [SerializeField] TextMeshProUGUI lukText;

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

        if (strText != null) strText.text = c.Str.ToString();
        if (agiText != null) agiText.text = c.Agi.ToString();
        if (wisText != null) wisText.text = c.Wis.ToString();
        if (lukText != null) lukText.text = c.Luk.ToString();
    }
}
