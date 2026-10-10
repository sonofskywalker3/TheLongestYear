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
        /// <summary>Debug: what does each mushroom (and a fruit or two) actually dry into? Answers
        /// whether a dried mushroom is one generic item or one per mushroom, and whether vanilla's
        /// PreserveType name "DriedMushroom" resolves as an item id for the icon lookup, which is
        /// what decided that mushrooms could not be a flavored slot in 0.18.34. Read-only.</summary>
        private void CmdDriedProbe(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            foreach (string id in new[] { "DriedMushroom", "DriedMushrooms", "DriedFruit", "SmokedFish" })
            {
                var meta = ItemRegistry.GetMetadata(id);
                this.Monitor.Log(
                    $"tly_driedprobe: id '{id}' -> metadata {(meta == null ? "(null)" : meta.QualifiedItemId + " type=" + meta.TypeIdentifier)}",
                    meta == null ? LogLevel.Warn : LogLevel.Info);
            }

            // One entry per edible mushroom, plus two fruits as a control.
            foreach (string preserve in new[] { "257", "281", "404", "420", "422", "258", "613" })
            {
                string baseName = preserve == "258" || preserve == "613" ? "DriedFruit" : "DriedMushroom";
                Item made = Utility.CreateFlavoredItem(baseName, preserve, 0, 1);
                string source = ItemRegistry.GetDataOrErrorItem("(O)" + preserve).DisplayName;
                if (made is StardewValley.Object obj)
                    this.Monitor.Log(
                        $"  {source} ({preserve}) + {baseName} -> \"{made.DisplayName}\" qualifiedId={made.QualifiedItemId} " +
                        $"spriteIndex={ItemRegistry.GetDataOrErrorItem(made.QualifiedItemId).SpriteIndex} preserved={obj.preservedParentSheetIndex.Value}",
                        LogLevel.Info);
                else
                    this.Monitor.Log($"  {source} ({preserve}) + {baseName} -> (did not resolve)", LogLevel.Warn);
            }
        }

        /// <summary>Debug: what fruit, mushroom or fish does each flavored slot of the live board
        /// name (plan 2026-09-21-flavored-bundle-slots)? Read-only.
        ///
        /// Constructs the note menu exactly as <see cref="CmdRingTest"/> does, because a Bundle only
        /// exists while a JunimoNoteMenu does, and the Bundle constructor is what
        /// FlavoredSlotPatch hooks. So this proves the patch actually fired on the live board,
        /// not merely that the map was stamped.</summary>
        private void CmdFlavors(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            IReadOnlyDictionary<string, string> map = _meta.State.WrittenBoardFlavors;
            this.Monitor.Log(
                $"tly_flavors: stamped map = {(map == null ? "(null: a board written before 0.18.33, or Vanilla mode)" : map.Count + " slot(s)")}.",
                LogLevel.Info);
            if (map != null)
                foreach (KeyValuePair<string, string> entry in map.OrderBy(e => e.Key, StringComparer.Ordinal))
                    this.Monitor.Log($"  stamped {entry.Key} -> {entry.Value}", LogLevel.Info);

            var cc = Game1.RequireLocation<StardewValley.Locations.CommunityCenter>("CommunityCenter");
            int flavored = 0, bare = 0;
            for (int area = 0; area <= 5; area++)
            {
                var note = new JunimoNoteMenu(area, cc.bundlesDict());
                foreach (Bundle b in note.bundles)
                {
                    for (int i = 0; i < b.ingredients.Count; i++)
                    {
                        StardewValley.Menus.BundleIngredientDescription ing = b.ingredients[i];
                        // Both kinds: a slot that names its input, and a slot that stays "any" and
                        // relies on the label instead. The label is the only thing the player has
                        // to go on for the second kind, so it has to be readable here too.
                        if (ing.id == null) continue;
                        if (!TheLongestYear.Core.FlavoredSlotRules.IsFlavored(ing.id)
                            && TheLongestYear.Core.FlavorlessBundleSlots.LabelKeyFor(ing.id) == null) continue;

                        string name;
                        if (ing.preservesId != null)
                        {
                            flavored++;
                            // Null when the id is not a PreserveType name, which is exactly the
                            // bug this command caught on 2026-09-21. Say so rather than throwing.
                            Item made = Utility.CreateFlavoredItem(ing.id, ing.preservesId, ing.quality, ing.stack);
                            name = made?.DisplayName ?? "(FLAVOR DID NOT RESOLVE)";
                        }
                        else
                        {
                            bare++;
                            name = ItemRegistry.GetDataOrErrorItem(TheLongestYear.Core.BundleParsing.NormalizeItemId(ing.id)).DisplayName;
                        }
                        // Prove the MATCH half too, not just the name: the right flavor must be
                        // accepted and a different one refused. Bundle.IsValidItemForThisIngredient
                        // Description is the exact check the note runs on a donation.
                        string accepts = "";
                        if (ing.preservesId != null)
                        {
                            Item right = Utility.CreateFlavoredItem(ing.id, ing.preservesId, 0, 1);
                            string otherId = ing.preservesId == "145" ? "132" : "145";
                            Item wrong = Utility.CreateFlavoredItem(ing.id, otherId, 0, 1);
                            Item plain = ItemRegistry.Create(TheLongestYear.Core.BundleParsing.NormalizeItemId(ing.id), 1);
                            accepts =
                                $" | accepts right={(right != null && b.IsValidItemForThisIngredientDescription(right, ing))}" +
                                $" wrong={(wrong != null && b.IsValidItemForThisIngredientDescription(wrong, ing))}" +
                                $" unflavored={(plain != null && b.IsValidItemForThisIngredientDescription(plain, ing))}";
                        }
                        // Does the slot get an icon in the required-items list? Vanilla only
                        // builds one when the ingredient id resolves as an object, and a flavored
                        // slot carries a PreserveType name instead. Reported per slot so a missing
                        // icon shows up in the log instead of needing a screenshot.
                        string icon = "";
                        try
                        {
                            Game1.activeClickableMenu = note;
                            // setUpBundleSpecificPage APPENDS to ingredientList without clearing
                            // it (only gameWindowSizeChanged clears), so without this the lookup
                            // by myID finds the previous bundle's component and reports its hover.
                            note.ingredientList?.Clear();
                            AccessTools.Method(typeof(JunimoNoteMenu), "setUpBundleSpecificPage")
                                .Invoke(note, new object[] { b });
                            ClickableTextureComponent comp = note.ingredientList?
                                .FirstOrDefault(c => c != null && c.myID == 1000 + i);
                            // hoverText is what the player actually reads on the slot, so this is
                            // the only honest check of the "Any Dried Fruit" label for a bare slot
                            // and of the flavored name for a named one.
                            icon = $" | icon={comp != null} hover=\"{comp?.hoverText ?? "(no component)"}\"";
                        }
                        catch (System.Exception ex) { icon = $" | icon=THREW {ex.InnerException?.GetType().Name ?? ex.GetType().Name}"; }
                        finally { Game1.activeClickableMenu = null; }

                        this.Monitor.Log(
                            $"  live area {area} bundle {b.bundleIndex} '{b.name}' slot {i}: id={ing.id} preservesId={ing.preservesId ?? "(none)"} " +
                            $"stack={ing.stack} reads as \"{name}\"{accepts}{icon}",
                            ing.preservesId != null ? LogLevel.Info : LogLevel.Warn);
                    }
                }
                note.exitThisMenu(false);
            }
            this.Monitor.Log($"tly_flavors: {flavored} flavored slot(s), {bare} still bare.", flavored > 0 ? LogLevel.Info : LogLevel.Warn);
        }

        private void CmdRunState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PrintRunState();
        }

        private void CmdDifficulty(string command, string[] args)
        {
            DifficultySettings configured = _config.Difficulty;
            DifficultyProfile live = _meta?.State != null
                ? _meta.State.EffectiveDifficulty(_config)
                : DifficultyResolver.Resolve(configured, _config);
            bool stamped = _meta?.State?.Difficulty != null;

            this.Monitor.Log("=== The Longest Year: difficulty ===", LogLevel.Info);
            this.Monitor.Log(
                stamped
                    ? "  In force: the profile STAMPED on this save. Config changes apply at your next loop."
                    : "  In force: resolved live from config (this save has no stamp yet; the next reset writes one).",
                LogLevel.Info);

            this.Monitor.Log("  Step               configured -> in force", LogLevel.Info);
            LogStep("stack size", configured.StackSize, live.Steps.StackSize);
            LogStep("quality asks", configured.QualityAsks, live.Steps.QualityAsks);
            LogStep("required slots", configured.RequiredSlots, live.Steps.RequiredSlots);
            LogStep("item rarity", configured.ItemRarity, live.Steps.ItemRarity);
            LogStep("JP earned", configured.JpEarned, live.Steps.JpEarned);
            LogStep("shrine prices", configured.ShrinePrices, live.Steps.ShrinePrices);
            LogStep("starting gold", configured.StartingGold, live.Steps.StartingGold);
            LogStep("cart slots", configured.CartSlots, live.Steps.CartSlots);
            LogStep("hold prices", configured.HoldPrices, live.Steps.HoldPrices);
            LogStep("bundles per room", configured.BundleCount, live.Steps.BundleCount);

            this.Monitor.Log("  Resolved values in force:", LogLevel.Info);
            this.Monitor.Log(
                $"    asks: stack x{live.StackFactor}, quality x{live.QualityFactor}, " +
                $"required slots {(live.RequireAllSlots ? "ALL shown" : live.RequiredSlotsDelta.ToString("+0;-0;0"))}, " +
                $"rarity bias {live.RarityBias}, theme week discount {live.EffectiveWeeklyGoalStackDiscount():P0}",
                LogLevel.Info);
            this.Monitor.Log(
                $"    economy: JP x{live.JpEarnedFactor}, shrine prices x{live.ShrinePriceFactor}, " +
                $"starting gold {live.StartingGold}g, starting cart slots {live.StartingCartSlots}, " +
                $"hold prices x{live.HoldPriceFactor}",
                LogLevel.Info);

            this.Monitor.Log(
                $"  Board source: {_meta?.State?.BundleSource ?? BundleSourceNames.Engine}. " +
                "Item rarity and bundles per room apply to Engine (TLY Custom) boards only; stack size, quality asks and " +
                "required slots apply to vanilla boards too.",
                LogLevel.Info);

            void LogStep(string label, DifficultyStep configuredStep, DifficultyStep liveStep)
            {
                string note = configuredStep == liveStep ? "" : "   (pending: applies at your next loop)";
                this.Monitor.Log($"    {label,-18} {configuredStep,-8} -> {liveStep}{note}", LogLevel.Info);
            }
        }

        private void CmdNetState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            NetWorldState ws = Game1.netWorldState.Value;
            Quest quest = ws.QuestOfTheDay;
            StardewValley.Object dish = ws.DishOfTheDay;

            this.Monitor.Log("=== netWorldState audit probe ===", LogLevel.Info);
            this.Monitor.Log(
                $"  Game1 date: Y{Game1.year} {Game1.season} {Game1.dayOfMonth} @ {Game1.timeOfDay}, " +
                $"DaysPlayed={Game1.stats.DaysPlayed}, uniqueID={Game1.uniqueIDForThisGame}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  netWorldState date: Y{ws.Date.Year} {ws.Date.Season} {ws.Date.DayOfMonth} " +
                $"(NOTE: Date is a computed WorldDate.Now(), so this mirrors Game1 by construction)",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 59] QuestOfTheDay = {(quest == null ? "null (expected on Spring 1)" : quest.GetType().Name + " \"" + quest.questTitle + "\" reward=" + quest.moneyReward.Value + "g")}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 54] DishOfTheDay  = {(dish == null ? "null (expected on Spring 1)" : dish.Name + " x" + dish.Stack)}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 16] VisitsUntilY1Guarantee = {ws.VisitsUntilY1Guarantee} (-1 = guarantee not armed on this save)",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 39-42] walnuts={ws.GoldenWalnuts}/{ws.GoldenWalnutsFound} coconut={ws.GoldenCoconutCracked} " +
                $"buriedNuts={ws.FoundBuriedNuts.Count} islandVisitors={ws.IslandVisitors.Count}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 7-8] minesDifficulty={ws.MinesDifficulty} skullCavesDifficulty={ws.SkullCavesDifficulty}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 30-31,45,56] raccoonBundles=[{string.Join(",", ws.raccoonBundles)}] " +
                $"season={ws.SeasonOfCurrentRacconBundle} timesFed={ws.TimesFedRaccoons} " +
                $"lastFinishedDay={ws.DaysPlayedWhenLastRaccoonBundleWasFinished}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 43-44,46] miniBins={ws.MiniShippingBinsObtained} waivers={ws.PerfectionWaivers} totems={ws.TreasureTotemsUsed}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 49-53,57-58] builders={ws.Builders.Length} worldStateIDs={Game1.worldStateIDs.Count} (Game1 mirror) " +
                $"passiveFestivals={ws.ActivePassiveFestivals.Count} checkedGarbage={ws.CheckedGarbage.Count} " +
                $"canDrive={ws.canDriveYourselfToday.Value} clocksOff={ws.goldenClocksTurnedOff.Value}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 35-38] lowestMineLevel={ws.LowestMineLevel}/{ws.LowestMineLevelForOrder} " +
                $"museumPieces={ws.MuseumPieces.Length} lostBooks={ws.LostBooksFound}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [keeps] whichFarm={Game1.whichFarm} shuffleMineChests={ws.ShuffleMineChests} " +
                $"farmhandData={ws.farmhandData.Length} locationsWithBuildings={ws.LocationsWithBuildings.Count}",
                LogLevel.Info);

            // Weather: the live Game1 flags, netWorldState's own copy (synced by UpdateFromGame1
            // mid-reset), and the scheduler's pick for today/tomorrow so the three can be compared.
            var defaultWeather = ws.GetWeatherForLocation(NetStateDefaultWeatherContext);
            var tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays++;
            string scheduledToday = WeatherScheduleWriterPatch.ScheduledFor(Game1.Date) ?? "(vanilla)";
            string scheduledTomorrow = WeatherScheduleWriterPatch.ScheduledFor(tomorrow) ?? "(vanilla)";
            this.Monitor.Log(
                $"  [weather] live: raining={Game1.isRaining} lightning={Game1.isLightning} snowing={Game1.isSnowing} " +
                $"debris={Game1.isDebrisWeather} greenRain={Game1.isGreenRain}; " +
                $"netWorldState Default: today={defaultWeather.Weather} tomorrow={defaultWeather.WeatherForTomorrow}; " +
                $"Game1.weatherForTomorrow={Game1.weatherForTomorrow}; " +
                $"schedule: today={scheduledToday} tomorrow={scheduledTomorrow}",
                LogLevel.Info);

            // `tly_netstate army1 <n>`: arm the Traveling Cart year-1 guarantee window in memory so
            // a reset can be seen re-rolling it (a save that never enabled YearOneCompletable sits
            // at -1 and the reset leaves it alone, so there is nothing to watch otherwise).
            if (args.Length >= 2 && string.Equals(args[0], NetStateArmY1Arg, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[1], out int visits))
            {
                ws.VisitsUntilY1Guarantee = visits;
                this.Monitor.Log($"  [row 16] VisitsUntilY1Guarantee armed at {visits} (in memory; a reset re-rolls it).", LogLevel.Info);
            }
        }

        private const string NetStateDefaultWeatherContext = "Default";

        private const string NetStateArmY1Arg = "army1";

        private void CmdCatalog(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var byTheme = new Dictionary<TheLongestYear.Core.Theme, int>();
            foreach (CcItem item in _catalog)
                byTheme[item.Theme] = byTheme.TryGetValue(item.Theme, out int n) ? n + 1 : 1;

            this.Monitor.Log($"CC catalog: {_catalog.Count} items.", LogLevel.Info);
            foreach (var kvp in byTheme)
                this.Monitor.Log($"  {kvp.Key}: {kvp.Value}", LogLevel.Info);
        }

        /// <summary>Re-run the bundle catalog + requirement classification over whatever is in
        /// <c>Game1.netWorldState.Value.BundleData</c> RIGHT NOW and log the usual summary lines.
        /// Results go into locals only — the active run's catalog/requirements are untouched, so
        /// this is safe on a live save. Exists so an unattended session can verify remixed-bundle
        /// classification: 'debug ShuffleBundles' regenerates the bundles as Remixed in memory
        /// (never persisted unless the game saves), then this command classifies them.</summary>
        private void CmdClassify(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var builder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ParseThemeOverrides(),
                ParseItemSeasonPins(),
                ParseBundleQuotas(),
                _availability);
            IReadOnlyList<CcItem> catalog = builder.Build();
            IReadOnlyList<BundleRequirement> requirements = builder.BuildRequirements();
            this.Monitor.Log($"tly_classify: {catalog.Count} catalog items, {requirements.Count} requirements (diagnostics only — active run unchanged).", LogLevel.Info);
        }

        /// <summary>Diagnostics-only dry run of the owned-bundle engine for a given loop number:
        /// generates the set (nothing written or persisted — never touches live BundleData or
        /// MetaState), logs each room's picked bundle names + slot counts, the manifest
        /// classification summary, and a determinism self-check (regenerates off the SAME seed
        /// and diffs the two sets byte-for-byte). Mirrors <see cref="CmdClassify"/>'s design —
        /// locals only. Guarded exactly like tly_classify (requires a loaded save) because the
        /// seed basis is Game1.player.UniqueMultiplayerID, which doesn't exist at the title
        /// screen.
        ///
        /// The optional mode argument (<c>custom</c> default, <c>standard</c>, <c>remixed</c>)
        /// picks WHICH board is audited: the engine's own set, or the board vanilla would build
        /// for the matching Advanced Options dropdown choice
        /// (<see cref="TheLongestYear.Loop.BundleOptionPatch.Choice"/>). Both vanilla modes run
        /// the exact same listing, classification, gate audit and determinism self-check, and
        /// write nothing either.</summary>
        private void CmdGenBundles(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int seedLoop = _meta.State.EffectiveBundleSeedLoop;
            var mode = TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom;
            foreach (string arg in args)
            {
                if (int.TryParse(arg, out int parsedLoop))
                {
                    // Same rejection tly_reset makes: a seed loop is a count of completed resets,
                    // so a negative one is a typo, not a board.
                    if (parsedLoop < 0)
                    {
                        this.Monitor.Log($"tly_genbundles: '{arg}' is not a seed loop. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                        return;
                    }
                    seedLoop = parsedLoop;
                }
                else if (TheLongestYear.Loop.BundleOptionPatch.TryParseChoice(arg, out var parsedMode))
                    mode = parsedMode;
                else
                {
                    this.Monitor.Log($"tly_genbundles: unknown argument '{arg}'. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                    return;
                }
            }

            // Same seed basis as ResolveRequirements/WorldResetService.PerformReset — see
            // ResolveRequirements' comment for why (Game1.uniqueIDForThisGame is time-based and
            // re-seeded by our own reset every loop, so it can't be the basis).
            ulong seedBasis = unchecked((ulong)Game1.player.UniqueMultiplayerID);
            int seed = BundleEngineSeed.For(seedBasis, seedLoop);

            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins = ParseItemSeasonPins();
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas = ParseBundleQuotas();

            if (mode != TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom)
            {
                GenBundlesVanilla(mode, seedLoop, seed, itemSeasonPins, bundleQuotas);
                return;
            }

            // Diagnostics have to show what the loop actually runs under, so this uses the STAMPED
            // profile like every other generation path. A preview resolved from live config would
            // report a board the save is not playing.
            TheLongestYear.Core.DifficultyProfile genDifficulty = _meta.State.BoardDifficulty(_config);
            BundleGenerationTuning genTuning =
                TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, genDifficulty);
            var firstEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            firstEngine.Availability = _availability;
            firstEngine.AllowModItems = _meta.State.ModItemsOnBoard();
            GeneratedBundleSet first = firstEngine.Generate(seed, _meta.State.RandomBundleRewardsBoard);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, mode custom), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(firstEngine, first, itemSeasonPins, bundleQuotas);

            var secondEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            secondEngine.Availability = _availability;
            secondEngine.AllowModItems = _meta.State.ModItemsOnBoard();
            GeneratedBundleSet second = secondEngine.Generate(seed, _meta.State.RandomBundleRewardsBoard);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>The vanilla half of <see cref="CmdGenBundles"/>: build the board the GAME
        /// would build for the given Advanced Options dropdown choice, then run it through the
        /// same listing, classification, gate audit and determinism self-check the engine board
        /// gets. Nothing is written: neither path touches BundleData or MetaState.
        ///
        /// Standard is just <c>Data/Bundles</c> (what <c>Game1.GenerateBundles(Default)</c>
        /// hands to SetBundleData). Remixed runs the game's own <c>BundleGenerator</c> over
        /// <c>Data/RandomBundles</c>, seeded with vanilla's own formula
        /// (<c>CreateRandom(seed * 9.0)</c>) but off OUR per-loop seed rather than
        /// <c>Game1.uniqueIDForThisGame</c>: a TLY reset re-seeds uniqueIDForThisGame from the
        /// clock, so the game's own per-loop remix is not reproducible, while this is (and it
        /// varies per seed loop, which is the point of the diagnostic).</summary>
        private void GenBundlesVanilla(
            TheLongestYear.Loop.BundleOptionPatch.Choice mode,
            int seedLoop,
            int seed,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas)
        {
            bool remixed = mode == TheLongestYear.Loop.BundleOptionPatch.Choice.VanillaRemixed;
            string label = remixed ? "remixed" : "standard";
            // Standard reads Data/Bundles verbatim, so the seed never enters the board at all: every
            // seed loop yields the same board. Say that in the header line rather than printing a
            // seed the mode ignores.
            string modeLabel = remixed ? "mode remixed" : "mode standard (seed ignored: Data/Bundles is fixed)";

            System.Collections.Generic.IReadOnlyDictionary<string, string> firstData;
            System.Collections.Generic.IReadOnlyDictionary<string, string> secondData = null;
            try
            {
                firstData = remixed
                    ? TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed)
                    : TheLongestYear.Loop.VanillaBundlePool.LoadStandardBundleData();
                if (remixed)
                    secondData = TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_genbundles: could not build the vanilla {label} board ({ex.GetType().Name}: {ex.Message}).", LogLevel.Error);
                return;
            }

            GeneratedBundleSet first = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(firstData);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, {modeLabel}), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(null, first, itemSeasonPins, bundleQuotas, $"vanilla slots ({label})");

            // The self-check regenerates off the same seed and diffs. For standard that compares
            // Data/Bundles to itself, which can never fail and so proves nothing: skip it and say so.
            if (!remixed)
            {
                this.Monitor.Log(
                    "tly_genbundles: determinism self-check skipped for standard (Data/Bundles is fixed and the seed is ignored, so there is nothing to vary).",
                    LogLevel.Info);
                return;
            }

            GeneratedBundleSet second = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(secondData);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>Logs each room's picked bundle names + slot counts, then the manifest
        /// classification summary ("N generated, M classified, K skipped"). K counts every
        /// bundle <see cref="GeneratedBundleSet.BuildRequirements"/> drops (Vault/non-themed
        /// rooms are expected to drop — RoomThemeMap has no entry for them — so that's not
        /// logged as a problem); any drop INSIDE a themed room is unexpected (the engine
        /// authored every bundle, so nothing themed should ever fail classification) and is
        /// called out at WARN with a per-room breakdown.</summary>
        /// <param name="engine">The engine that produced the set, for its per-slot provenance.
        /// Null for a vanilla board (no engine ran); then <paramref name="slotSourceOverride"/>
        /// labels every slot line instead.</param>
        /// <param name="slotSourceOverride">Fixed slot-source label, e.g.
        /// "vanilla slots (remixed)". Null keeps the engine's per-bundle provenance.</param>
        private void LogGeneratedBundleSet(
            TheLongestYear.Loop.BundleEngine engine,
            GeneratedBundleSet set,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas,
            string slotSourceOverride = null)
        {
            var derivedSeasonPins = engine?.LastDerivedSeasonPins
                ?? (System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season>)
                   new Dictionary<string, TheLongestYear.Core.Season>(StringComparer.Ordinal);

            foreach (var roomGroup in set.Bundles.GroupBy(b => b.Room).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                this.Monitor.Log($"  {roomGroup.Key}:", LogLevel.Info);
                foreach (BundleSpec spec in roomGroup.OrderBy(b => b.Index))
                {
                    // Authored names are unique by construction (AuthoredBundleCatalog), so an
                    // exact Name match is sufficient here -- unlike Uniquify's " II"/" III"
                    // collision suffixes (which only ever apply to vanilla RandomBundles name
                    // collisions), an authored def's Name never gets suffixed.
                    string authoredTag = engine != null && TheLongestYear.Core.AuthoredBundleCatalog.All.Any(d => d.Name == spec.Name)
                        ? " [authored]"
                        : "";
                    this.Monitor.Log(
                        $"    [{spec.Index}] {spec.DisplayName} (pick {spec.NumberOfSlots} of {spec.Slots.Count}){authoredTag}",
                        LogLevel.Info);

                    // engine.LastDomains keyed by absolute index; missing key/None = vanilla slots
                    // (money bundles only since spec 2026-08-28-obtainable-board-3-pools). A Recipe
                    // pick names its recipe and its parts, so the log shows WHICH pool it drew from.
                    // An authored bundle is composed by AuthoredBundleComposer and is FINAL, so the
                    // engine files it under None as well; it does NOT keep vanilla slots, and
                    // saying so made the "no non-money bundle keeps vanilla slots" audit unreadable.
                    string source;
                    if (slotSourceOverride != null)
                        source = slotSourceOverride;
                    else if (authoredTag.Length > 0)
                        source = "authored slots";
                    else if (!engine.LastDomains.TryGetValue(spec.Index, out TheLongestYear.Core.DomainMatch m)
                        || m.Domain == TheLongestYear.Core.PoolDomain.None)
                        source = "vanilla slots";
                    else if (m.Domain == TheLongestYear.Core.PoolDomain.Recipe)
                        source = $"re-rolled from recipe {(engine.LastRecipes.TryGetValue(spec.Index, out string recipe) ? recipe : spec.Name)}";
                    else
                        source = $"re-rolled from {m.Domain}{(m.Season != null ? $"({m.Season})" : "")}";
                    this.Monitor.Log(
                        $"      {spec.Room}/{spec.Index} '{spec.Name}' [{spec.Slots.Count} slots, need {spec.NumberOfSlots}] — {source}",
                        LogLevel.Info);

                    // Vault money slots carry the GOLD amount in Quality with item id -1, so they
                    // showed up here as "-1 q25000" and read like an impossible quality ask. They are
                    // not asks at all: skip them (Jeff, 0.13.0 smoke).
                    var qualityAsks = spec.Slots
                        .Where(s => s.Quality > 0 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} q{s.Quality}").ToList();
                    if (qualityAsks.Count > 0)
                        this.Monitor.Log($"        quality asks: {string.Join(", ", qualityAsks)}", LogLevel.Info);

                    // Stack asks above 1, so a balance report can show what the stack-size
                    // difficulty modifier actually did. Money slots are excluded for the same
                    // reason as above: a Vault "stack" is a gold amount, not an ask.
                    var stackAsks = spec.Slots
                        .Where(s => s.Stack > 1 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} x{s.Stack}").ToList();
                    if (stackAsks.Count > 0)
                        this.Monitor.Log($"        stack asks: {string.Join(", ", stackAsks)}", LogLevel.Info);

                    // Every slot by name, so a log alone is enough to audit what the board asks
                    // for (bundle-loop audit, 2026-08-29). Money slots are gold amounts, not asks.
                    var slotNames = spec.Slots
                        .Where(s => s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{DisplayName(s.ItemId)} ({s.ItemId}) x{s.Stack}{(s.Quality > 0 ? $" q{s.Quality}" : "")}")
                        .ToList();
                    if (slotNames.Count > 0)
                        this.Monitor.Log($"        slots: {string.Join(", ", slotNames)}", LogLevel.Info);
                }
            }
            this.Monitor.Log($"  derived season pins in effect: {derivedSeasonPins.Count}", LogLevel.Info);

            IReadOnlyList<BundleRequirement> requirements = engine != null
                ? engine.BuildRequirements(set, itemSeasonPins, bundleQuotas, availability: _availability)
                : set.BuildRequirements(itemSeasonPins, bundleQuotas, availability: _availability);
            int generated = set.Bundles.Count;
            int classified = requirements.Count;
            int skipped = generated - classified;

            // The gates this board would run under, then the same audit tly_gatecheck runs on the
            // live board, so a diagnostic loop can be checked for IMPOSSIBLE gates without a reset.
            foreach (BundleRequirement req in requirements.OrderBy(r => r.Theme).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                string gates = req.Kind switch
                {
                    BundleKind.Seasonal => $"all by {req.SeasonalSeason}",
                    BundleKind.PerItem => string.Join(", ", req.ItemSeasonPins
                        .OrderBy(kv => (int)kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => $"{DisplayName(kv.Key)} by {kv.Value}")),
                    BundleKind.Percentage => $"ramp [{string.Join(",", req.CumulativeRequiredBySeason)}] of X={req.NumberOfSlots}",
                    _ => "",
                };
                this.Monitor.Log($"      gates {req.Name} ({req.Kind}): {gates}", LogLevel.Info);
            }
            var auditPins = new Dictionary<string, TheLongestYear.Core.Season>(derivedSeasonPins, StringComparer.Ordinal);
            foreach (var kv in itemSeasonPins)
                auditPins[kv.Key] = kv.Value;
            LogGateAudit(requirements, auditPins, "tly_genbundles", engine?.LastVanillaOnlyRecipes);

            var themedSkipsByRoom = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BundleSpec spec in set.Bundles)
            {
                if (!RoomThemeMap.TryGetTheme(spec.Room, out TheLongestYear.Core.Theme theme))
                    continue; // Vault / non-themed room — always classified out, not a problem.

                var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), BundleDataWriter.Value(spec));
                if (BundleClassifier.Classify(parsed, theme, itemSeasonPins, bundleQuotas, _availability) == null)
                    themedSkipsByRoom[spec.Room] = themedSkipsByRoom.TryGetValue(spec.Room, out int n) ? n + 1 : 1;
            }
            int themedSkipped = themedSkipsByRoom.Values.Sum();

            this.Monitor.Log(
                $"tly_genbundles: {generated} generated, {classified} classified, {skipped} skipped.",
                LogLevel.Info);
            if (themedSkipped > 0)
            {
                string breakdown = string.Join(", ", themedSkipsByRoom.Select(kv => $"{kv.Key}: {kv.Value}"));
                // A vanilla board can legitimately drop a themed bundle (category-only asks such
                // as "any fish"), so that is reported, not flagged as a defect; only the engine's
                // own board is expected to classify every themed bundle it authored.
                this.Monitor.Log(
                    engine != null
                        ? $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms (unexpected: {breakdown})."
                        : $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms ({breakdown}).",
                    engine != null ? LogLevel.Warn : LogLevel.Info);
            }
        }

        /// <summary>
        /// Diagnostics-only proof that the weapon/hat donation patches (see
        /// <see cref="TheLongestYear.Patches.BundleDonationPatches"/>) make (W)/(H) items valid CC
        /// ingredients. Creates ephemeral <c>(W)13</c>, <c>(H)8</c>, <c>(O)520</c> items via
        /// <c>ItemRegistry.Create</c> and a synthetic, DETACHED <see cref="Bundle"/> carrying Gil's
        /// Trophies' real ingredient composition (see
        /// <see cref="TheLongestYear.Core.AuthoredBundleCatalog.GilTrophies"/> / the Boiler Room
        /// authored def) via the simple
        /// <c>Bundle(name, displayName, ingredients, completedFlags, rewardListString)</c>
        /// constructor — chosen over the raw-BundleData-string overload because that one loads a
        /// texture and builds a <c>TemporaryAnimatedSprite</c> (Bundle.cs ~87-160), a side effect a
        /// diagnostic command shouldn't risk. The synthetic bundle is never added to
        /// <c>Game1.RequireLocation&lt;CommunityCenter&gt;("CommunityCenter").bundles</c>, so
        /// nothing here can touch the real board.
        ///
        /// For each id, logs PASS/FAIL for (a) <c>Bundle.IsValidItemForThisIngredientDescription</c>
        /// and (b) <c>Bundle.canAcceptThisItem</c> — the checks the highlight-wrapper's
        /// <c>ItemMatchesAnyNonObjectIngredient</c> and the vanilla pickup/click paths both rely on;
        /// (a) is the load-bearing check since (b) and the deposit path both call it internally.
        /// (c) <c>Bundle.tryToDepositThisItem</c> needs a live <see cref="JunimoNoteMenu"/> — its
        /// <c>onIngredientDeposit</c> callback is what breaks BEFORE the
        /// <c>communityCenter.bundles.FieldDict</c> persistence write (Bundle.cs:323-328), which is
        /// what would make a non-persisting deposit test safe — but constructing a
        /// <see cref="JunimoNoteMenu"/> headlessly loads a whole room's textures/bundle set as a
        /// side effect of a debug command, so (c) is skipped and logged rather than risking that.
        /// Requires a loaded save (world-ready gate).
        /// </summary>
        private void CmdTrophyTest(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_config == null) { this.Monitor.Log("Config unavailable.", LogLevel.Warn); return; }

            bool wrapperActive = RunActivation.IsActive && _config.EnableNonObjectDonations;
            this.Monitor.Log(
                $"tly_trophytest: highlight-wrapper active={wrapperActive} " +
                $"(RunActivation.IsActive={RunActivation.IsActive}, EnableNonObjectDonations={_config.EnableNonObjectDonations}).",
                LogLevel.Info);

            string[] ids = { "(W)13", "(H)8", "(O)520" };
            var ingredients = new List<BundleIngredientDescription>();
            foreach (string id in ids)
                ingredients.Add(new BundleIngredientDescription(id, 1, 0, completed: false));
            var completedFlags = new bool[ingredients.Count];

            Bundle synthetic;
            try
            {
                synthetic = new Bundle(
                    "Gil's Trophies (tly_trophytest)",
                    "Gil's Trophies (tly_trophytest)",
                    ingredients,
                    completedFlags,
                    "O 879 5");
            }
            catch (Exception ex)
            {
                this.Monitor.Log(
                    $"tly_trophytest: couldn't construct the synthetic Bundle: {ex.GetType().Name}: {ex.Message}. Aborting.",
                    LogLevel.Error);
                return;
            }

            int pass = 0, total = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                Item item;
                try { item = ItemRegistry.Create(id, 1); }
                catch (Exception ex)
                {
                    total++;
                    this.Monitor.Log($"tly_trophytest [{id}]: couldn't create item — {ex.Message}. FAIL.", LogLevel.Error);
                    continue;
                }

                BundleIngredientDescription ingredient = synthetic.ingredients[i];

                bool validA = synthetic.IsValidItemForThisIngredientDescription(item, ingredient);
                total++;
                if (validA) pass++;
                this.Monitor.Log(
                    $"tly_trophytest [{id}] (a) IsValidItemForThisIngredientDescription = {(validA ? "PASS" : "FAIL")}.",
                    validA ? LogLevel.Info : LogLevel.Warn);

                // canAcceptThisItem accepts a null slot (its gate is "slot == null || slot.item == null"),
                // so no ClickableTextureComponent needs to be constructed for this check.
                bool validB = synthetic.canAcceptThisItem(item, null);
                total++;
                if (validB) pass++;
                this.Monitor.Log(
                    $"tly_trophytest [{id}] (b) canAcceptThisItem = {(validB ? "PASS" : "FAIL")}.",
                    validB ? LogLevel.Info : LogLevel.Warn);
            }

            this.Monitor.Log(
                "tly_trophytest (c) tryToDepositThisItem: deposit check skipped (needs live menu) — " +
                "constructing a headless JunimoNoteMenu would load a whole room's textures/bundle set " +
                "as a side effect of a debug command; (a)+(b) already exercise the ingredient-matching " +
                "logic the deposit path shares (Bundle.IsValidItemForThisIngredientDescription's id-branch).",
                LogLevel.Info);

            this.Monitor.Log($"tly_trophytest: {pass}/{total} PASS", pass == total ? LogLevel.Info : LogLevel.Error);
        }

        /// <summary>Compares two engine-generated sets by their written BundleData key/value
        /// pairs (the canonical form the game itself would see) and returns a description of the
        /// first difference found, or null if they're identical.</summary>
        private static string FirstBundleSetDifference(GeneratedBundleSet a, GeneratedBundleSet b)
        {
            IReadOnlyDictionary<string, string> dataA = a.ToBundleData();
            IReadOnlyDictionary<string, string> dataB = b.ToBundleData();

            foreach (string key in dataA.Keys.Union(dataB.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool inA = dataA.TryGetValue(key, out string valueA);
                bool inB = dataB.TryGetValue(key, out string valueB);
                if (!inA)
                    return $"key '{key}' is present in the second generation but missing from the first.";
                if (!inB)
                    return $"key '{key}' is present in the first generation but missing from the second.";
                if (valueA != valueB)
                    return $"key '{key}' differs: '{valueA}' vs '{valueB}'.";
            }
            return null;
        }

        /// <summary>Diagnostics: read/set MetaState.BundleSource + VanillaBundleType and the
        /// save's chosen source in memory so an unattended smoke can reset in each mode.</summary>
        private void CmdBundleSource(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length >= 1)
                _meta.State.BundleSource = BundleSourceNames.Normalize(args[0]);
            if (args.Length >= 2)
                _meta.State.VanillaBundleType = string.Equals(args[1], "Remixed", StringComparison.OrdinalIgnoreCase)
                    ? Game1.BundleType.Remixed.ToString() : Game1.BundleType.Default.ToString();
            if (args.Length >= 1)
                _meta.State.ChosenBundleSource = BundleSourceNames.IsVanilla(_meta.State.BundleSource)
                    ? BundleSourceNames.ForVanillaType(_meta.State.VanillaBundleType)
                    : BundleSourceNames.Engine;
            this.Monitor.Log(
                $"tly_bundlesource: save BundleSource={_meta.State.BundleSource}, VanillaBundleType={_meta.State.VanillaBundleType ?? "(unknown)"}, " +
                $"chosen={_meta.State.ChosenBundleSource ?? "(none)"}, config default={_config.BundleSource}, marker={_meta.State.BundlesGeneratedForReset}, loop={_meta.State.CompletedResets}.",
                LogLevel.Info);
        }
    }
}
