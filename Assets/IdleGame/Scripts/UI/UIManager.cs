using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject inventoryOverlay;
    [SerializeField] GameObject equipOverlay;

    void Awake()
    {
        inventoryOverlay.SetActive(false);
        equipOverlay.SetActive(false);
    }

    public void ToggleInventory()
    {
        inventoryOverlay.SetActive(!inventoryOverlay.activeSelf);
    }

    public void ToggleEquipment()
    {
        equipOverlay.SetActive(!equipOverlay.activeSelf);
    }
}
