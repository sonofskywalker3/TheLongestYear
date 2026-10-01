using System;
using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// MenuWithInventory.heldItem, the item on the cursor. On PC it is a property (get_heldItem /
    /// set_heldItem over a private _heldItem); on Android it is a public field. A direct reference
    /// compiles against one and throws MissingFieldException or MissingMethodException on the other,
    /// so it is resolved once by reflection: the property first, else the field. When neither is
    /// found (logged once), reads give null and writes report false, never a throw.
    /// </summary>
    internal static class MenuHeldItem
    {
        private const string MemberName = "heldItem";

        private static readonly Func<MenuWithInventory, Item> Getter;
        private static readonly Action<MenuWithInventory, Item> Setter;

        static MenuHeldItem()
        {
            PropertyInfo property = AccessTools.Property(typeof(MenuWithInventory), MemberName);
            if (property != null && property.CanRead && property.CanWrite)
            {
                Getter = menu => (Item)property.GetValue(menu);
                Setter = (menu, item) => property.SetValue(menu, item);
                return;
            }
            FieldInfo field = AccessTools.Field(typeof(MenuWithInventory), MemberName);
            if (field != null)
            {
                Getter = menu => (Item)field.GetValue(menu);
                Setter = (menu, item) => field.SetValue(menu, item);
                return;
            }
            PatchLog.Warn("MenuHeldItem: MenuWithInventory has no heldItem property or field; the cursor item cannot be read.");
        }

        /// <summary>The menu's cursor item, or null (no menu, or the member was not found).</summary>
        internal static Item Get(MenuWithInventory menu)
            => menu == null || Getter == null ? null : Getter(menu);

        /// <summary>Set the menu's cursor item. False when there is no menu or no member to set.</summary>
        internal static bool TrySet(MenuWithInventory menu, Item item)
        {
            if (menu == null || Setter == null)
                return false;
            Setter(menu, item);
            return true;
        }
    }
}
