using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// New-game Advanced Options: the vanilla "Community Center Bundles: Normal / Remixed" dropdown
    /// is meaningless on a TLY save — the bundle engine writes its own board at run-create and on
    /// every reset regardless of the choice (and on 0.11.60 the choice was silently lost on the
    /// first reset anyway, Nexus bug 1108030). Rather than removing the row (which looks like a
    /// missing feature), replace its two entries with a single "TLY Custom" entry + a tooltip that
    /// says why (user ruling 2026-08-21: "replace it with a single option, that's less confusing").
    ///
    /// Postfix on <c>AdvancedGameOptions.PopulateOptions</c>: the CC-bundles dropdown is the one
    /// whose values contain <c>BundleType.Remixed</c>. Its apply-callback still writes
    /// <c>Game1.bundleType</c> from the original options array, so leaving the single entry at index
    /// 0 (= Default) is harmless. Gated on <see cref="Enabled"/> (= <c>GameplayConfig.Enabled</c>)
    /// so the vanilla dropdown returns when the player turns TLY off.
    /// </summary>
    [HarmonyPatch(typeof(AdvancedGameOptions), nameof(AdvancedGameOptions.PopulateOptions))]
    internal static class BundleOptionPatch
    {
        /// <summary>Set by ModEntry: mirrors <c>GameplayConfig.Enabled</c>.</summary>
        internal static Func<bool> Enabled;
        internal static IMonitor Monitor;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(AdvancedGameOptions __instance)
        {
            if (Enabled == null || !Enabled()) return;
            if (Context.IsWorldReady) return;   // new-game flow only (AGO is title-screen only, belt-and-braces)

            string remixed = Game1.BundleType.Remixed.ToString();
            OptionsDropDown dropdown = __instance.options
                .OfType<OptionsDropDown>()
                .FirstOrDefault(d => d.dropDownOptions.Contains(remixed));
            if (dropdown == null)
            {
                Monitor?.Log("Advanced Options: CC-bundles dropdown not found — leaving vanilla options.", LogLevel.Trace);
                return;
            }

            string label = Strings.Get("ago.bundles.tly-custom");
            string tooltip = Strings.Get("ago.bundles.tly-custom.tooltip");

            dropdown.dropDownOptions.Clear();
            dropdown.dropDownDisplayOptions.Clear();
            dropdown.dropDownOptions.Add(Game1.BundleType.Default.ToString());
            dropdown.dropDownDisplayOptions.Add(label);
            dropdown.selectedOption = 0;
            dropdown.RecalculateBounds();

            __instance.tooltips[dropdown] = tooltip;
            int index = __instance.options.IndexOf(dropdown);
            if (index > 0 && __instance.options[index - 1].style == OptionsElement.Style.OptionLabel)
                __instance.tooltips[__instance.options[index - 1]] = tooltip;

            Game1.bundleType = Game1.BundleType.Default;
            Monitor?.Log("Advanced Options: CC-bundles dropdown replaced with 'TLY Custom'.", LogLevel.Trace);
        }
    }
}
