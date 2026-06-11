using UnityEngine;
using UnityEngine.UI;
using IdleTime.Player;

namespace IdleTime.UI
{
    // Drives the Auto Attack toggle button (UI > BG > Auto_Attack). The BG stays put;
    // this only swaps the icon's sprite to reflect whether auto-attack is engaged and
    // forwards clicks to PlayerAttack. Add a TooltipTrigger alongside this for the hover
    // label — see the field note below.
    [RequireComponent(typeof(Button))]
    public class AutoAttackButton : MonoBehaviour
    {
        [Tooltip("The PlayerAttack whose auto-attack mode this button toggles. Found automatically if left empty.")]
        [SerializeField] private PlayerAttack playerAttack;
        [Tooltip("The Auto_Attack icon Image whose sprite swaps with state (NOT the BG image).")]
        [SerializeField] private Image iconImage;
        [Tooltip("Icon shown while auto-attack is ON.")]
        [SerializeField] private Sprite enabledSprite;
        [Tooltip("Icon shown while auto-attack is OFF.")]
        [SerializeField] private Sprite disabledSprite;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (iconImage == null) iconImage = GetComponent<Image>();
            if (playerAttack == null) playerAttack = FindAnyObjectByType<PlayerAttack>();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClicked);
            RefreshIcon();
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            if (playerAttack == null) return;
            playerAttack.ToggleAutoAttack();
            RefreshIcon();
        }

        // Keeps the icon in sync with the actual mode — also covers the case where the
        // button is re-enabled or auto-attack was changed elsewhere.
        private void RefreshIcon()
        {
            if (iconImage == null) return;
            bool on = playerAttack != null && playerAttack.AutoAttackEnabled;
            Sprite next = on ? enabledSprite : disabledSprite;
            if (next != null) iconImage.sprite = next;
        }
    }
}
