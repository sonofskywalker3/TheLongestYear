using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Dump the <c>NetWorldState</c> fields the 2026-08-26 keep/wipe audit ruled on
        /// (docs/superpowers/2026-08-26-networldstate-field-rulings.md). Read-only. Exists because
        /// the audit's fixes all live in WorldResetService over live Game1 statics, which the Core
        /// test project cannot construct — so the only way to verify a reset actually wiped what
        /// the table says is to print both sides of it and compare. Run before and after a
        /// tly_reset.</summary>
        /// <summary>Read-only difficulty probe. Prints the CONFIGURED steps next to the STAMPED
        /// ones, because those two disagree by design whenever the player has changed GMCM since
        /// the last reset, and a balance report is worthless without knowing which was in force.
        /// Same read-only shape as <see cref="CmdNetState"/>.</summary>
        /// <summary>Writes a Markdown catalogue of everything the engine can put on a board: every
        /// candidate bundle per room position, and for each one either its fixed item list or the
        /// pool it re-rolls from, plus the rules that decide quantities.
        ///
        /// Built from LIVE game data (Data/Bundles, Data/RandomBundles, Data/Crops, Data/Fish,
        /// Data/Locations, Data/Monsters ...) rather than a hand-written table, so it stays true
        /// for whatever content mods are installed and cannot drift from the generator.
        /// Diagnostics only: nothing is written to the save.</summary>
        /// <summary>Audits every season gate on the live board: for each bundle and each season,
        /// what the gate demands against how many of that bundle's ingredients can actually exist
        /// by that season's day 28.
        ///
        /// The question it answers is "hard but possible". IMPOSSIBLE means the gate demands more
        /// than the world can supply by then, which bricks the run; FREE means the gate demands
        /// nothing that season. Both are reported per bundle so a curated quota can be judged.
        ///
        /// LIMIT, stated plainly: this checks SEASON feasibility only. An item obtainable in Spring
        /// but needing a keg, a fish pond or a 10,000g tool upgrade counts as obtainable here.
        /// It proves nothing is impossible for calendar reasons; it does not prove anything is
        /// comfortable. Read-only.</summary>
        /// <summary>Prints the derived availability model for one item id, or for every
        /// ingredient of a named bundle. Diagnostics only, read-only.</summary>
        private void CmdItemModel(string command, string[] args)
        {
            if (_availability == null)
            {
                this.Monitor.Log("No availability model yet; load a save first.", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: tly_itemmodel <itemId|bundleName>", LogLevel.Info);
                return;
            }

            string target = string.Join(" ", args);
            // Read the live requirements the way tly_gatecheck does. The _requirements field is
            // not refreshed when a reset regenerates the board, so sourcing from it would report
            // due dates from the previous board and disagree with tly_gatecheck on the same save.
            var requirements = _runController?.Requirements ?? _requirements;
            BundleRequirement req = requirements?
                .FirstOrDefault(r => string.Equals(r.Name, target, StringComparison.OrdinalIgnoreCase));

            if (req != null)
            {
                this.Monitor.Log($"Bundle '{req.Name}' ({req.Kind}):", LogLevel.Info);
                foreach (string id in req.Ingredients)
                {
                    TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                    string due = req.ItemSeasonPins != null
                        && req.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season d)
                        ? d.ToString()
                        : "never";
                    this.Monitor.Log(
                        $"  {id}: due {due}; earliest {a.EarliestSeason}, effort {a.Effort} ({a.Source}), tier {TierLabel(id, a)} [{a.Basis}]",
                        LogLevel.Info);
                }
                return;
            }

            string itemId = target.StartsWith("(", StringComparison.Ordinal) ? target : $"(O){target}";
            TheLongestYear.Core.ItemAvailability single = _availability.For(itemId);
            this.Monitor.Log(
                $"{itemId}: earliest {single.EarliestSeason}, effort {single.Effort} ({single.Source}), tier {TierLabel(itemId, single)} [{single.Basis}]",
                LogLevel.Info);
        }

        /// <summary>The item's effort tier within the first theme pool that contains it, or
        /// "n/a" when no engine pool lists it (tiers are absolute effort bands).</summary>
        private string TierLabel(string itemId, TheLongestYear.Core.ItemAvailability availability)
        {
            if (_enginePools == null || _effortData == null) return "n/a";
            foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
            {
                IReadOnlyList<string> ids = ThemeEffortPools.IdsFor(theme, _enginePools, _effortData.Objects);
                if (!ids.Contains(itemId)) continue;
                return $"{EffortTiers.Tier(availability.Effort)} in {theme}";
            }
            return "n/a";
        }

        /// <summary><c>tly_dumpmodel</c>: the whole model under every step, built on the side with the
        /// same inputs as <see cref="BuildAvailabilityModelFor"/> so the live model is not replaced.</summary>
        private void CmdDumpModel(string[] args)
            => TheLongestYear.DebugCommands.ModelDumpCommand.Run(
                this.Monitor, _enginePools, _effortData, _engineReachability,
                step => TheLongestYear.Core.Availability.ItemAvailabilityBuilder.Build(
                    _enginePools, seasonOverrides: _itemSeasonPins, effortData: _effortData,
                    hasKitchen: _meta.State.HasUpgrade("keep_kitchen"),
                    weekOverrides: _config.AvailabilityWeekOverrides,
                    mode: TheLongestYear.Core.WeekModes.For(step), step: step),
                this.Helper.DirectoryPath, args);

        /// <summary>Jeff, 2026-08-28: "define can't exist; list all of the items in all of the bundles
        /// and when the first possible time you can get them is." One row per ingredient of every
        /// bundle on the live board: the model's earliest season (a hard floor only when the model
        /// derived it), the catalog's spawn seasons, the season the gate demands it, and the basis.</summary>
        private void CmdDumpAvailability(string command, string[] args)
        {
            if (!Context.IsWorldReady || _availability == null)
            {
                this.Monitor.Log("Load a save first (the availability model is derived from live game data).", LogLevel.Warn);
                return;
            }
            var requirements = _runController?.Requirements ?? _requirements;
            if (requirements == null || requirements.Count == 0) { this.Monitor.Log("No requirements on this save yet.", LogLevel.Warn); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# The Longest Year: live board item availability");
            sb.AppendLine();
            sb.AppendLine($"Generated by `tly_dumpavailability` (mod version {this.ModManifest.Version}), loop seed {_meta?.Run?.Seed}.");
            sb.AppendLine();
            sb.AppendLine($"Week mode: **{_availability.Mode}** (spec 2026-08-28-obtainable-board, section 1).");
            sb.AppendLine();
            sb.AppendLine("**Week** is the pacing week (1 to 16): the week a normal player reasonably has the item. **Hard** is the first week the item can exist at all (facts: crop and forage seasons, fish seasons and locations, festival dates, Jeff's location rulings; falls back to Week when no hard week was placed). **Gate** the season a day-28 gate may first demand it under the current week mode (the deep mine and the Skull Cavern gate later than their goal week). **Placed** says who decided: `derived` (fish, crab-pot, metals from game data), `rule` (a Phase 2 rule: mines, geodes, monsters, artifacts, animals, machines, dishes, ponds, crops, forage, saplings), `judgement` (a Phase 2 row that is Jeff's own placement rather than a game-data fact -- a hand-ruled AvailabilityWeeks table entry or a note still awaiting his sign-off, listed again below so he can find every one), `override` (a pin or AvailabilityWeekOverrides), or `UNKNOWN` (nothing placed it: the gate treats it as Winter and it is listed at the end for Jeff to rule on). **Catalog seasons** are the spawn seasons the bundle catalog assigned (`any` = year-round). **Due** is the season the day-28 gate demands the item (per-item pin), the bundle's season (seasonal), or the quota ramp (pick-X-of-Y).");
            sb.AppendLine();
            var unknown = new List<string>();
            var judgement = new List<string>();
            foreach (BundleRequirement req in requirements)
            {
                string ramp = req.CumulativeRequiredBySeason != null ? $" ramp [{string.Join(", ", req.CumulativeRequiredBySeason)}]" : "";
                sb.AppendLine($"## {req.Name} ({req.Kind}, {req.NumberOfSlots} of {req.Ingredients.Count}{ramp})");
                sb.AppendLine();
                sb.AppendLine("| Item | Id | Week | Hard | Gate | Placed | Catalog seasons | Due | Effort | Basis |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                foreach (string id in req.Ingredients)
                {
                    TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                    string placed = _availability.IsDerived(id) ? "derived"
                        : !_availability.IsPlaced(id) ? "UNKNOWN"
                        : a.Basis.Contains("override", StringComparison.Ordinal) ? "override"
                        : TheLongestYear.Core.AvailabilityWeeks.IsJudgementBasis(a.Basis) ? "judgement" : "rule";
                    if (placed == "UNKNOWN") unknown.Add($"- {DisplayName(id)} ({id}), in {req.Name}");
                    if (placed == "judgement") judgement.Add($"- {DisplayName(id)} ({id}), week {a.PacingWeek}, in {req.Name}");
                    string due = req.StretchLines.TryGetValue(id, out TheLongestYear.Core.Season stretchSeason) ? $"stretch ({stretchSeason})"
                        : req.Kind == BundleKind.Seasonal && req.SeasonalSeason.HasValue ? req.SeasonalSeason.Value.ToString()
                        : req.ItemSeasonPins != null && req.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season d) ? d.ToString()
                        : req.Kind == BundleKind.Percentage ? "ramp" : "never";
                    string catalogSeasons = "any";
                    foreach (var cc in _catalog)
                        if (cc.Id == id) { catalogSeasons = cc.ObtainableSeasons == null || cc.ObtainableSeasons.Count == 4 ? "any" : string.Join("/", cc.ObtainableSeasons.OrderBy(x => (int)x)); break; }
                    sb.AppendLine($"| {DisplayName(id)} | {id} | {a.PacingWeek} | {a.HardWeekOrPacing} | {a.Gate} | {placed} | {catalogSeasons} | {due} | {a.Effort} ({a.Source}) | {a.Basis.Replace("|", "/")} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"## Judgement rows ({judgement.Count})");
            sb.AppendLine();
            sb.AppendLine("Rows placed by Jeff's own ruling rather than a game-data fact: a hand-ruled AvailabilityWeeks table entry (\"table, ...\" basis) or a late-floor note still awaiting his sign-off (\"(for Jeff to confirm)\" basis). Not unknown -- they gate and appear on cards like any other rule -- but worth a second look.");
            sb.AppendLine();
            foreach (string line in judgement) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"## Unknown items ({unknown.Count})");
            sb.AppendLine();
            sb.AppendLine("Nothing placed these; the gate treats each as Winter. Jeff rules on every one (memory tly-sim-list-unknowns-each-run); a ruling becomes an AvailabilityWeeks row or an AvailabilityWeekOverrides default.");
            sb.AppendLine();
            foreach (string line in unknown) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"## Rejected overrides ({_availability.RejectedSeasonOverrides.Count})");
            sb.AppendLine();
            foreach (string id in _availability.RejectedSeasonOverrides) sb.AppendLine($"- {DisplayName(id)} ({id}): {_availability.For(id).Basis}");
            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "board-availability.md";
            string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
            System.IO.File.WriteAllText(path, sb.ToString());
            this.Monitor.Log($"tly_dumpavailability: wrote {path} ({sb.Length:N0} chars, {requirements.Count} bundles, {judgement.Count} judgement row(s), {unknown.Count} unknown item(s)).", LogLevel.Info);
        }

        /// <summary><c>tly_dumpeffort [fileName]</c>: the item effort review document, written to the
        /// mod folder like tly_dumpbundles (copy to docs/item-effort-model.md; gitignored).</summary>
        private void CmdDumpEffort(string command, string[] args)
        {
            if (!Context.IsWorldReady || _availability == null || _enginePools == null || _effortData == null)
            {
                this.Monitor.Log("Load a save first (the effort model is derived from live game data).", LogLevel.Warn);
                return;
            }
            string text = TheLongestYear.Debug.EffortDocWriter.Render(
                _enginePools, _effortData.Objects, _availability, this.ModManifest.Version.ToString());
            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "item-effort-model.md";
            string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
            System.IO.File.WriteAllText(path, text);
            this.Monitor.Log($"tly_dumpeffort: wrote {path} ({text.Length:N0} chars).", LogLevel.Info);
        }
    }
}
