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
        /// (docs/design/2026-08-26-networldstate-field-rulings.md). Read-only. Exists because
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
            LogYearTwoLine(itemId);
        }

        /// <summary>The <c>{id}: year 2: ...</c> line, when the lexicon serves a year-2 answer for the item.</summary>
        private void LogYearTwoLine(string itemId)
        {
            TheLongestYear.Core.ItemAvailability y2 = _lexicon?.YearTwoFor(itemId);
            if (y2 == null) return;
            this.Monitor.Log(
                $"{itemId}: year 2: earliest {y2.EarliestSeason} (week {y2.EarliestWeek}), effort {y2.Effort} ({y2.Source}), tier {TierLabel(itemId, y2)} [{y2.Basis}]",
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
                    weekOverrides: AvailabilityWeekOverrides(),
                    mode: TheLongestYear.Core.WeekModes.For(step), step: step,
                    hasUpgrade: _meta.State.HasUpgrade),
                this.Helper.DirectoryPath, args, OriginLine());

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

        /// <summary>Constructs the path lexicon (which loads the supported-mod layers) once per session.
        /// Called before the load-time pools are built, so the layers' year-2 maps and shops are known
        /// to the location walk. A failure is logged and leaves it null; <see cref="BuildObtainabilityModel"/>
        /// tries again and reports its own failure.</summary>
        private void EnsureLexicon()
        {
            try
            {
                _lexicon ??= new TheLongestYear.Loop.LexiconService(this.Monitor, this.Helper.DirectoryPath, this.Helper.ModRegistry, BuildLexiconVariant);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Lexicon: could not load the layers before the pools ({ex.GetType().Name}: {ex.Message}); no layer map or shop leaves the year-1 walk this load.", LogLevel.Warn);
            }
        }

        /// <summary>The supported-mod layers' year-2 maps and shops for every pool build, or null
        /// (none) before the lexicon exists.</summary>
        private TheLongestYear.Core.Obtainability.YearTwoMarks LayerMarks() => _lexicon?.Loaded.Set.Marks;

        /// <summary>The supported-mod layers' daily shop picks for every pool build, or null (none)
        /// before the lexicon exists.</summary>
        private System.Collections.Generic.IReadOnlyList<TheLongestYear.Core.Lexicon.RandomShopItem> LayerRandomShopPicks() => _lexicon?.Loaded.Set.RandomShopItems;

        /// <summary>Reads the obtainability inputs from the live game data and publishes the models
        /// the path lexicon resolves them to: the shipped answers when the data is the unmodded game's,
        /// else the disk cache entry for this exact data, else a build (<see cref="BuildLexiconVariant"/>).</summary>
        private void BuildObtainabilityModel()
        {
            _obtainabilityReadMs = _obtainabilityBuildMs = _yearSortMs = 0;
            _lastLexiconBuild = null;
            _lexicon?.Forget();
            try
            {
                _lexicon ??= new TheLongestYear.Loop.LexiconService(this.Monitor, this.Helper.DirectoryPath, this.Helper.ModRegistry, BuildLexiconVariant);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                // A shop line a layer names as a daily pick reads as every item it can pick, so the
                // data, its row hashes and everything built from it are the same on any day.
                var inputs = _lexicon.Loaded.Set.Normalize(new TheLongestYear.Loop.GameObtainabilityData(this.Monitor).Build());
                _lastObtainabilityInputs = inputs;
                long readMs = timer.ElapsedMilliseconds;
                _obtainabilityReadMs = readMs;
                TheLongestYear.Core.Lexicon.Resolved resolved = _lexicon.Resolve(
                    inputs, _meta.State.HasUpgrade(PierreYear2SeedsService.UpgradeId), Game1.version, this.ModManifest.Version.ToString());
                _obtainability = resolved.Variant.ObtainModel();
                _yearSortModel = resolved.Variant.YearSortModel();
                _obtainabilityUnresolved = resolved.Variant.Unresolved;
                _obtainabilityFailure = null;
                _obtainabilityBuildMs = timer.ElapsedMilliseconds - readMs - _yearSortMs;
                TheLongestYear.Core.Obtainability.ObtainabilityBuild built = _lastLexiconBuild;
                string origin = resolved.Origin == TheLongestYear.Core.Lexicon.AnswerOrigin.Computed
                    ? (_lastLexiconBuildReused ? ", unchanged data, reused" : "")
                    : $", {resolved.Origin.ToString().ToLowerInvariant()} answers";
                this.Monitor.Log($"Year sort model: {_yearSortModel.Count} items{origin} ({timer.ElapsedMilliseconds} ms total).", LogLevel.Trace);
                bool hitCap = built?.HitPassCap == true;
                this.Monitor.Log(
                    $"Obtainability model: {_obtainability.Count} items in {timer.ElapsedMilliseconds} ms (read {readMs} ms{origin}), {resolved.Variant.Passes} pass(es), " +
                    $"{_obtainabilityUnresolved.Count} unresolved source(s)" + (hitCap ? ", STOPPED AT THE PASS CAP" : "") + ".",
                    hitCap ? LogLevel.Warn : LogLevel.Info);
            }
            catch (TheLongestYear.Core.Obtainability.ObtainabilityReadException rex)
            {
                _obtainability = null;
                _yearSortModel = null;
                _obtainabilityUnresolved = System.Array.Empty<string>();
                _obtainabilityFailure = rex.Message;
                this.Monitor.Log(rex.Message, LogLevel.Warn);
            }
            catch (Exception ex)
            {
                _obtainability = null;
                _yearSortModel = null;
                _obtainabilityUnresolved = System.Array.Empty<string>();
                _obtainabilityFailure = null;   // not a read failure; an older run's section list would be a lie
                this.Monitor.Log($"Obtainability model: build failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
            }
        }

        /// <summary>The lexicon's build for live data it has no answers for: the cold obtainability and
        /// year-sort builds (each memoised for the session by input hash, so a reload of unchanged data
        /// reuses them) with the supported-mod layers' extra direct sources and year-2 map and shop flags,
        /// snapshotted with this save's map and shop weeks.</summary>
        private TheLongestYear.Core.Lexicon.AnswerVariant BuildLexiconVariant(
            TheLongestYear.Core.Obtainability.ObtainabilityInputs inputs,
            IReadOnlyDictionary<string, IReadOnlyList<TheLongestYear.Core.Obtainability.ObtainSource>> extraDirect,
            TheLongestYear.Core.Obtainability.YearTwoMarks marks)
        {
            var build = _obtainabilityCache.Get(inputs, out bool reused, extraDirect, marks);
            var sortTimer = System.Diagnostics.Stopwatch.StartNew();
            var yearSort = _yearSortCache.Get(TheLongestYear.Core.YearTwoItems.WithoutCartRandomStock(inputs), out _, extraDirect, marks);
            _yearSortMs = sortTimer.ElapsedMilliseconds;
            _lastLexiconBuild = build;
            _lastLexiconBuildReused = reused;
            return TheLongestYear.Core.Lexicon.AnswerVariantFactory.From(
                build, yearSort, inputs, _enginePools?.LocationWeeks, DataLocationKeys(), _enginePools?.ShopWeeks);
        }

        /// <summary>Every Data/Locations key, the same list <see cref="Loop.GameDataPools"/> reads.</summary>
        private static List<string> DataLocationKeys()
            => Game1.content.Load<Dictionary<string, StardewValley.GameData.Locations.LocationData>>("Data/Locations").Keys.ToList();

        /// <summary>The loaded save's facts <c>tly_lexicon write vanilla</c> composes in, or null with no save.
        /// The maps are every Data/Locations key, the same list <see cref="Loop.GameDataPools"/> reads.</summary>
        private TheLongestYear.DebugCommands.LexiconSaveContext LexiconSaveContextNow()
        {
            if (!Context.IsWorldReady || _meta?.State == null) return null;
            var maps = DataLocationKeys();
            return new TheLongestYear.DebugCommands.LexiconSaveContext(
                _meta.State.HasUpgrade(PierreYear2SeedsService.UpgradeId),
                _enginePools?.LocationWeeks, _enginePools?.ShopWeeks, maps,
                Game1.version, this.ModManifest.Version.ToString(), AllAvailabilityAnswers, AvailabilityInputsNow, _lexicon?.Loaded, _lexicon?.Vanilla, _lexicon);
        }

        /// <summary>The "origin: ...; layers: ..." line the model dumps start with.</summary>
        private string OriginLine() => _lexicon?.OriginLine ?? "origin: none; layers: vanilla";

        /// <summary><c>tly_obtain &lt;itemId&gt;</c>: every source the obtainability model has for one item.</summary>
        private void CmdObtain(string command, string[] args)
        {
            if (!Context.IsWorldReady || _obtainability == null)
            {
                this.Monitor.Log(_obtainability == null && _obtainabilityFailure != null
                    ? _obtainabilityFailure                              // already starts "Obtainability: no model published..."
                    : "Load a save first (the obtainability model is built at save load).", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: tly_obtain <itemId> [startDay 1-112] | tly_obtain compare [fileName]", LogLevel.Info);
                return;
            }
            if (args[0] == "compare")
            {
                if (_availability == null || _enginePools == null)
                {
                    this.Monitor.Log("tly_obtain compare: the existing item model is not built yet.", LogLevel.Warn);
                    return;
                }
                TheLongestYear.Core.ItemPools pools = _enginePools;
                IEnumerable<string> poolIds = new[]
                    {
                        pools.Crops, pools.Fish, pools.CrabPot, pools.Forage, pools.MonsterDrops, pools.Metals,
                        pools.ArtisanGoods, pools.Artifacts, pools.Books, pools.Saplings, pools.GeodeMinerals,
                        pools.Cooking, pools.TapperGoods, pools.WinterOnly,
                    }
                    .SelectMany(list => list).Select(item => item.ItemId);
                var rows = TheLongestYear.Core.ObtainabilityComparison.Compare(
                    _availability.KnownIds.Concat(poolIds),
                    id => _availability.IsPlaced(id),
                    id => { var a = _availability.For(id); return (a.PacingWeek, a.HardWeekOrPacing, a.Basis); },
                    _obtainability);
                string report = TheLongestYear.Core.ObtainabilityComparison.Render(
                    rows, _obtainability, _obtainabilityUnresolved, id => ItemRegistry.GetData(id)?.DisplayName,
                    this.ModManifest.Version.ToString(), OriginLine());
                string fileName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : "obtainability-compare.md";
                string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
                System.IO.File.WriteAllText(path, report);
                string counts = string.Join(", ", rows.GroupBy(r => r.Verdict).Select(g => $"{g.Key} {g.Count()}"));
                this.Monitor.Log($"tly_obtain compare: wrote {path} ({rows.Count} items: {counts}; {_obtainabilityUnresolved.Count} unresolved).", LogLevel.Info);
                return;
            }
            int startDay = 1;
            if (args.Length > 1)
            {
                if (!int.TryParse(args[1], out startDay) || startDay < 1 || startDay > TheLongestYear.Core.Obtainability.DayTable.Days)
                {
                    this.Monitor.Log($"tly_obtain: startDay must be an integer 1-{TheLongestYear.Core.Obtainability.DayTable.Days}.", LogLevel.Warn);
                    return;
                }
            }
            LogYearTwoLine(TheLongestYear.Core.BundleParsing.NormalizeItemId(args[0]));   // the year-2 answer reads first
            this.Monitor.Log(
                OriginLine() + System.Environment.NewLine
                + TheLongestYear.Core.Obtainability.ObtainabilityText.Describe(args[0], _obtainability, id => ItemRegistry.GetData(id)?.DisplayName, startDay),
                LogLevel.Info);
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
