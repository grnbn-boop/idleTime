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
            bool stackable = item.equipSlot == EquipSlot.None;

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

        // Places item directly into a specific slot (overwrites). Used by equipment drag.
        public void SetSlot(int index, ItemDefinition item, int count = 1)
        {
            if (index < 0 || index >= MaxSlots) return;
            _slots[index].item  = item;
            _slots[index].count = count;
            OnInventoryChanged?.Invoke();
        }

        // Moves item from → to. If target is occupied the two slots swap.
        public void SwapSlots(int from, int to)
        {
            if (from < 0 || from >= MaxSlots || to < 0 || to >= MaxSlots || from == to) return;
            var tmp = (_slots[to].item, _slots[to].count);
            _slots[to].item    = _slots[from].item;
            _slots[to].count   = _slots[from].count;
            _slots[from].item  = tmp.item;
            _slots[from].count = tmp.count;
            OnInventoryChanged?.Invoke();
        }

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
