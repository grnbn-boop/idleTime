using UnityEngine;
using UnityEngine.InputSystem;
using IdleTime.UI;

namespace IdleTime.Core
{
    // Developer-only runtime commands for flushing persisted state and seeding test
    // gear so bugs can be reproduced from a known-clean start. Drop this on any
    // always-present object (e.g. the same GameObject as Inventory / SaveManager).
    //
    // Why this exists: quitting Play Mode auto-saves the live inventory + equipment
    // (SaveManager.OnApplicationQuit), so a broken arrangement — or stale data left
    // over from the 16:9 resolution change — survives reloads. Flushing the live state
    // and re-saving writes a clean slate that the next session loads from.
    public class DebugCommands : MonoBehaviour
    {
        [Header("Hotkeys (editor / development builds only)")]
        [Tooltip("Empties every inventory slot.")]
        [SerializeField] Key flushInventoryKey = Key.F9;

        [Tooltip("Strips the active character's equipment.")]
        [SerializeField] Key flushEquipmentKey = Key.F8;

        [Tooltip("Force-equips the Test Gear list below onto the active character.")]
        [SerializeField] Key forceEquipKey = Key.F7;

        [Tooltip("Dumps the live visual state of every inventory + equipment slot (why an icon won't draw).")]
        [SerializeField] Key dumpVisualsKey = Key.F6;

        [Header("Force-equip set")]
        [Tooltip("Items slammed straight into their slots by the force-equip command. " +
                 "Assign one item per equip slot you want to test.")]
        [SerializeField] ItemDefinition[] testGear;

        [Header("Options")]
        [Tooltip("Persist the new state to disk immediately after each command.")]
        [SerializeField] bool saveAfterChange = true;

        void Update()
        {
            // Gate to editor + dev builds so the keys can't fire in a shipped game.
            if (!Debug.isDebugBuild && !Application.isEditor) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[flushInventoryKey].wasPressedThisFrame) FlushInventory();
            if (kb[flushEquipmentKey].wasPressedThisFrame) FlushEquipment();
            if (kb[forceEquipKey].wasPressedThisFrame)      ForceEquipTestGear();
            if (kb[dumpVisualsKey].wasPressedThisFrame)     DumpSlotVisuals();
        }

        // ── Diagnostics ───────────────────────────────────────────────────────

        // Logs the live Image state of every slot so we can see why an icon isn't
        // drawing. Open the inventory + equipment panels first so their slots are
        // active (inactive panels report active=False).
        [ContextMenu("Dump Slot Visuals")]
        public void DumpSlotVisuals()
        {
            var invSlots = FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var eqSlots  = FindObjectsByType<EquipmentSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[Debug] Dumping {invSlots.Length} inventory + {eqSlots.Length} equipment slot visuals " +
                      "(open both panels first so slots are active):");
            foreach (var s in invSlots) s.DebugDumpVisual();
            foreach (var e in eqSlots)  e.DebugDumpVisual();
        }

        // ── Inventory ─────────────────────────────────────────────────────────

        [ContextMenu("Flush Inventory")]
        public void FlushInventory()
        {
            if (Inventory.Instance == null) { Debug.LogWarning("[Debug] No Inventory instance — are you in Play Mode?"); return; }
            Inventory.Instance.Clear();
            Persist("inventory flushed");
        }

        // ── Equipment ─────────────────────────────────────────────────────────

        [ContextMenu("Flush Equipment")]
        public void FlushEquipment()
        {
            var character = ActiveCharacterOrWarn();
            if (character == null) return;
            EquipmentManager.Instance?.ClearAll(character);
            Persist("equipment flushed");
        }

        [ContextMenu("Force-Equip Test Gear")]
        public void ForceEquipTestGear()
        {
            var character = ActiveCharacterOrWarn();
            if (character == null) return;
            if (EquipmentManager.Instance == null) { Debug.LogWarning("[Debug] No EquipmentManager in scene."); return; }
            if (testGear == null || testGear.Length == 0) { Debug.LogWarning("[Debug] Test Gear list is empty — assign items in the Inspector."); return; }

            int equipped = 0;
            foreach (var item in testGear)
            {
                if (item == null) continue;
                EquipmentManager.Instance.ForceEquip(item, character);
                equipped++;
            }
            Persist($"force-equipped {equipped} test item(s)");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        CharacterData ActiveCharacterOrWarn()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c == null) Debug.LogWarning("[Debug] No active character — are you in Play Mode?");
            return c;
        }

        void Persist(string what)
        {
            if (saveAfterChange) SaveManager.Instance?.SaveAll();
            Debug.Log($"[Debug] {what}{(saveAfterChange ? " and saved." : ".")}");
        }
    }
}
