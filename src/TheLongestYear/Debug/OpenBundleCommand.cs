using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_openbundle: open the Community Center note on one bundle's own page, the page
    /// the player reaches by clicking that bundle in its room's note, so the page can be looked at
    /// headless (the darkened-slot mark, the slot icons). Close it with tly_dismiss.</summary>
    internal static class OpenBundleCommand
    {
        public const string Usage =
            "Debug: open the Community Center note on one bundle's page. Usage: tly_openbundle <bundle name|bundle index>";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length == 0) { monitor.Log(Usage, LogLevel.Warn); return; }
            string wanted = string.Join(" ", args);
            CommunityCenter cc = Game1.RequireLocation<CommunityCenter>("CommunityCenter");
            for (int area = 0; area <= 5; area++)
            {
                var note = new JunimoNoteMenu(false, area, true);
                Bundle bundle = note.bundles.FirstOrDefault(b =>
                    string.Equals(b.name, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(b.label, wanted, StringComparison.OrdinalIgnoreCase)
                    || b.bundleIndex.ToString() == wanted);
                if (bundle == null) continue;
                Game1.activeClickableMenu = note;
                AccessTools.Method(typeof(JunimoNoteMenu), "setUpBundleSpecificPage").Invoke(note, new object[] { bundle });
                string slots = string.Join(", ", bundle.ingredients.Select((ing, i) => $"{i}:{ing.id}x{ing.stack}{(ing.completed ? " done" : "")}"));
                monitor.Log($"tly_openbundle: opened '{bundle.name}' (index {bundle.bundleIndex}, area {area}): {slots}.", LogLevel.Info);
                return;
            }
            monitor.Log($"tly_openbundle: no bundle on the board is called '{wanted}'.", LogLevel.Warn);
        }
    }
}
