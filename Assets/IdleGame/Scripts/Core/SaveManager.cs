using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleTime.Core
{
    // Reads/writes JSON save files: one master file for game-wide state, one file
    // per character. Saves reference assets by name, so gear/inventory can only be
    // restored if the databases below contain every saveable asset — fill them via
    // the component context menu (⋮ → Auto-Fill Databases) after adding items.
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Asset Databases (resolve saved ids back to assets)")]
        [SerializeField] ItemDefinition[] itemDatabase;
        [SerializeField] PlayerClass[] classDatabase;

        public static string SaveFolder => Path.Combine(Application.persistentDataPath, "saves");
        public static string MasterPath => Path.Combine(SaveFolder, "master.json");

        public static string CharacterPath(string characterName) =>
            Path.Combine(SaveFolder, "char_" + Sanitize(characterName) + ".json");

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (itemDatabase == null || itemDatabase.Length == 0)
                Debug.LogWarning("[Save] Item database is empty — saved gear/inventory can't be restored. Right-click the SaveManager component → Auto-Fill Databases.");
        }

        void OnApplicationQuit() => SaveAll();

        // ── Saving ────────────────────────────────────────────────────────────

        [ContextMenu("Save Now")]
        public void SaveAll()
        {
            var pm = PlayerManager.Instance;
            if (pm == null || pm.Characters.Count == 0)
            {
                Debug.Log("[Save] Nothing to save — no PlayerManager/characters (not in Play Mode?).");
                return;
            }

            Directory.CreateDirectory(SaveFolder);

            var now = DateTime.UtcNow;
            var master = new MasterSaveData
            {
                activeIndex = pm.ActiveIndex,
                savedAtUtcTicks = now.Ticks,
                savedAtUtc = now.ToString("u"),
            };
            foreach (var c in pm.Characters)
                master.characterNames.Add(c.characterName);
            WriteJson(MasterPath, master);

            // The shared Inventory singleton holds the *active* character's items;
            // inactive characters keep whatever inventory their file already had.
            for (int i = 0; i < pm.Characters.Count; i++)
                SaveCharacter(pm.Characters[i], includeLiveInventory: i == pm.ActiveIndex);

            Debug.Log($"[Save] Saved master + {pm.Characters.Count} character(s) to {SaveFolder}");
        }

        public void SaveCharacter(CharacterData c, bool includeLiveInventory)
        {
            if (c == null) return;
            Directory.CreateDirectory(SaveFolder);

            var data = Capture(c);
            data.inventory = includeLiveInventory
                ? CaptureInventory()
                : ReadExistingInventory(c.characterName);
            WriteJson(CharacterPath(c.characterName), data);
        }

        CharacterSaveData Capture(CharacterData c)
        {
            var data = new CharacterSaveData
            {
                characterName = c.characterName,
                classId = c.playerClass != null ? c.playerClass.name : "",
                level = c.level,
                currentXP = c.currentXP,
                gold = c.gold,
                skills = c.skills,
                // Stub: no activity system yet, so every save records "Idle, as of now".
                currentActivity = "Idle",
                activityStartedUtcTicks = DateTime.UtcNow.Ticks,
            };

            foreach (var pc in c.unlockedClasses)
                if (pc != null) data.unlockedClassIds.Add(pc.name);

            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None) continue;
                var item = c.equipment.Get(slot);
                if (item != null)
                    data.equipment.Add(new EquipmentSaveEntry { slot = slot.ToString(), itemId = item.name });
            }

            return data;
        }

        static List<InventorySaveEntry> CaptureInventory()
        {
            var list = new List<InventorySaveEntry>();
            var inv = Inventory.Instance;
            if (inv == null) return list;

            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                var slot = inv.GetSlot(i);
                if (!slot.IsEmpty)
                    list.Add(new InventorySaveEntry { index = i, itemId = slot.item.name, count = slot.count });
            }
            return list;
        }

        List<InventorySaveEntry> ReadExistingInventory(string characterName)
        {
            string path = CharacterPath(characterName);
            if (!File.Exists(path)) return new List<InventorySaveEntry>();
            var existing = ReadJson<CharacterSaveData>(path);
            return existing?.inventory ?? new List<InventorySaveEntry>();
        }

        // ── Loading ───────────────────────────────────────────────────────────

        // Returns the saved active character index, or -1 if there is no master save.
        public int LoadMasterActiveIndex()
        {
            if (!File.Exists(MasterPath)) return -1;
            var master = ReadJson<MasterSaveData>(MasterPath);
            return master != null ? master.activeIndex : -1;
        }

        // Applies each character's save file onto the Inspector-authored characters.
        // Characters without a file are left untouched (fresh defaults). The shared
        // Inventory is only populated from the active character's file.
        public void LoadCharacters(IReadOnlyList<CharacterData> characters, int activeIndex)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                var c = characters[i];
                string path = CharacterPath(c.characterName);
                if (!File.Exists(path)) continue;

                var data = ReadJson<CharacterSaveData>(path);
                if (data == null) continue;

                Apply(data, c, applyInventory: i == activeIndex);
            }
        }

        // Refreshes the shared Inventory from a character's save file (or clears it
        // if they have none) — used when switching the active character.
        public void LoadInventoryFor(CharacterData c)
        {
            if (c == null) return;
            string path = CharacterPath(c.characterName);
            var data = File.Exists(path) ? ReadJson<CharacterSaveData>(path) : null;
            ApplyInventory(data?.inventory ?? new List<InventorySaveEntry>());
        }

        void Apply(CharacterSaveData data, CharacterData c, bool applyInventory)
        {
            c.level = Mathf.Max(1, data.level);
            c.currentXP = data.currentXP;
            c.gold = Mathf.Max(0, data.gold);
            if (data.skills != null) c.skills = data.skills;
            // currentActivity/activityStartedUtcTicks: nothing consumes these yet —
            // the AFK-gains pass will apply offline progress here on load.

            // Keep the authored base class; add any extra unlocked classes on top.
            foreach (var id in data.unlockedClassIds)
            {
                var pc = FindClass(id);
                if (pc != null && !c.unlockedClasses.Contains(pc))
                    c.unlockedClasses.Add(pc);
            }

            // The save is the source of truth for gear: clear, then re-equip.
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
                if (slot != EquipSlot.None) c.equipment.Set(slot, null);

            foreach (var e in data.equipment)
            {
                if (!Enum.TryParse(e.slot, out EquipSlot slot) || slot == EquipSlot.None) continue;
                var item = FindItem(e.itemId);
                if (item != null) c.equipment.Set(slot, item);
                else Debug.LogWarning($"[Save] Unknown item '{e.itemId}' in {c.characterName}'s {e.slot} slot — is the item database filled?");
            }

            if (applyInventory) ApplyInventory(data.inventory);
        }

        void ApplyInventory(List<InventorySaveEntry> entries)
        {
            var inv = Inventory.Instance;
            if (inv == null) return;

            for (int i = 0; i < Inventory.MaxSlots; i++)
                inv.SetSlot(i, null, 0);

            if (entries == null) return;
            foreach (var e in entries)
            {
                var item = FindItem(e.itemId);
                if (item != null) inv.SetSlot(e.index, item, e.count);
                else Debug.LogWarning($"[Save] Unknown item '{e.itemId}' in saved inventory slot {e.index} — is the item database filled?");
            }
        }

        // ── Wiping (used by the Save Tools editor window too) ─────────────────

        public static void WipeCharacter(string characterName)
        {
            string path = CharacterPath(characterName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static void WipeAll()
        {
            if (Directory.Exists(SaveFolder)) Directory.Delete(SaveFolder, true);
        }

        // Rewrites a character's save file as a fresh level-1 record while keeping
        // their identity: same name and class, but XP, skills, extra unlocked
        // classes, gear, and inventory are all stripped. Unlike WipeCharacter this
        // leaves a file in place (so the character isn't re-seeded from Inspector
        // defaults). Returns false if there's no readable file to reset.
        public static bool ResetCharacterToLevelOne(string characterName) =>
            ResetSaveFileToLevelOne(CharacterPath(characterName));

        // Path-based variant so the Save Tools window can reset a file it's already
        // listing without round-tripping the (sanitized) character name.
        public static bool ResetSaveFileToLevelOne(string path)
        {
            if (!File.Exists(path)) return false;

            var data = ReadJson<CharacterSaveData>(path);
            if (data == null) return false;

            data.level = 1;
            data.currentXP = 0;
            data.gold = 0;
            data.skills = new SkillRegistry();        // back to starting points, nothing learned
            data.unlockedClassIds = new List<string>(); // base class is re-added on load via EnsureBaseClassUnlocked
            data.equipment = new List<EquipmentSaveEntry>();
            data.inventory = new List<InventorySaveEntry>();

            WriteJson(path, data);
            return true;
        }

        [ContextMenu("Wipe Active Character Save")]
        void WipeActiveCharacterSave()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c == null) { Debug.Log("[Save] No active character."); return; }
            WipeCharacter(c.characterName);
            Debug.Log($"[Save] Wiped save for '{c.characterName}'. Note: quitting Play Mode auto-saves and will re-create it.");
        }

        [ContextMenu("Reset Active Character to Level 1")]
        void ResetActiveCharacterSave()
        {
            var c = PlayerManager.Instance?.ActiveCharacter;
            if (c == null) { Debug.Log("[Save] No active character."); return; }
            if (ResetCharacterToLevelOne(c.characterName))
                Debug.Log($"[Save] Reset '{c.characterName}' to level 1. Note: this rewrote the file — quitting Play Mode auto-saves the live character over it, so reset after exiting Play Mode (or reload to see it).");
            else
                Debug.Log($"[Save] No save file for '{c.characterName}' yet — nothing to reset.");
        }

        // ── Lookups & IO ──────────────────────────────────────────────────────

        ItemDefinition FindItem(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || itemDatabase == null) return null;
            foreach (var item in itemDatabase)
                if (item != null && item.name == assetName) return item;
            return null;
        }

        PlayerClass FindClass(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || classDatabase == null) return null;
            foreach (var pc in classDatabase)
                if (pc != null && pc.name == assetName) return pc;
            return null;
        }

        static void WriteJson<T>(string path, T data)
        {
            try { File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true)); }
            catch (Exception ex) { Debug.LogError($"[Save] Failed writing {path}: {ex.Message}"); }
        }

        static T ReadJson<T>(string path) where T : class
        {
            try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] Failed reading {path}: {ex.Message}");
                return null;
            }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unnamed";
            foreach (char bad in Path.GetInvalidFileNameChars())
                s = s.Replace(bad, '_');
            return s.Replace(' ', '_').ToLowerInvariant();
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Fill Databases")]
        void AutoFillDatabases()
        {
            itemDatabase = FindAssetsOfType<ItemDefinition>();
            classDatabase = FindAssetsOfType<PlayerClass>();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[Save] Databases filled: {itemDatabase.Length} item(s), {classDatabase.Length} class(es).");
        }

        static T[] FindAssetsOfType<T>() where T : UnityEngine.Object
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var result = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                result[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
            return result;
        }
#endif
    }
}
