using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Darkness pushback (spec 2026-09-09): the night pass that rolls the three fronts and
    /// the morning report that tells the player what was taken. Blight kills crops (BlightPass),
    /// reversion opens a filled slot on the board and re-mirrors the ledger, tampering rewrites an
    /// unfilled slot's item on the board and in the stored copy, then asks ModEntry to rebuild
    /// the catalog and requirements. Each front has its own config switch and its own ward.
    /// Host only, single player or master, and never on a day 28 or the win night (the caller
    /// decides that: <see cref="RunController.OnDayEnding"/> calls <see cref="RunNight"/> only on
    /// a Continue verdict).</summary>
    internal sealed class SabotageService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly Func<IReadOnlyList<BundleRequirement>> _requirements;
        private readonly Func<ItemAvailabilityModel> _availability;
        private readonly Func<ItemPools> _pools;
        private readonly Action<string> _rebuildBoard;
        private readonly SabotageMailService _mail;
        /// <summary>Starts the board-changed porch scene; set by ModEntry (the season-turn driver).</summary>
        public Action<string, string, Action> StartTamperScene { get; set; }

        private RunState Run => _store.Run;
        private MetaState Meta => _store.State;

        public SabotageService(
            IMonitor monitor, MetaStore store, GameplayConfig config,
            Func<IReadOnlyList<BundleRequirement>> requirements,
            Func<ItemAvailabilityModel> availability,
            Func<ItemPools> pools,
            Action<string> rebuildBoard,
            SabotageMailService mail)
        {
            _mail = mail;
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _rebuildBoard = rebuildBoard ?? throw new ArgumentNullException(nameof(rebuildBoard));
        }

        private static bool HostCanAct() => Game1.IsMasterGame && !Game1.IsMultiplayer;

        public bool Enabled(SabotageKind kind) => kind switch
        {
            SabotageKind.Blight => _config.EnableBlight,
            SabotageKind.Reversion => _config.EnableBundleReversion,
            SabotageKind.Tampering => _config.EnableRequirementTampering,
            _ => false,
        };

        // ------------------------------------------------------------------ the night pass

        /// <summary>Roll every front for tonight. Effects land before the save; reports queue for
        /// the morning.</summary>
        public void RunNight()
        {
            if (!RunActivation.IsActive || !HostCanAct()) return;
            CoreSeason season = Run.Season;
            int day = Run.DayOfMonth;
            int dayOfYear = Calendar.DayOfYear((int)season, day);
            int week = Run.WeekOfYear;

            if (Enabled(SabotageKind.Blight))
            {
                Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear, SabotageKind.Blight);
                if (SabotageSchedule.StrikesTonight(SabotageKind.Blight, Run, season, day, rng))
                {
                    // The ward covers crops in the ground, not chests (Jeff, 2026-09-09).
                    int crops = CropsWarded(season) ? 0 : BlightPass.CountFor(season);
                    int spoil = BlightRule.SpoilCount(SpoilagePass.StoredUnits());
                    if (Blight(crops, spoil, rng) > 0)
                        SabotageSchedule.RecordStrike(SabotageKind.Blight, Run, week, dayOfYear);
                }
            }

            if (Enabled(SabotageKind.Reversion))
            {
                Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear, SabotageKind.Reversion);
                if (SabotageSchedule.StrikesTonight(SabotageKind.Reversion, Run, season, day, rng)
                    && Revert(rng))
                    SabotageSchedule.RecordStrike(SabotageKind.Reversion, Run, week, dayOfYear);
            }

            if (Enabled(SabotageKind.Tampering))
            {
                Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear, SabotageKind.Tampering);
                if (SabotageSchedule.StrikesTonight(SabotageKind.Tampering, Run, season, day, rng)
                    && Tamper(rng, dayOfYear))
                    SabotageSchedule.RecordStrike(SabotageKind.Tampering, Run, week, dayOfYear);
            }
        }

        private bool CropsWarded(CoreSeason season)
        {
            string ward = WardIds.CropWardFor(season);
            return ward != null && Meta.HasUpgrade(ward);
        }

        // ------------------------------------------------------------------ blight

        /// <summary>Kill <paramref name="crops"/> crops and spoil <paramref name="spoil"/> stored
        /// units now, then queue the report. Returns how many things were taken in all. Also the
        /// debug entry point (<c>tly_sabotage blight [crops] [spoil]</c>).</summary>
        public int Blight(int crops, int spoil, Random rng)
        {
            int killed = BlightPass.Strike(crops, rng);
            SpoilagePass.Taken taken = SpoilagePass.Strike(spoil, rng);
            if (killed <= 0 && taken.Total <= 0)
            {
                _monitor.Log("Darkness: blight rolled but found nothing to strike or take.", LogLevel.Trace);
                return 0;
            }
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Blight, Count = killed, Spoiled = taken.Spoiled, Missing = taken.Missing,
            });
            _monitor.Log($"Darkness: {killed} crop(s) struck down, {taken.Spoiled} stored unit(s) spoiled, {taken.Missing} gone missing on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return killed + taken.Total;
        }

        // ------------------------------------------------------------------ reversion

        /// <summary>Open one filled slot in an unwarded, unfinished bundle. True when a slot came
        /// undone. Also the debug entry point (<c>tly_sabotage revert</c>).</summary>
        public bool Revert(Random rng)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            DonatedSlot pick = ReversionRule.Pick(ledger, _requirements(), rng);
            if (pick == null)
            {
                _monitor.Log("Darkness: reversion rolled but every filled slot is in a finished bundle.", LogLevel.Trace);
                return false;
            }
            if (!TheLongestYear.Integration.CcSlotWriter.TryUnfill(pick.BundleIndex, pick.IngredientIndex))
            {
                _monitor.Log($"Darkness: reversion could not open slot {pick.BundleIndex}/{pick.IngredientIndex} on the board.", LogLevel.Warn);
                return false;
            }
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            string bundleName = _requirements().FirstOrDefault(r => r.BundleIndex == pick.BundleIndex)?.Name ?? "";
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Reversion, Count = 1, BundleName = bundleName, ItemId = pick.ItemId,
            });
            _monitor.Log(
                $"Darkness: {Strings.ItemName(pick.ItemId)} came undone from {bundleName} (slot {pick.BundleIndex}/{pick.IngredientIndex}) on {Run.Season} {Run.DayOfMonth}.",
                LogLevel.Info);
            return true;
        }

        // ------------------------------------------------------------------ tampering

        /// <summary>Rewrite one unfilled slot to a different Winter-obtainable item. True when the
        /// board changed. Also the debug entry point (<c>tly_sabotage tamper</c>).</summary>
        public bool Tamper(Random rng, int dayOfYear)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return false;
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            List<TamperTarget> targets = TamperRule.Targets(ledger, requirements).ToList();
            if (targets.Count == 0)
            {
                _monitor.Log("Darkness: tampering rolled but no unfilled slot is left in an unfinished bundle.", LogLevel.Trace);
                return false;
            }

            ItemAvailabilityModel availability = _availability();
            int currentWeek = Run.WeekOfYear;
            IReadOnlyList<TamperCandidate> candidates = WinterCandidates(availability, currentWeek);
            HashSet<string> held = HeldItemIds();

            // The held-first ordering is the rule's; when the chosen slot has no fit (a Pantry slot
            // in a Winter with no Winter farm item, say) fall through the rest in the same order.
            var ordered = new List<TamperTarget>();
            while (targets.Count > 0)
            {
                TamperTarget next = TamperRule.PickTarget(targets, held.Contains, rng);
                if (next == null) break;
                ordered.Add(next);
                targets.Remove(next);
            }

            foreach (TamperTarget target in ordered)
            {
                int effort = availability.For(target.ItemId).Effort;
                TamperCandidate replacement = TamperRule.PickReplacement(target, effort, candidates, rng);
                if (replacement == null) continue;
                if (WriteTamper(worldState, target, replacement.ItemId, dayOfYear)) return true;
            }
            _monitor.Log("Darkness: tampering rolled but no Winter item fits any open slot.", LogLevel.Info);
            return false;
        }

        /// <summary>Every pool item the availability model places in Winter (week 13 to now), with
        /// the room theme its pool feeds.</summary>
        private IReadOnlyList<TamperCandidate> WinterCandidates(ItemAvailabilityModel availability, int currentWeek)
        {
            ItemPools pools = _pools();
            int firstWinterWeek = AvailabilityWeeks.FirstWeekOf(CoreSeason.Winter);
            var result = new List<TamperCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(IEnumerable<PoolItem> pool, Theme theme)
            {
                foreach (PoolItem item in pool)
                {
                    string id = BundleParsing.NormalizeItemId(item.ItemId);
                    if (!seen.Add(id)) continue;
                    if (!availability.IsPlaced(id)) continue;
                    ItemAvailability a = availability.For(id);
                    if (a.Week < firstWinterWeek || a.Week > currentWeek) continue;
                    result.Add(new TamperCandidate(id, theme, a.Effort));
                }
            }
            Add(pools.Crops, Theme.Farming);
            Add(pools.ArtisanGoods, Theme.Farming);
            Add(pools.Forage, Theme.Foraging);
            Add(pools.TapperGoods, Theme.Foraging);
            Add(pools.Fish, Theme.Fishing);
            Add(pools.CrabPot, Theme.Fishing);
            Add(pools.Metals, Theme.Mining);
            Add(pools.MonsterDrops, Theme.Mining);
            Add(pools.GeodeMinerals, Theme.Mining);
            return result;
        }

        /// <summary>Ids the player is holding right now: inventory plus every chest on every map.</summary>
        private static HashSet<string> HeldItemIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Farmer player = Game1.player;
            if (player != null)
                foreach (Item item in player.Items)
                    if (item != null) ids.Add(item.QualifiedItemId);
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                    if (obj is Chest chest)
                        foreach (Item item in chest.Items)
                            if (item != null) ids.Add(item.QualifiedItemId);
                return true;
            });
            return ids;
        }

        private bool WriteTamper(StardewValley.Network.NetWorldState worldState, TamperTarget target, string newItemId, int dayOfYear)
        {
            Dictionary<string, string> live = worldState.BundleData;
            string key = BundleDataTamper.KeyForIndex(live, target.Bundle.BundleIndex);
            if (key == null) return false;
            Dictionary<string, string> tampered = BundleDataTamper.Apply(
                live, key, target.IngredientIndex, newItemId, SabotageTuning.TamperStack, SabotageTuning.TamperQuality);
            if (tampered == null) return false;

            // The board, then the stored copy in lockstep: the next load's manifest check compares
            // the two, and a mismatch demotes the save to the legacy read path (ModEntry.ResolveRequirements).
            worldState.SetBundleData(tampered);
            if (Meta.WrittenBoard != null && Meta.WrittenBoard.Count > 0 && Meta.WrittenBoard.ContainsKey(key))
                Meta.WrittenBoard[key] = tampered[key];

            // A goal card pointing at the old item would show a slot the board no longer has.
            Run.CurrentWeekBonusSlots?.RemoveAll(s =>
                s.BundleIndex == target.Bundle.BundleIndex && s.IngredientIndex == target.IngredientIndex);

            Run.Tampers.Add(new TamperRecord
            {
                BundleIndex = target.Bundle.BundleIndex,
                IngredientIndex = target.IngredientIndex,
                BundleName = target.Bundle.Name,
                OldItemId = target.ItemId,
                NewItemId = newItemId,
                DayOfYear = dayOfYear,
            });
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Tampering, Count = 1, BundleName = target.Bundle.Name,
                ItemId = newItemId, OldItemId = target.ItemId,
            });
            _monitor.Log(
                $"Darkness: {target.Bundle.Name} slot {target.IngredientIndex} now asks for {Strings.ItemName(newItemId)} instead of {Strings.ItemName(target.ItemId)} ({Run.Season} {Run.DayOfMonth}).",
                LogLevel.Info);
            _rebuildBoard("darkness tampering");
            return true;
        }

        // ------------------------------------------------------------------ the morning

        /// <summary>The morning: HUD lines and letters for what the night took, and when the board
        /// changed, the Junimos' porch scene first. Returns true when the scene was started and
        /// <paramref name="continueWith"/> will run after it; false when the caller continues now.</summary>
        public bool ShowMorning(Action continueWith)
        {
            if (!RunActivation.IsActive) return false;
            SabotageReport tamper = Run.PendingSabotageReports?.Find(r => r.Kind == SabotageKind.Tampering);
            if (tamper == null || StartTamperScene == null)
            {
                ShowMorningReports();
                return false;
            }
            string oldName = Strings.ItemName(tamper.OldItemId);
            string newName = Strings.ItemName(tamper.ItemId);
            Run.PendingSabotageReports.RemoveAll(r => r.Kind == SabotageKind.Tampering);
            StartTamperScene(oldName, newName, () => { ShowMorningReports(); continueWith?.Invoke(); });
            return true;
        }

        /// <summary>Show what the night took, as HUD lines, then forget them.</summary>
        public void ShowMorningReports()
        {
            if (!RunActivation.IsActive) return;
            List<SabotageReport> reports = Run.PendingSabotageReports;
            if (reports == null || reports.Count == 0) return;
            // The hall fronts share one line and say it once, however many struck (Jeff, 2026-09-09:
            // the player wakes with a feeling, the board tells the rest).
            bool hallSaid = false;
            foreach (SabotageReport report in reports)
            {
                _mail?.SendFirstStrikeLetter(report.Kind);
                switch (report.Kind)
                {
                    case SabotageKind.Blight:
                        // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
                        if (report.Count > 0)
                            Hud(Strings.Get("hud.sabotage.blight", new Dictionary<string, string> { ["count"] = report.Count.ToString() }));
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
            reports.Clear();
            Game1.playSound("shadowDie");
        }

        private static void Hud(string text) => Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));

        /// <summary>One-screen status for tly_sabotage.</summary>
        public string Status()
        {
            var lines = new List<string>
            {
                $"Darkness pushback: blight={(Enabled(SabotageKind.Blight) ? "on" : "off")}, reversion={(Enabled(SabotageKind.Reversion) ? "on" : "off")}, tampering={(Enabled(SabotageKind.Tampering) ? "on" : "off")}",
                $"  season {Run.Season} day {Run.DayOfMonth}: open fronts = {string.Join(", ", Enum.GetValues(typeof(SabotageKind)).Cast<SabotageKind>().Where(k => SabotageSchedule.IsOpen(k, Run.Season)))}",
                $"  wards owned: {string.Join(", ", WardIds.All.Where(Meta.HasUpgrade).DefaultIfEmpty("none"))}",
                $"  blight week {Run.BlightWeek} nights {Run.BlightNightsThisWeek}; last reversion week {Run.LastReversionWeek}; tamper days [{string.Join(",", Run.TamperDays)}]",
                $"  live crops on the farm: {BlightPass.LiveCropTiles().Count}; units in chests (stash excluded): {SpoilagePass.StoredUnits()}",
            };
            foreach (TamperRecord t in Run.Tampers)
                lines.Add($"  tampered: {t.BundleName} slot {t.IngredientIndex}: {Strings.ItemName(t.OldItemId)} -> {Strings.ItemName(t.NewItemId)} (day {t.DayOfYear})");
            if (Run.PendingSabotageReports.Count > 0)
                lines.Add($"  pending morning reports: {Run.PendingSabotageReports.Count}");
            return string.Join("\n", lines);
        }
    }
}
