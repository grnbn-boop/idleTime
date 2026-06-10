using System;
using UnityEngine;

namespace IdleTime.Core
{
    [Serializable]
    public class EquipmentSlots
    {
        public ItemDefinition helmet;
        public ItemDefinition chest;
        public ItemDefinition legs;
        public ItemDefinition mainHand;
        public ItemDefinition offHand;
        public ItemDefinition ring;
        public ItemDefinition necklace;

        public ItemDefinition Get(EquipSlot slot) => slot switch
        {
            EquipSlot.Helmet   => helmet,
            EquipSlot.Chest    => chest,
            EquipSlot.Legs     => legs,
            EquipSlot.MainHand => mainHand,
            EquipSlot.OffHand  => offHand,
            EquipSlot.Ring     => ring,
            EquipSlot.Necklace => necklace,
            _                  => null,
        };

        public void Set(EquipSlot slot, ItemDefinition item)
        {
            switch (slot)
            {
                case EquipSlot.Helmet:   helmet   = item; break;
                case EquipSlot.Chest:    chest    = item; break;
                case EquipSlot.Legs:     legs     = item; break;
                case EquipSlot.MainHand: mainHand = item; break;
                case EquipSlot.OffHand:  offHand  = item; break;
                case EquipSlot.Ring:     ring     = item; break;
                case EquipSlot.Necklace: necklace = item; break;
            }
        }

        public bool IsEmpty(EquipSlot slot) => Get(slot) == null;
    }
}
