using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
