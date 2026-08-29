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
    /// New-game Advanced Options: the vanilla "Community Center Bundles" Normal/Remixed dropdown
    /// gains a third, DEFAULT entry — "TLY Custom" (user ruling 2026-08-21: TLY Custom stays the
    /// pre-selected default; Normal/Remixed are deliberate picks). The choice is per save: the
    /// first SaveLoaded after SaveCreating reads <see cref="ConsumeLastChoice"/> and stamps
    /// <c>MetaState.BundleSource</c> / <c>VanillaBundleType</c> (spec 2026-08-21 BundleSource).
    ///
    /// Mechanics: vanilla's apply callback indexes its ORIGINAL two-entry option array
    /// (AdvancedGameOptions.AddDropdown captures <c>dropdown_options[selectedOption]</c>), so a
    /// third entry would throw on OK. We replace that callback with our own, which sets
    /// <c>Game1.bundleType</c> (Default for TLY Custom — the engine overwrites the board anyway)
    /// and records the choice. Only fires while the mod is enabled and no save is loaded.
    /// </summary>
    [HarmonyPatch(typeof(AdvancedGameOptions), nameof(AdvancedGameOptions.PopulateOptions))]
    internal static class BundleOptionPatch
    {
        internal enum Choice { TlyCustom, VanillaStandard, VanillaRemixed }

        internal static Func<bool> Enabled;
        internal static IMonitor Monitor;
        /// <summary>Config default (<see cref="BundleSourceNames"/>) — Vanilla pre-selects Normal.</summary>
        internal static Func<string> ConfiguredSource;

        private const string TlyCustomValue = "TLYCustom";

        private static Choice _lastChoice = Choice.TlyCustom;

        /// <summary>The choice the player left the dropdown on (default TLY Custom when the
        /// Advanced Options screen was never opened). Consumed by the new-game load.</summary>
        internal static Choice ConsumeLastChoice()
        {
            Choice c = _lastChoice;
            _lastChoice = Choice.TlyCustom;
            return c;
        }

        internal static void ResetChoice() => _lastChoice = Choice.TlyCustom;

        /// <summary>Maps a console token ("custom" / "standard" / "remixed", plus the dropdown's
        /// own vanilla wording) onto a <see cref="Choice"/>, so a diagnostic command can generate
        /// a board under a chosen option WITHOUT touching the save's stamped choice. Returns
        /// false for anything else, leaving the caller to report the bad argument.</summary>
        internal static bool TryParseChoice(string token, out Choice choice)
        {
            switch (token?.ToLowerInvariant())
            {
                case "custom":
                case "tlycustom":
                    choice = Choice.TlyCustom;
                    return true;
                case "standard":
                case "normal":
                case "default":
                    choice = Choice.VanillaStandard;
                    return true;
                case "remixed":
                    choice = Choice.VanillaRemixed;
                    return true;
                default:
                    choice = Choice.TlyCustom;
                    return false;
            }
        }

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

            // The vanilla apply callback for this dropdown, located by what its closure
            // CAPTURED (it holds the dropdown instance). Never locate it positionally:
            // AGO's header rows use the Default element style, so the old "count the
            // non-label options before it" index was off by one — it replaced the
            // Year1Completable checkbox's callback, left vanilla's 2-entry capture live,
            // and picking Remixed (index 2) threw out-of-range on OK, soft-locking the
            // screen (Nexus 1122619).
            int index = __instance.options.IndexOf(dropdown);
            int callbackIndex = -1;
            for (int i = 0; i < __instance.applySettingCallbacks.Count; i++)
            {
                if (DelegateClosures.References(__instance.applySettingCallbacks[i], dropdown))
                {
                    callbackIndex = i;
                    break;
                }
            }
            if (callbackIndex < 0)
            {
                Monitor?.Log("Advanced Options: couldn't locate the CC-bundles apply callback by its closure — leaving vanilla options.", LogLevel.Warn);
                return;
            }

            string normalLabel = dropdown.dropDownDisplayOptions.Count > 0 ? dropdown.dropDownDisplayOptions[0] : "Normal";
            string remixedLabel = dropdown.dropDownDisplayOptions.Count > 1 ? dropdown.dropDownDisplayOptions[1] : "Remixed";
            string tooltip = Strings.Get("ago.bundles.tly-custom.tooltip");

            dropdown.dropDownOptions.Clear();
            dropdown.dropDownDisplayOptions.Clear();
            dropdown.dropDownOptions.Add(TlyCustomValue);
            dropdown.dropDownDisplayOptions.Add(Strings.Get("ago.bundles.tly-custom"));
            dropdown.dropDownOptions.Add(Game1.BundleType.Default.ToString());
            dropdown.dropDownDisplayOptions.Add(normalLabel);
            dropdown.dropDownOptions.Add(remixed);
            dropdown.dropDownDisplayOptions.Add(remixedLabel);

            bool vanillaDefault = BundleSourceNames.IsVanilla(ConfiguredSource?.Invoke());
            dropdown.selectedOption = vanillaDefault ? 1 : 0;
            _lastChoice = vanillaDefault ? Choice.VanillaStandard : Choice.TlyCustom;
            dropdown.RecalculateBounds();

            __instance.tooltips[dropdown] = tooltip;
            if (index > 0 && __instance.options[index - 1].style == OptionsElement.Style.OptionLabel)
                __instance.tooltips[__instance.options[index - 1]] = tooltip;

            __instance.applySettingCallbacks[callbackIndex] = () =>
            {
                switch (dropdown.selectedOption)
                {
                    case 2:
                        Game1.bundleType = Game1.BundleType.Remixed;
                        _lastChoice = Choice.VanillaRemixed;
                        break;
                    case 1:
                        Game1.bundleType = Game1.BundleType.Default;
                        _lastChoice = Choice.VanillaStandard;
                        break;
                    default:
                        Game1.bundleType = Game1.BundleType.Default;
                        _lastChoice = Choice.TlyCustom;
                        break;
                }
                Monitor?.Log($"Advanced Options: CC-bundles choice = {_lastChoice} (Game1.bundleType={Game1.bundleType}).", LogLevel.Trace);
            };

            Game1.bundleType = Game1.BundleType.Default;
            Monitor?.Log($"Advanced Options: CC-bundles dropdown = TLY Custom / {normalLabel} / {remixedLabel} (default index {dropdown.selectedOption}).", LogLevel.Trace);
        }
    }
}
