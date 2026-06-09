using System;
using UnityEngine;

namespace IdleTime.Core
{
    [Serializable]
    public class InventorySlot
    {
        public ItemDefinition item;
        public int count;
        public bool IsEmpty => item == null;
    }

    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        public const int MaxSlots = 16;

        InventorySlot[] _slots;

        public event Action OnInventoryChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _slots = new InventorySlot[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
                _slots[i] = new InventorySlot();
        }

        public bool AddItem(ItemDefinition item)
        {
            bool stackable = item.itemType != ItemType.Weapon && item.itemType != ItemType.Armor;

            if (stackable)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_slots[i].IsEmpty && _slots[i].item == item)
                    {
                        _slots[i].count++;
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }

            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].item = item;
                    _slots[i].count = 1;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public bool RemoveAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots || _slots[slotIndex].IsEmpty) return false;
            _slots[slotIndex].item = null;
            _slots[slotIndex].count = 0;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public InventorySlot GetSlot(int index) => _slots[index];

        public bool IsFull
        {
            get
            {
                for (int i = 0; i < MaxSlots; i++)
                    if (_slots[i].IsEmpty) return false;
                return true;
            }
        }
    }
}
