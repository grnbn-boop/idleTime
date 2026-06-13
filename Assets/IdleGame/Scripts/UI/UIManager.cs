using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject inventoryOverlay;
    [SerializeField] GameObject equipOverlay;
    [SerializeField] GameObject statOverlay;
    [SerializeField] GameObject skillOverlay;

    // The toolbar buttons that toggle each overlay, matched by GameObject name. These
    // live in the UI prefab while this UIManager is a separate scene object — and a
    // prefab can't serialize a reference back to a scene object, so a hand-wired onClick
    // breaks every time the prefab is re-dragged. Wiring in code (the same self-building
    // convention as PortalNavHUD / TooltipManager / MenuUIBuilder) makes the link
    // immune to that: the inspector onClick lists can stay empty.
    // Public so the editor's Check Scene Rig can validate these buttons exist by name
    // (it wires nothing — it just confirms the names this code depends on are present).
    public const string InventoryButtonName = "Inv_Button";
    public const string EquipmentButtonName = "Equip_Button";
    public const string SkillsButtonName    = "Skills_Button";
    public const string StatsButtonName     = "Stats_Button";
    const string SkillsCloseButton          = "Close";   // the close button inside the skills overlay

    void Awake()
    {
        inventoryOverlay.SetActive(false);
        equipOverlay.SetActive(false);
        statOverlay.SetActive(false);
        if (skillOverlay != null) skillOverlay.SetActive(false);

        WireButtons();
    }

    // ── Button wiring ─────────────────────────────────────────────────────────

    void WireButtons()
    {
        Wire(FindButton(InventoryButtonName), ToggleInventory, InventoryButtonName);
        Wire(FindButton(EquipmentButtonName), ToggleEquipment, EquipmentButtonName);
        Wire(FindButton(SkillsButtonName),    ToggleSkills,    SkillsButtonName);
        Wire(FindButton(StatsButtonName),     ToggleStats,     StatsButtonName);

        // The skills overlay's own Close button. Scope the lookup to the overlay so we
        // don't grab a same-named "Close" from another panel.
        if (skillOverlay != null)
            Wire(FindButtonIn(skillOverlay, SkillsCloseButton), ToggleSkills, $"{SkillsCloseButton} (skills)");
    }

    void Wire(Button button, UnityAction action, string label)
    {
        if (button == null)
        {
            Debug.LogWarning($"[UIManager] Button '{label}' not found in scene — overlay toggle won't work.");
            return;
        }

        // Drop any stale editor-authored persistent calls (their target is the now-missing
        // prefab reference) so only our code listener fires, and guard against a double-add.
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // Scene-wide search including inactive objects: the toolbar buttons live outside this
    // GameObject's hierarchy (separate UI canvas root), and overlay buttons start inactive.
    static Button FindButton(string name)
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include))
            if (b.name == name) return b;
        return null;
    }

    static Button FindButtonIn(GameObject scope, string name)
    {
        foreach (var b in scope.GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return null;
    }

    // ── Toggles ───────────────────────────────────────────────────────────────

    public void ToggleInventory()  => Toggle(inventoryOverlay, "inventory");
    public void ToggleEquipment()  => Toggle(equipOverlay, "equipment");
    public void ToggleStats()      => Toggle(statOverlay, "stats");
    public void ToggleSkills()     => Toggle(skillOverlay, "skills");

    void Toggle(GameObject overlay, string label)
    {
        if (overlay == null)
        {
            Debug.LogWarning($"[UIManager] Toggle '{label}': overlay reference is NULL — assign it on the UIManager.");
            return;
        }
        overlay.SetActive(!overlay.activeSelf);
    }
}
