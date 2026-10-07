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
    /// <summary>The morning: HUD reports and the Junimos' tainted scene.</summary>
    internal sealed partial class SabotageService
    {
        // ------------------------------------------------------------------ the morning

        /// <summary>The morning: HUD lines for what the night took. A tamper report waits for the
        /// Junimos' scene, which plays when the farmer first steps out onto the Farm
        /// (<see cref="TryStartTamperScene"/>, Jeff 2026-10-07); every other report (blight,
        /// spoiled, missing, the hall line for a reversion) shows on waking as it always has. The
        /// morning goes on at once either way.</summary>
        public void ShowMorning()
        {
            // The morning cannot report what has not happened: a strike whose scene never played
            // lands here at the latest.
            ApplyPendingIfAny("morning");
            if (!RunActivation.IsActive) return;
            if (TamperSceneOwed)
                _monitor.Log("Darkness: the board changed in the night; the Junimos wait for the farmer to step out onto the farm.", LogLevel.Info);
            ShowMorningReports();
        }

        /// <summary>A tamper report is waiting for the Junimos' scene.</summary>
        public bool TamperSceneOwed
            => RunActivation.IsActive && StartTamperScene != null
               && Run.PendingSabotageReports != null
               && Run.PendingSabotageReports.Exists(r => r.Kind == SabotageKind.Tampering);

        /// <summary>Start the Junimos' "tainted" scene where the farmer stands, if a tamper report is
        /// waiting. The report is consumed only once the scene has really started, so it plays once,
        /// and a scene that cannot start here keeps it for the next Farm entry. Only the report the
        /// scene tells is consumed: a second tamper keeps its report for the next door exit. Any
        /// other report still waiting shows after the scene.</summary>
        public bool TryStartTamperScene()
        {
            if (!TamperSceneOwed) return false;
            SabotageReport tamper = Run.PendingSabotageReports.Find(r => r.Kind == SabotageKind.Tampering);
            // Plurals the way the game makes them, corrected for its mass nouns (Jeff, 2026-10-07:
            // "all the Parsnip", "Bring us 3 Beer").
            Func<string, string> gamePlural = word => StardewValley.BellsAndWhistles.Lexicon.makePlural(word);
            // The exact item, flavour included (designer, 2026-10-07: "all the Dried Apples").
            string oldName = ItemPlurals.Tainted(ExactName(tamper.OldItemId, tamper.OldFlavor), tamper.OldItemId, !string.IsNullOrEmpty(tamper.OldFlavor), gamePlural);
            // The counted ask names a container or a proper plural (designer, 2026-10-07: "jars of
            // wild honey, and sea jellies, and bottles of blueberry wine").
            string askName = FlavorlessBundleSlots.AskNameKeyFor(tamper.ItemId) is string nameKey
                ? Strings.Get(nameKey)
                : Strings.ItemName(tamper.ItemId);
            string ask = AskPhrases.Ask(tamper.Count, tamper.ItemId, askName, gamePlural);
            if (!StartTamperScene(oldName, ask, ItemPlurals.AskIsPlural(tamper.Count), ShowMorningReports)) return false;
            Run.PendingSabotageReports.Remove(tamper);
            return true;
        }

        /// <summary>Show what the night took, as HUD lines, then forget what was shown. A tamper
        /// report stays while the Junimos' scene can still tell it (<see cref="TamperSceneOwed"/>);
        /// with no scene to tell it, it shows as the hall line like a reversion.</summary>
        public void ShowMorningReports()
        {
            if (!RunActivation.IsActive) return;
            List<SabotageReport> pending = Run.PendingSabotageReports;
            if (pending == null || pending.Count == 0) return;
            List<SabotageReport> reports = MorningReports.TakeShownNow(pending, tampersWait: StartTamperScene != null);
            if (reports.Count == 0) return;
            // The hall fronts share one line and say it once, however many struck (Jeff, 2026-09-09:
            // the player wakes with a feeling, the board tells the rest).
            bool hallSaid = false;
            foreach (SabotageReport report in reports)
            {
                switch (report.Kind)
                {
                    case SabotageKind.Blight:
                        // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
                        if (report.Count == 1)
                            Hud(Strings.Get("hud.sabotage.blight.one"));
                        else if (report.Count > 1)
                            Hud(Strings.Get("hud.sabotage.blight.other", new Dictionary<string, string> { ["count"] = report.Count.ToString() }));
                        if (report.Spoiled > 0)
                            Hud(Strings.Get("hud.sabotage.spoiled", new Dictionary<string, string> { ["count"] = report.Spoiled.ToString() }));
                        if (report.Missing > 0)
                            Hud(Strings.Get("hud.sabotage.missing", new Dictionary<string, string> { ["count"] = report.Missing.ToString() }));
                        break;
                    case SabotageKind.Reversion:
                    case SabotageKind.Tampering:
                        if (!hallSaid) Game1.addHUDMessage(new HUDMessage(Strings.Get("hud.sabotage.hall"), HUDMessage.error_type));
                        hallSaid = true;
                        break;
                }
            }
            Game1.playSound("shadowDie");
        }

        private static void Hud(string text) => Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));
    }
}
