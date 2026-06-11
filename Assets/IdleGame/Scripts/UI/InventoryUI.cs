using UnityEngine;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] InventorySlotUI[] slots;

        void Awake()
        {
            Debug.Log($"[InventoryUI] Awake — initialising {slots.Length} slot indices");
            for (int i = 0; i < slots.Length; i++)
                slots[i].Init(i);
        }

        void OnEnable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.OnInventoryChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.OnInventoryChanged -= Refresh;
        }

        void Refresh()
        {
            if (Inventory.Instance == null) { Debug.LogWarning("[InventoryUI] Refresh skipped — Inventory.Instance is null"); return; }
            Debug.Log($"[InventoryUI] Refresh — repainting {slots.Length} slots");
            for (int i = 0; i < slots.Length; i++)
                slots[i].Refresh(Inventory.Instance.GetSlot(i));
        }
    }
}
