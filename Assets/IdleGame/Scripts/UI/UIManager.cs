using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject inventoryOverlay;
    [SerializeField] GameObject equipOverlay;
    [SerializeField] GameObject statOverlay;
    [SerializeField] GameObject skillOverlay;

    void Awake()
    {
        inventoryOverlay.SetActive(false);
        equipOverlay.SetActive(false);
        statOverlay.SetActive(false);
        if (skillOverlay != null) skillOverlay.SetActive(false);
    }

    public void ToggleInventory()  => inventoryOverlay.SetActive(!inventoryOverlay.activeSelf);
    public void ToggleEquipment()  => equipOverlay.SetActive(!equipOverlay.activeSelf);
    public void ToggleStats()      => statOverlay.SetActive(!statOverlay.activeSelf);
    public void ToggleSkills()     => skillOverlay?.SetActive(!skillOverlay.activeSelf);
}
