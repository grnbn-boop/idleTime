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

        // Total backing slots. The inventory UI shows one page (PageSize) at a time and
        // pages through the rest. Doubling this stays save-compatible: persistence is
        // index-based and SetSlot/ApplyInventory bounds-guard, so older 16-slot saves load
        // unchanged into the low slots.
        public const int MaxSlots = 32;

        InventorySlot[] _slots;

        public event Action OnInventoryChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

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

        // Removes a single unit from a stack (for consuming items). Clears the slot
        // when the count reaches zero.
        public bool RemoveOne(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots || _slots[slotIndex].IsEmpty) return false;
            _slots[slotIndex].count--;
            if (_slots[slotIndex].count <= 0)
            {
                _slots[slotIndex].item = null;
                _slots[slotIndex].count = 0;
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        public InventorySlot GetSlot(int index) => _slots[index];

        // Empties every slot in one shot. Debug/maintenance hook — used to flush a
        // persisted bad state and start from a known-clean inventory.
        public void Clear()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i].item  = null;
                _slots[i].count = 0;
            }
            OnInventoryChanged?.Invoke();
            Debug.Log("[Inventory] Cleared all slots.");
        }

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

        // Would AddItem succeed? True for a stackable item that already has a stack to grow,
        // or for anything when at least one slot is free. Lets callers (e.g. a shop purchase)
        // refuse before spending rather than add-then-refund.
        public bool CanAccept(ItemDefinition item)
        {
            if (item == null) return false;

            bool stackable = item.equipSlot == EquipSlot.None;
            if (stackable)
            {
                for (int i = 0; i < MaxSlots; i++)
                    if (!_slots[i].IsEmpty && _slots[i].item == item) return true;
            }

            return !IsFull;
        }
    }
}
