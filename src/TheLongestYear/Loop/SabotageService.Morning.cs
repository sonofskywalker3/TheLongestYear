using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The morning: the message box reports and the Junimos' tainted scene.</summary>
    internal sealed partial class SabotageService
    {
        // ------------------------------------------------------------------ the morning

        /// <summary>The morning: message boxes for what the night took. A tamper report waits for the
        /// Junimos' scene, which plays when the farmer first arrives on the Farm by any route
        /// (<see cref="TryStartTamperScene"/>, Jeff 2026-10-07); every other report (the crows,
        /// the thief, the hall line for a reversion) queues for the morning box on waking. The
        /// morning goes on at once either way.</summary>
        public void ShowMorning()
        {
            // Nothing waiting crosses into the day: a strike whose scene had the slot lands here at
            // the latest, and one whose scene never had it is postponed.
            SettlePendingIfAny("morning");
            if (!RunActivation.IsActive) return;
            if (TamperSceneOwed)
                _monitor.Log("Darkness: the board changed in the night; the Junimos wait for the farmer's first arrival on the farm.", LogLevel.Info);
            ShowMorningReports();
        }

        /// <summary>A tamper report is waiting for the Junimos' scene.</summary>
        public bool TamperSceneOwed
            => RunActivation.IsActive && StartTamperScene != null
               && Run.PendingSabotageReports != null
               && Run.PendingSabotageReports.Exists(r => r.Kind == SabotageKind.Tampering);

        /// <summary>Start the Junimos' "tainted" scene at the porch, if a tamper report is waiting.
        /// The report is consumed only once the scene has really started, so it plays once, and a
        /// scene that cannot start keeps it for the next Farm arrival. Only the report the scene
        /// tells is consumed: a second tamper keeps its report for the next arrival. Any other
        /// report still waiting shows after the scene.</summary>
        public bool TryStartTamperScene()
        {
            if (!TamperSceneOwed) return false;
            SabotageReport tamper = Run.PendingSabotageReports.Find(r => r.Kind == SabotageKind.Tampering);
            // Plurals the way the game makes them, corrected for its mass nouns (Jeff, 2026-10-07:
            // "all the Parsnip", "Bring us 3 Beer").
            Func<string, string> gamePlural = word => StardewValley.BellsAndWhistles.Lexicon.makePlural(word);
            // The exact item, flavour included (designer, 2026-10-07: "all the Dried Apples").
            string exactOld = ExactName(tamper.OldItemId, tamper.OldFlavor);
            bool oldFlavored = !string.IsNullOrEmpty(tamper.OldFlavor);
            int oldCategory = CategoryOf(tamper.OldItemId);
            string oldName = ItemPlurals.Tainted(exactOld, tamper.OldItemId, oldFlavored, gamePlural, oldCategory);
            // "touched it" after an uncountable item, "touched them" otherwise (designer, 2026-10-08).
            bool oldIsMass = ItemPlurals.TaintedReadsAsMass(exactOld, tamper.OldItemId, oldFlavored, gamePlural, oldCategory);
            // The counted ask names a container or a proper plural (designer, 2026-10-07: "jars of
            // wild honey, and sea jellies, and bottles of blueberry wine").
            string askName = FlavorlessBundleSlots.AskNameKeyFor(tamper.ItemId) is string nameKey
                ? Strings.Get(nameKey)
                : Strings.ItemName(tamper.ItemId);
            string ask = AskPhrases.Ask(tamper.Count, tamper.ItemId, askName, gamePlural, CategoryOf(tamper.ItemId));
            if (!StartTamperScene(oldName, oldIsMass, ask, ItemPlurals.AskIsPlural(tamper.Count), ShowMorningReports)) return false;
            Run.PendingSabotageReports.Remove(tamper);
            return true;
        }

        /// <summary>Show what the night took in the morning message box (<see cref="MorningBox"/>,
        /// designer 2026-10-08), one box per message, then forget what was shown. A tamper report
        /// stays while the Junimos' scene can still tell it (<see cref="TamperSceneOwed"/>); with no
        /// scene to tell it, it shows as the hall line like a reversion.</summary>
        public void ShowMorningReports()
        {
            if (!RunActivation.IsActive) return;
            List<SabotageReport> pending = Run.PendingSabotageReports;
            if (pending == null || pending.Count == 0) return;
            List<SabotageReport> reports = MorningReports.TakeShownNow(pending, tampersWait: StartTamperScene != null);
            if (reports.Count == 0) return;
            // The hall fronts share one line and say it once, however many struck (Jeff, 2026-09-09:
            // the player wakes with a feeling, the board tells the rest).
            // Each reversion adds one sentence after it naming the item and its bundle (designer,
            // 2026-10-08); the hall box goes where the first hall report was.
            bool hallQueued = false;
            int hallAt = -1;
            var reverted = new List<string>();
            bool first = true;
            var boxes = new List<string>();
            foreach (SabotageReport report in reports)
            {
                switch (report.Kind)
                {
                    case SabotageKind.Blight:
                        if (report.Count > 0) boxes.Add(CrowsLine(report.Count));
                        if (report.Stolen != null && report.Stolen.Count > 0) boxes.Add(StolenLine(report.Stolen));
                        break;
                    case SabotageKind.Reversion:
                    case SabotageKind.Tampering:
                        if (!hallQueued) { hallAt = boxes.Count; boxes.Add(null); hallQueued = true; }
                        if (report.Kind == SabotageKind.Reversion && RevertedLine(report) is string line) reverted.Add(line);
                        break;
                }
            }
            if (hallAt >= 0)
                boxes[hallAt] = reverted.Count == 0
                    ? Strings.Get("morning.sabotage.hall")
                    : Strings.Get("morning.sabotage.hall") + " " + string.Join(" ", reverted);
            foreach (string text in boxes) Box(text, ref first);
        }

        /// <summary>The reversion callout, the designer's words (2026-10-08): "The Parsnip is gone
        /// from the Spring Crops bundle." / "The Parsnips are gone from ...", no count. Null when the
        /// report names no item (a save from before the callout existed).</summary>
        private static string RevertedLine(SabotageReport report)
        {
            if (string.IsNullOrEmpty(report.ItemId)) return null;
            Func<string, string> gamePlural = word => StardewValley.BellsAndWhistles.Lexicon.makePlural(word);
            bool flavored = !string.IsNullOrEmpty(report.OldFlavor);
            string exact = ExactName(report.ItemId, report.OldFlavor);
            (string item, bool plural) = MorningLines.RevertedItem(
                Math.Max(1, report.Stack), exact, report.ItemId, flavored, gamePlural, CategoryOf(report.ItemId));
            string bundle = string.IsNullOrWhiteSpace(report.BundleLabel) ? report.BundleName : report.BundleLabel;
            bool named = MorningLines.LabelSaysBundle(bundle);
            // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
            if (plural)
                return named
                    ? Strings.Get("morning.sabotage.reverted.other-named", new Dictionary<string, string> { ["item"] = item, ["bundle"] = bundle })
                    : Strings.Get("morning.sabotage.reverted.other", new Dictionary<string, string> { ["item"] = item, ["bundle"] = bundle });
            return named
                ? Strings.Get("morning.sabotage.reverted.one-named", new Dictionary<string, string> { ["item"] = item, ["bundle"] = bundle })
                : Strings.Get("morning.sabotage.reverted.one", new Dictionary<string, string> { ["item"] = item, ["bundle"] = bundle });
        }

        /// <summary>The crows' morning line, the designer's own words (2026-10-08).</summary>
        private static string CrowsLine(int withered)
            // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
            => withered == 1
                ? Strings.Get("morning.sabotage.crows.one")
                : Strings.Get("morning.sabotage.crows.other", new Dictionary<string, string> { ["count"] = withered.ToString() });

        /// <summary>The thief's morning line, naming what he took ("3 Parsnips, 1 bottle of Wine
        /// and 2 other things").</summary>
        private static string StolenLine(List<StolenStack> stolen)
        {
            Func<string, string> gamePlural = word => StardewValley.BellsAndWhistles.Lexicon.makePlural(word);
            (List<string> named, int other) = MorningLines.StolenPhrases(MorningLines.Merge(stolen), gamePlural);
            if (other == 1) named.Add(Strings.Get("morning.sabotage.stolen.more.one"));
            else if (other > 1) named.Add(Strings.Get("morning.sabotage.stolen.more.other", new Dictionary<string, string> { ["count"] = other.ToString() }));
            return Strings.Get("morning.sabotage.stolen", new Dictionary<string, string> { ["items"] = MorningLines.JoinList(named) });
        }

        /// <summary>Queue one morning box. The first of the morning carries the darkness's sound.</summary>
        private void Box(string text, ref bool first)
        {
            _monitor.Log($"Darkness: morning message queued: {text}", LogLevel.Trace);
            MorningBox.Enqueue(text, first ? "shadowDie" : null);
            first = false;
        }
    }
}
