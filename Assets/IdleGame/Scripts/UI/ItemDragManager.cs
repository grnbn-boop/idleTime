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
            WarnIfDragInProgress();
            DraggedItem     = item;
            SourceSlotIndex = -1;
            SourceEquipSlot = sourceSlot;
            CreateGhost(item);
        }

        public void BeginDrag(ItemDefinition item, int inventorySlotIndex)
        {
            WarnIfDragInProgress();
            DraggedItem     = item;
            SourceSlotIndex = inventorySlotIndex;
            SourceEquipSlot = EquipSlot.None;

            CreateGhost(item);
        }

        // A new drag should only ever start from a clean slate. If state is still set,
        // the previous drag's OnEndDrag never ran (e.g. its slot was deactivated mid-drag),
        // which is exactly how a stale ghost gets stranded on the canvas.
        void WarnIfDragInProgress()
        {
            if (DraggedItem != null || _ghost != null)
                Debug.LogWarning($"[ItemDragManager] New drag starting while previous one is unresolved " +
                                 $"(DraggedItem='{(DraggedItem != null ? DraggedItem.itemName : "null")}', ghostAlive={_ghost != null}). " +
                                 $"Previous OnEndDrag was skipped — cleaning up.");
        }

        void CreateGhost(ItemDefinition item)
        {
            if (rootCanvas == null) { Debug.LogError("[ItemDragManager] rootCanvas is not assigned!"); return; }

            // Defensive: never leak a previous ghost. Without this, a skipped OnEndDrag
            // leaves the old ghost alive and a fresh one is layered on top of it.
            if (_ghost != null)
            {
                Debug.LogWarning($"[ItemDragManager] Destroying leaked ghost '{_ghost.sprite?.name}' before creating '{item.itemName}'.");
                Destroy(_ghost.gameObject);
                _ghost = null;
            }

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
            Debug.Log($"[ItemDragManager] EndDrag — clearing '{(DraggedItem != null ? DraggedItem.itemName : "null")}', ghostAlive={_ghost != null}");
            DraggedItem     = null;
            SourceSlotIndex = -1;
            SourceEquipSlot = EquipSlot.None;
            if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }
        }
    }
}
