using System.Text;
using UnityEngine;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Formats an ItemDefinition into tooltip rich-text. Shared by inventory and
    // equipment slots so item hover text reads the same everywhere.
    public static class ItemTooltips
    {
        public static string Describe(ItemDefinition item, CharacterData character = null)
        {
            if (item == null) return "";

            var sb = new StringBuilder();
            sb.Append($"<b>{item.itemName}</b>");

            // Subtitle: weapon type / equip slot / item type.
            string subtitle = item is WeaponDefinition w
                ? $"{w.weaponType} Weapon"
                : item.equipSlot != EquipSlot.None ? item.equipSlot.ToString() : item.itemType.ToString();
            sb.Append($"\n<size=80%><color=#9AA0A6>{subtitle}</color></size>");

            // Class restriction line for equippable items (red when this character can't wear it).
            if (item.equipSlot != EquipSlot.None)
            {
                bool canWear = character == null || item.AllowsClass(character.playerClass);
                sb.Append($"\n<size=80%><color=#{(canWear ? "9AA0A6" : "E06666")}>{ClassList(item)}</color></size>");
            }

            if (item is WeaponDefinition wpn && wpn.baseWeaponPower != 0)
                sb.Append($"\n<color=#E8B923>{wpn.baseWeaponPower} Weapon Power</color>");

            AppendStat(sb, "Attack", item.bonusAttack);
            AppendStat(sb, "Defense", item.bonusDefense);
            AppendStat(sb, "Accuracy", item.bonusAccuracy);
            AppendStat(sb, "STR", item.bonusStr);
            AppendStat(sb, "DEX", item.bonusDex);
            AppendStat(sb, "WIS", item.bonusWis);
            AppendStat(sb, "LUK", item.bonusLuk);
            if (item is ArmorDefinition armor) AppendStat(sb, "Max HP", armor.bonusMaxHP);

            if (item.restoreHP > 0 || item.restoreHPPercent > 0f)
                sb.Append($"\n<color=#6FCF6F>Restores {RestoreText(item.restoreHP, item.restoreHPPercent)} HP</color>");
            if (item.restoreMP > 0 || item.restoreMPPercent > 0f)
                sb.Append($"\n<color=#6FA8DC>Restores {RestoreText(item.restoreMP, item.restoreMPPercent)} MP</color>");

            if (!string.IsNullOrEmpty(item.description))
                sb.Append($"\n<size=85%><i>{item.description}</i></size>");

            return sb.ToString();
        }

        static string ClassList(ItemDefinition item)
        {
            if (!item.IsClassRestricted) return "All classes";

            var sb = new StringBuilder();
            foreach (var pc in item.allowedClasses)
            {
                if (pc == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(pc.className);
            }
            return sb.Length == 0 ? "All classes" : sb.ToString();
        }

        static void AppendStat(StringBuilder sb, string label, int value)
        {
            if (value == 0) return;
            sb.Append($"\n{(value > 0 ? "+" : "")}{value} {label}");
        }

        // "25%", "50", or "25% +10" when both a percent and a flat amount are set.
        static string RestoreText(int flat, float percent)
        {
            string pct = percent > 0f ? $"{Mathf.RoundToInt(percent * 100f)}%" : "";
            if (percent > 0f && flat > 0) return $"{pct} +{flat}";
            return percent > 0f ? pct : flat.ToString();
        }
    }
}
