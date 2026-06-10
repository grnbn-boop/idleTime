using UnityEngine;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class EquipmentUI : MonoBehaviour
    {
        [SerializeField] EquipmentSlotUI helmetSlot;
        [SerializeField] EquipmentSlotUI chestSlot;
        [SerializeField] EquipmentSlotUI legsSlot;
        [SerializeField] EquipmentSlotUI mainHandSlot;
        [SerializeField] EquipmentSlotUI offHandSlot;
        [SerializeField] EquipmentSlotUI ringSlot;
        [SerializeField] EquipmentSlotUI necklaceSlot;

        void OnEnable()
        {
            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.OnEquipmentChanged += Refresh;
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnActiveCharacterChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.OnEquipmentChanged -= Refresh;
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.OnActiveCharacterChanged -= Refresh;
        }

        void Refresh()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c == null) return;

            helmetSlot?.Refresh(c.equipment.Get(EquipSlot.Helmet));
            chestSlot?.Refresh(c.equipment.Get(EquipSlot.Chest));
            legsSlot?.Refresh(c.equipment.Get(EquipSlot.Legs));
            mainHandSlot?.Refresh(c.equipment.Get(EquipSlot.MainHand));
            offHandSlot?.Refresh(c.equipment.Get(EquipSlot.OffHand));
            ringSlot?.Refresh(c.equipment.Get(EquipSlot.Ring));
            necklaceSlot?.Refresh(c.equipment.Get(EquipSlot.Necklace));
        }
    }
}
