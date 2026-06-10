using System.Text;
using IdleTime.Core;

namespace IdleTime.UI
{
    // Formats an ItemDefinition into tooltip rich-text. Shared by inventory and
    // equipment slots so item hover text reads the same everywhere.
    public static class ItemTooltips
    {
        public static string Describe(ItemDefinition item)
        {
            if (item == null) return "";

            var sb = new StringBuilder();
            sb.Append($"<b>{item.itemName}</b>");

            // Subtitle: weapon type / equip slot / item type.
            string subtitle = item is WeaponDefinition w
                ? $"{w.weaponType} Weapon"
                : item.equipSlot != EquipSlot.None ? item.equipSlot.ToString() : item.itemType.ToString();
            sb.Append($"\n<size=80%><color=#9AA0A6>{subtitle}</color></size>");

            AppendStat(sb, "Attack", item.bonusAttack);
            AppendStat(sb, "Defense", item.bonusDefense);
            AppendStat(sb, "Accuracy", item.bonusAccuracy);
            AppendStat(sb, "STR", item.bonusStr);
            AppendStat(sb, "DEX", item.bonusDex);
            AppendStat(sb, "WIS", item.bonusWis);
            AppendStat(sb, "LUK", item.bonusLuk);
            if (item is ArmorDefinition armor) AppendStat(sb, "Max HP", armor.bonusMaxHP);

            if (!string.IsNullOrEmpty(item.description))
                sb.Append($"\n<size=85%><i>{item.description}</i></size>");

            return sb.ToString();
        }

        static void AppendStat(StringBuilder sb, string label, int value)
        {
            if (value == 0) return;
            sb.Append($"\n{(value > 0 ? "+" : "")}{value} {label}");
        }
    }
}
