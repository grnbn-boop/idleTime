using System;
using System.Collections.Generic;
using IdleTime.Core;
using IdleTime.UI;
using UnityEngine;

namespace IdleTime.Interactions
{
    /// <summary>
    /// A vendor NPC. Clicking it (optionally after an intro line or two) opens the
    /// screen-space shop overlay populated from this shop's own stock list. Buy
    /// prices are per-entry; the sell price for right-click selling is derived from
    /// the item's baseValue × <see cref="sellMultiplier"/>. Stock is infinite — buying
    /// never depletes an entry.
    /// </summary>
    public class ShopNpc : NpcDialogue
    {
        [Serializable]
        public class ShopEntry
        {
            public ItemDefinition item;
            [Tooltip("Gold cost to buy one. Leave 0 to fall back to the item's baseValue.")]
            public int price;
        }

        [Header("Shop")]
        [SerializeField] private string shopTitle = "Shop";
        [SerializeField] private ShopEntry[] stock = Array.Empty<ShopEntry>();
        [Tooltip("Fraction of an item's baseValue paid out when the player sells it here.")]
        [Range(0f, 1f)]
        [SerializeField] private float sellMultiplier = 0.5f;

        [Tooltip("World-space offset from this NPC's origin to where the shop overlay's bottom " +
                 "edge sits — raise Y to float it higher above the shopkeeper's head.")]
        [SerializeField] private Vector2 overlayWorldOffset = new Vector2(0f, 3.5f);

        public string Title => shopTitle;
        public IReadOnlyList<ShopEntry> Stock => stock;

        // World point the shop overlay anchors its bottom edge to (above the NPC's head).
        public Vector3 OverlayWorldAnchor => transform.position + (Vector3)overlayWorldOffset;

        // Buy price: the entry's explicit price, or the item's baseValue when left at 0.
        public int GetBuyPrice(ShopEntry entry)
        {
            if (entry == null) return 0;
            return entry.price > 0 ? entry.price : (entry.item != null ? entry.item.baseValue : 0);
        }

        // Sell price for an arbitrary item the player offers (not limited to stock).
        public int GetSellPrice(ItemDefinition item) =>
            item != null ? Mathf.FloorToInt(item.baseValue * sellMultiplier) : 0;

        public override void Interact()
        {
            // Clicking an open shop closes it.
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpenFor(this))
            {
                ShopUI.Instance.Close();
                return;
            }

            // Intro lines (if any) play first; the shop opens when they finish.
            if (lines != null && lines.Length > 0)
            {
                base.Interact();
                return;
            }

            OpenShop();
        }

        protected override void OnDialogueFinished() => OpenShop();

        private void OpenShop() => ShopUI.Ensure().Open(this);
    }
}
