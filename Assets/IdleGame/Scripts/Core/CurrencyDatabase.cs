using UnityEngine;

namespace IdleTime.Core
{
    // Ordered list of coin denominations (Copper, Silver, Gold, Platinum…). A gold
    // drop of an arbitrary amount renders as the single highest-value coin that fits,
    // so 112 gold shows the Silver coin (100) rather than 112 individual coppers.
    [CreateAssetMenu(fileName = "CurrencyDatabase", menuName = "IdleTime/Currency Database")]
    public class CurrencyDatabase : ScriptableObject
    {
        [Tooltip("Coin items, any order — each must have a currencyValue > 0. " +
                 "HighestDenominationFor picks the largest whose value <= the amount.")]
        public ItemDefinition[] denominations;

        // The coin to display for `amount` gold: the highest denomination that doesn't
        // exceed it. Falls back to the smallest coin for amounts below every denomination
        // (e.g. a stray 0), and null if the table is empty/unconfigured.
        public ItemDefinition HighestDenominationFor(int amount)
        {
            ItemDefinition best = null;
            ItemDefinition smallest = null;

            if (denominations != null)
            {
                foreach (var coin in denominations)
                {
                    if (coin == null || coin.currencyValue <= 0) continue;

                    if (smallest == null || coin.currencyValue < smallest.currencyValue)
                        smallest = coin;

                    if (coin.currencyValue <= amount &&
                        (best == null || coin.currencyValue > best.currencyValue))
                        best = coin;
                }
            }

            return best != null ? best : smallest;
        }
    }
}
