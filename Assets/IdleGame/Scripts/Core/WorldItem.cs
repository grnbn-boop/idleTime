using UnityEngine;

namespace IdleTime.Core
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldItem : MonoBehaviour
    {
        [SerializeField] ItemDefinition _startItem;
        ItemDefinition _item;

        void Start()
        {
            if (_startItem != null)
                SetItem(_startItem);
        }

        public void SetItem(ItemDefinition item)
        {
            _item = item;
            GetComponent<SpriteRenderer>().sprite = item.icon;
        }

        void OnMouseDown()
        {
            if (Inventory.Instance == null || _item == null) return;

            if (Inventory.Instance.AddItem(_item))
                Destroy(gameObject);
        }
    }
}
