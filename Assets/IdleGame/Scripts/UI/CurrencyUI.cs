using UnityEngine;
using TMPro;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Drives the "Currency > current" label with the active character's gold.
    // Mirrors how PlayerStatsUI feeds the Level label, but stands alone so it can
    // live on the always-on HUD rather than the toggled stats overlay.
    public class CurrencyUI : MonoBehaviour
    {
        [Tooltip("The 'current' text under the Currency label; shows the active character's gold.")]
        [SerializeField] TextMeshProUGUI goldText;

        void Start()
        {
            var pm = PlayerManager.Instance;
            if (pm != null)
            {
                pm.OnGoldChanged += Refresh;
                pm.OnActiveCharacterChanged += Refresh;   // switching characters swaps the gold total
            }
            Refresh();
        }

        void OnDestroy()
        {
            var pm = PlayerManager.Instance;
            if (pm == null) return;
            pm.OnGoldChanged -= Refresh;
            pm.OnActiveCharacterChanged -= Refresh;
        }

        void Refresh()
        {
            if (goldText == null) return;
            goldText.text = (PlayerManager.Instance?.Gold ?? 0).ToString("N0");
        }
    }
}
