using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text countLabel;

        public void Refresh(InventorySlot slot)
        {
            if (slot == null) { Debug.LogError($"[InventorySlotUI] slot is null on {gameObject.name}"); return; }
            if (iconImage == null) { Debug.LogError($"[InventorySlotUI] iconImage not wired on {gameObject.name}"); return; }
            if (countLabel == null) { Debug.LogError($"[InventorySlotUI] countLabel not wired on {gameObject.name}"); return; }

            if (!slot.IsEmpty)
            {
                iconImage.sprite = slot.item.icon;
                iconImage.color = Color.white;
                countLabel.text = slot.count > 1 ? slot.count.ToString() : "";
                countLabel.enabled = slot.count > 1;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
                countLabel.enabled = false;
            }
        }
    }
}
