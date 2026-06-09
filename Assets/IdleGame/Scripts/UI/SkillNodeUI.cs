using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;

public class SkillNodeUI : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] Image frameImage;
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] Button button;

    [Header("Frame Colors")]
    [SerializeField] Color lockedColor    = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color availableColor = Color.white;
    [SerializeField] Color unlockedColor  = new Color(0.35f, 0.85f, 0.35f, 1f);
    [SerializeField] Color maxedColor     = new Color(1f, 0.8f, 0.2f, 1f);

    public SkillNodeEntry NodeEntry { get; private set; }
    private System.Action<SkillNodeEntry> _onClick;

    public void Setup(SkillNodeEntry entry, CharacterData character, System.Action<SkillNodeEntry> onClick)
    {
        NodeEntry = entry;
        _onClick = onClick;

        iconImage.sprite = entry.skill.icon;
        iconImage.enabled = entry.skill.icon != null;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClick?.Invoke(NodeEntry));

        Refresh(character);
    }

    public void Refresh(CharacterData character)
    {
        int level    = character.skills.GetLevel(NodeEntry.skill);
        bool maxed   = level >= NodeEntry.skill.maxLevel;
        bool unlocked = level > 0;
        bool canUnlock = !maxed && SkillManager.Instance != null && SkillManager.Instance.CanUnlock(NodeEntry, character);

        levelText.text = $"Lv{level}";

        if (maxed)
            frameImage.color = maxedColor;
        else if (unlocked)
            frameImage.color = unlockedColor;
        else if (canUnlock)
            frameImage.color = availableColor;
        else
            frameImage.color = lockedColor;

        Color icon = iconImage.color;
        icon.a = (unlocked || canUnlock) ? 1f : 0.35f;
        iconImage.color = icon;
    }
}
