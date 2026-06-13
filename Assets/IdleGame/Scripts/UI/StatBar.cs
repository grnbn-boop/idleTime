using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A passive "current / max" bar: a filled Image plus an optional value label. It owns
// no state and reads no backend — a UI panel (e.g. PlayerStatsUI) calls SetValues(...)
// after a stats event and the bar just reflects it (fillAmount + "cur / max" text).
// Reused for the HP, MP, and XP bars; the fill colour is set per-instance in the Inspector.
public class StatBar : MonoBehaviour
{
    [SerializeField] Image fillImage;
    [SerializeField] TextMeshProUGUI valueText;
    [SerializeField] Color fillColor = Color.white;

    void Awake()
    {
        if (fillImage != null)
            fillImage.color = fillColor;
    }

    public void SetValues(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = max > 0 ? current / max : 0f;
        if (valueText != null)
            valueText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }
}
