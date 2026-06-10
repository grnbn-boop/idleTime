using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using IdleTime.Core;
using IdleTime.UI;

public class SkillNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    private CharacterData _character;

    public void Setup(SkillNodeEntry entry, CharacterData character, System.Action<SkillNodeEntry> onClick)
    {
        NodeEntry = entry;
        _onClick = onClick;
        _character = character;

        iconImage.sprite = entry.skill.icon;
        iconImage.enabled = entry.skill.icon != null;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClick?.Invoke(NodeEntry));

        Refresh(character);
    }

#if UNITY_EDITOR
    // Edit-mode preview: show the icon/frame without any runtime character or SkillManager state.
    public void EditorPreview(SkillNodeEntry entry)
    {
        NodeEntry = entry;

        if (iconImage != null)
        {
            iconImage.sprite  = entry.skill != null ? entry.skill.icon : null;
            iconImage.enabled = entry.skill != null && entry.skill.icon != null;
            Color icon = iconImage.color;
            icon.a = 1f;
            iconImage.color = icon;
        }

        if (frameImage != null) frameImage.color = availableColor;
        if (levelText != null)  levelText.text  = "Lv0";
    }
#endif

    public void Refresh(CharacterData character)
    {
        _character = character;
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

    // ── Hover tooltip ───────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (NodeEntry == null) return;
        TooltipManager.Instance?.Show(SkillTooltips.Describe(NodeEntry, _character));
    }

    public void OnPointerExit(PointerEventData eventData) => TooltipManager.Instance?.Hide();

    void OnDisable() => TooltipManager.Instance?.Hide();
}
