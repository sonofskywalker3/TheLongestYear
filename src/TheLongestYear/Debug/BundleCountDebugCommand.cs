using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_bundlecount: headless checks for the bundle-count dial (spec
    /// 2026-10-09-bundle-count-dial).
    ///   (no args)        live bundles per room, and whether the CC's room lookup knows every bundle
    ///   set &lt;step&gt;       sets the dial in memory only (config.json is not written); next new board
    ///   open &lt;area 0-5&gt;  opens that room's page, logs the bags on it
    ///   missed           runs the CC's checkForMissedRewards (threw KeyNotFound on a stale lookup)
    ///   close            closes the open menu without its exit action (no restore cutscene)
    ///   buy &lt;index|cheapest&gt;  pays a Vault bundle through the page's own purchase button</summary>
    internal static class BundleCountDebugCommand
    {
        public const string Usage =
            "Debug: bundle-count dial. Usage: tly_bundlecount [set <easy|normal|hard|extreme> | open <area 0-5> | close | missed | buy <vault index|cheapest>]. No args lists bundles per room.";

        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>CommunityCenter.getAreaNumberFromName("Vault").</summary>
        private const int VaultArea = 4;

        public static void Run(IMonitor monitor, GameplayConfig config, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (sub == "set")
            {
                DifficultyStep step = DifficultySteps.Parse(args.Length > 1 ? args[1] : null);
                config.Difficulty.BundleCount = step;
                monitor.Log($"tly_bundlecount: dial set to {step} in memory (config.json not written); the next new board uses it.", LogLevel.Info);
                return;
            }
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc == null) { monitor.Log("tly_bundlecount: no CommunityCenter.", LogLevel.Warn); return; }

            switch (sub)
            {
                case "open":
                    Open(monitor, cc, args.Length > 1 && int.TryParse(args[1], out int area) ? area : 0);
                    return;
                case "missed":
                    Missed(monitor, cc);
                    return;
                case "buy":
                    Buy(monitor, cc, args.Length > 1 ? args[1] : "cheapest");
                    return;
                case "close":
                    Close(monitor);
                    return;
                default:
                    List(monitor, cc);
                    return;
            }
        }

        private static void List(IMonitor monitor, CommunityCenter cc)
        {
            Dictionary<string, string> data = Game1.netWorldState.Value.BundleData;
            foreach (IGrouping<string, string> room in data.Keys.GroupBy(k => k.Split('/')[0]).OrderBy(g => CommunityCenter.getAreaNumberFromName(g.Key)))
            {
                var parts = room.OrderBy(k => int.TryParse(k.Split('/')[1], out int i) ? i : -1)
                    .Select(k => $"{k.Split('/')[1]}:{data[k].Split('/')[0]}");
                monitor.Log($"tly_bundlecount: {room.Key} = {room.Count()} [{string.Join(", ", parts)}]", LogLevel.Info);
            }

            var bundleToArea = typeof(CommunityCenter).GetField("bundleToAreaDictionary", Instance)?.GetValue(cc) as Dictionary<int, int>;
            var missing = Game1.netWorldState.Value.BundleRewards.Keys.Where(k => bundleToArea == null || !bundleToArea.ContainsKey(k)).ToList();
            int completionEntries = Game1.netWorldState.Value.Bundles.Keys.Count();
            monitor.Log(
                $"tly_bundlecount: {data.Count} bundles, {completionEntries} completion entries, CC lookup {(missing.Count == 0 ? "knows every bundle" : "MISSING " + string.Join(",", missing))}.",
                missing.Count == 0 ? LogLevel.Info : LogLevel.Error);
        }

        private static void Open(IMonitor monitor, CommunityCenter cc, int area)
        {
            // The page shows "???" for every name until the player can read Junimo text.
            Game1.player.mailReceived.Add("canReadJunimoText");
            var note = new JunimoNoteMenu(area, cc.bundlesDict());
            Game1.activeClickableMenu = note;
            monitor.Log(
                $"tly_bundlecount: opened area {area} ({CommunityCenter.getAreaNameFromNumber(area)}) with {note.bundles.Count} bag(s): " +
                string.Join(", ", note.bundles.Select(b => $"{b.bundleIndex}:{b.name}@{b.bounds.X},{b.bounds.Y}")),
                LogLevel.Info);
        }

        /// <summary>Buys one Vault bundle through the Vault page's own purchase button (the vanilla
        /// money path: gold taken, bundle and reward flagged, room completion checked). Gives the
        /// farmer the shortfall first. Leaves the page open (close it with "close" in a later batch).</summary>
        private static void Buy(IMonitor monitor, CommunityCenter cc, string which)
        {
            // TLY's donation observer only pays a bag it saw incomplete on an earlier tick, so the
            // page must already be open: open it first, then buy in the next batch.
            if (Game1.activeClickableMenu is not JunimoNoteMenu note || note.whichArea != VaultArea)
            {
                Open(monitor, cc, VaultArea);
                monitor.Log("tly_bundlecount buy: opened the Vault page; send buy again in the next batch.", LogLevel.Info);
                return;
            }
            Bundle bundle = which == "cheapest"
                ? note.bundles.Where(b => !b.complete).OrderBy(b => b.ingredients.Last().stack).FirstOrDefault()
                : note.bundles.FirstOrDefault(b => int.TryParse(which, out int index) && b.bundleIndex == index);
            if (bundle == null)
            {
                monitor.Log($"tly_bundlecount buy: no open Vault bundle '{which}'.", LogLevel.Warn);
                return;
            }
            int price = bundle.ingredients.Last().stack;
            if (Game1.player.Money < price)
                Game1.player.Money = price;
            int before = Game1.player.Money;
            typeof(JunimoNoteMenu).GetMethod("setUpBundleSpecificPage", Instance)?.Invoke(note, new object[] { bundle });
            if (note.purchaseButton == null)
            {
                monitor.Log("tly_bundlecount buy: the Vault page has no purchase button.", LogLevel.Error);
                return;
            }
            note.receiveLeftClick(note.purchaseButton.bounds.Center.X, note.purchaseButton.bounds.Center.Y);
            // The page stays open so TLY's donation observer sees the bag complete on its next tick,
            // as it would for a player; send "close" in a later batch. No restore cutscene on exit.
            note.exitFunction = null;
            monitor.Log(
                $"tly_bundlecount buy: {bundle.bundleIndex}:{bundle.name} price {price:N0}g, gold {before:N0} -> {Game1.player.Money:N0}, " +
                $"paid {cc.bundles[bundle.bundleIndex][0]}, reward flag {cc.bundleRewards[bundle.bundleIndex]}, " +
                $"ccVault queued {Game1.player.mailForTomorrow.Any(m => m.StartsWith("ccVault"))}.",
                LogLevel.Info);
        }

        /// <summary>Closes whatever menu is open (a page left open by <c>open</c>) without its exit
        /// action, so no restore cutscene runs.</summary>
        private static void Close(IMonitor monitor)
        {
            if (Game1.activeClickableMenu is IClickableMenu menu)
            {
                menu.exitFunction = null;
                menu.exitThisMenu(playSound: false);
            }
            monitor.Log("tly_bundlecount: menu closed.", LogLevel.Info);
        }

        private static void Missed(IMonitor monitor, CommunityCenter cc)
        {
            MethodInfo method = typeof(CommunityCenter).GetMethod("checkForMissedRewards", Instance);
            try
            {
                method?.Invoke(cc, null);
                monitor.Log("tly_bundlecount: checkForMissedRewards ran clean.", LogLevel.Info);
            }
            catch (TargetInvocationException ex)
            {
                monitor.Log($"tly_bundlecount: checkForMissedRewards threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}", LogLevel.Error);
            }
        }
    }
}
