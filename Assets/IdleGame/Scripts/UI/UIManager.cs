using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject inventoryOverlay;
    [SerializeField] GameObject equipOverlay;
    [SerializeField] GameObject statOverlay;

    void Awake()
    {
        inventoryOverlay.SetActive(false);
        equipOverlay.SetActive(false);
        statOverlay.SetActive(false);
    }

    public void ToggleInventory()
    {
        inventoryOverlay.SetActive(!inventoryOverlay.activeSelf);
    }

    public void ToggleEquipment()
    {
        equipOverlay.SetActive(!equipOverlay.activeSelf);
    }
    public void ToggleStats()
    {
        statOverlay.SetActive(!statOverlay.activeSelf);
    }
}
