using UnityEngine;
using UnityEngine.UI;
using IdleTime.Core;

namespace IdleTime.UI
{
    public class ItemDragManager : MonoBehaviour
    {
        public static ItemDragManager Instance { get; private set; }

        [SerializeField] Canvas rootCanvas;

        Image _ghost;

        public ItemDefinition DraggedItem     { get; private set; }
        public int            SourceSlotIndex { get; private set; } = -1;
        public EquipSlot      SourceEquipSlot { get; private set; } = EquipSlot.None;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void BeginDragFromEquipment(ItemDefinition item, EquipSlot sourceSlot)
        {
            DraggedItem     = item;
            SourceSlotIndex = -1;
            SourceEquipSlot = sourceSlot;
            CreateGhost(item);
        }

        public void BeginDrag(ItemDefinition item, int inventorySlotIndex)
        {
            DraggedItem     = item;
            SourceSlotIndex = inventorySlotIndex;
            SourceEquipSlot = EquipSlot.None;

            CreateGhost(item);
        }

        void CreateGhost(ItemDefinition item)
        {
            if (rootCanvas == null) { Debug.LogError("[ItemDragManager] rootCanvas is not assigned!"); return; }
            _ghost = new GameObject("DragGhost", typeof(Image)).GetComponent<Image>();
            _ghost.transform.SetParent(rootCanvas.transform, false);
            _ghost.transform.SetAsLastSibling();
            _ghost.sprite        = item.icon;
            _ghost.raycastTarget = false;
            ((RectTransform)_ghost.transform).sizeDelta = new Vector2(48, 48);
            Debug.Log($"[ItemDragManager] Ghost created for '{item.itemName}', canvas='{rootCanvas.name}'");
        }

        public void MoveGhost(Vector2 screenPosition)
        {
            if (_ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rootCanvas.transform,
                screenPosition,
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                out Vector2 local);
            ((RectTransform)_ghost.transform).anchoredPosition = local;
        }

        public void EndDrag()
        {
            DraggedItem     = null;
            SourceSlotIndex = -1;
            SourceEquipSlot = EquipSlot.None;
            if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }
        }
    }
}
