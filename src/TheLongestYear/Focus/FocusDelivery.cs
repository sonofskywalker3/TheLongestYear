using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
using TheLongestYear.Core;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.Focus
{
    /// <summary>Puts a bought focus card into the world now: the tool is upgraded where it sits,
    /// the knack takes effect, the shortcuts open and the house upgrade lands overnight. The keep
    /// itself is already recorded by <see cref="FocusPurchase"/>; this is only the "get it now"
    /// half. Coop, barn, silo and fish pond cards are built by <see cref="FocusService"/> through
    /// <see cref="FocusBuildSession"/> and <see cref="FocusBuildingUpgrade"/>, not here.
    ///
    /// Knack readers audited for the mid-loop purchase (all read MetaState per call unless noted):
    /// UpgradeChecker.GetTier / HasUpgrade (green thumb, coal vein, forager's eye, quick bite,
    /// shop discount, cart slot), HighestKeptTier (weather sage, stash), JpBoostHelper,
    /// MixedSeedsPatch, JunimoStashCapPatch, and the shop discount on ShopBuilder.GetShopStock.
    /// Two needed a refresh: the backpack (Farmer.MaxItems is world state, grown here) and the
    /// Traveling Cart's remembered stock for the day (CartDayStock opens the new slots on the
    /// next stock build).</summary>
    internal sealed class FocusDelivery
    {
        private const string HouseChain = "house";
        private const string ShortcutsChain = "shortcuts";
        private const string BackpackChain = "backpack";
        private const string ShortcutsMail = "communityUpgradeShortcuts";
        private const int KitchenRung = 1;
        private const int KitchenLevel = 1;
        private const int BasementLevel = 3;
        private const int HouseUpgradeTonight = 1;

        /// <summary>Building chains that stand on the farm: a new one goes through the placement
        /// screen, an existing one is upgraded in place (see <see cref="FocusService.Buy"/>).</summary>
        private static readonly IReadOnlySet<string> PlacedBuildingChains =
            new HashSet<string>(StringComparer.Ordinal) { "coop", "barn", "silo", "fish_pond" };

        /// <summary>Every map the shortcuts mail changes. Vanilla re-reads the mail in each one's
        /// MakeMapModifications (Town, Forest, Mountain, Beach overrides; Backwoods in GameLocation).</summary>
        private static readonly string[] ShortcutLocations = { "Town", "Forest", "Mountain", "Beach", "Backwoods" };

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public FocusDelivery(IMonitor monitor, MetaStore store)
        {
            _monitor = monitor;
            _store = store;
        }

        public static bool NeedsPlacement(string chainId)
            => chainId != null && PlacedBuildingChains.Contains(chainId);

        /// <summary>Delivers a bought card and shows its HUD line. Only an Offer delivers; a
        /// keep-only card (the world already has the top rung) delivers nothing. Returns the
        /// i18n key of the message shown, or null when nothing was delivered.</summary>
        public string Deliver(FocusQuote quote, FocusTheme theme)
        {
            if (quote == null || quote.Kind != FocusQuoteKind.Offer) return null;
            if (!Context.IsMainPlayer || Game1.player == null) return null;
            return theme switch
            {
                FocusTheme.Tools => DeliverTool(quote),
                FocusTheme.Knacks => DeliverKnack(quote),
                FocusTheme.Buildings when quote.ChainId == HouseChain => DeliverHouse(quote),
                FocusTheme.Buildings when quote.ChainId == ShortcutsChain => DeliverShortcuts(),
                _ => null,
            };
        }

        private string DeliverTool(FocusQuote quote)
        {
            Farmer p = Game1.player;
            string slug = quote.ChainId;
            string itemId = ToolTierItems.ItemIdFor(slug, ToolTierItems.LevelForRung(slug, quote.TargetRung));
            if (itemId == null || ItemRegistry.Create(itemId, allowNull: true) is not Tool fresh)
            {
                _monitor.Log($"Focus delivery: no tool item for '{slug}' rung {quote.TargetRung}; keep recorded, nothing handed over.", LogLevel.Warn);
                return null;
            }

            ToolSlot slot = ToolTierScan.FindBest(p, slug);
            var moved = new List<string>();
            string where;
            if (slot == null)
            {
                p.addItemByMenuIfNecessary(fresh);
                where = "granted fresh";
                // Without this Willy's beach scene still plays and hands over a second Bamboo Pole.
                if (slug == ToolTierScan.RodSlug)
                    p.eventsSeen.Add(ToolTierItems.WillyRodEventId);
            }
            else
            {
                ToolTierItems.CopyState(slot.Tool, fresh, slug, moved);
                if (slot.Source == ToolTierScan.ClintSource)
                {
                    p.toolBeingUpgraded.Value = null;
                    p.daysLeftForToolUpgrade.Value = 0;
                    p.addItemByMenuIfNecessary(fresh);
                    where = "fetched from Clint";
                }
                else
                {
                    slot.Replace(fresh);
                    where = $"replaced in {slot.Source}";
                }
            }

            _monitor.Log(
                $"Focus delivery: {slug} -> {fresh.QualifiedItemId} ({where})" +
                (moved.Count > 0 ? $", carried over {string.Join(", ", moved)}." : "."),
                LogLevel.Info);
            return slot == null
                ? Show("focus.delivered.tool-new", Strings.Get("focus.delivered.tool-new",
                    new Dictionary<string, string> { ["item"] = fresh.DisplayName }))
                : Show("focus.delivered.tool", Strings.Get("focus.delivered.tool",
                    new Dictionary<string, string> { ["item"] = fresh.DisplayName }));
        }

        private string DeliverKnack(FocusQuote quote)
        {
            if (quote.ChainId == BackpackChain)
            {
                Farmer p = Game1.player;
                int size = FocusWorldRungs.BackpackSizeForRung(quote.TargetRung);
                if (p.MaxItems < size)
                    p.increaseBackpackSize(size - p.MaxItems);
                _monitor.Log($"Focus delivery: backpack is now {p.MaxItems} slots.", LogLevel.Info);
            }
            else
            {
                _monitor.Log($"Focus delivery: {quote.UpgradeId} is live from now.", LogLevel.Info);
            }

            string name = UpgradeCatalog.TryGet(quote.UpgradeId)?.DisplayName ?? quote.UpgradeId;
            return Show("focus.delivered.knack", Strings.Get("focus.delivered.knack",
                new Dictionary<string, string> { ["upgrade"] = name }));
        }

        private string DeliverShortcuts()
        {
            Game1.MasterPlayer.mailReceived.Add(ShortcutsMail);
            var refreshed = new List<string>();
            foreach (string name in ShortcutLocations)
            {
                GameLocation loc = Game1.getLocationFromName(name);
                if (loc == null) continue;
                loc.MakeMapModifications(force: true);
                refreshed.Add(name);
            }
            _monitor.Log($"Focus delivery: shortcuts opened, maps refreshed: {string.Join(", ", refreshed)}.", LogLevel.Info);
            return Show("focus.delivered.shortcuts", Strings.Get("focus.delivered.shortcuts"));
        }

        private string DeliverHouse(FocusQuote quote)
        {
            int target = quote.TargetRung == KitchenRung ? KitchenLevel : BasementLevel;
            _store.Run.FocusHouseTarget = target;
            int robinDays = Game1.player.daysUntilHouseUpgrade.Value;
            if (robinDays > HouseUpgradeTonight)
                _monitor.Log($"Focus delivery: Robin's house upgrade had {robinDays} days left; the card finishes it tonight instead.", LogLevel.Trace);
            Game1.player.daysUntilHouseUpgrade.Value = HouseUpgradeTonight;
            _monitor.Log($"Focus delivery: house goes to level {target} overnight (now {Game1.player.HouseUpgradeLevel}).", LogLevel.Info);
            return Show("focus.delivered.house-overnight", Strings.Get("focus.delivered.house-overnight"));
        }

        /// <summary>DayEnding: vanilla's Farmer.dayupdate raises the house one level when its
        /// countdown runs out. A basement bought from below level 2 needs more than one level, so
        /// the levels below the last are stepped here first, each with vanilla's own furniture
        /// shift, and dayupdate then does the last one and loads the map.</summary>
        public void OnDayEnding()
        {
            if (!Context.IsMainPlayer) return;
            int target = _store.Run.FocusHouseTarget;
            Farmer p = Game1.player;
            if (target < 0 || p == null) return;
            if (p.daysUntilHouseUpgrade.Value != HouseUpgradeTonight || p.HouseUpgradeLevel >= target) return;

            FarmHouse home = Utility.getHomeOfFarmer(p);
            int from = p.HouseUpgradeLevel;
            while (home != null && p.HouseUpgradeLevel < target - 1)
            {
                home.moveObjectsForHouseUpgrade(p.HouseUpgradeLevel + 1);
                p.HouseUpgradeLevel++;
            }
            if (p.HouseUpgradeLevel != from)
                _monitor.Log($"Focus delivery: house stepped {from} -> {p.HouseUpgradeLevel}; the night takes it to {target}.", LogLevel.Trace);
        }

        /// <summary>DayStarted: once the house reached the target, give a basement its Cellar and
        /// clear the target. If the upgrade did not happen (the countdown was lost), arm it again.</summary>
        public void OnDayStarted()
        {
            if (!Context.IsMainPlayer) return;
            int target = _store.Run.FocusHouseTarget;
            Farmer p = Game1.player;
            if (target < 0 || p == null) return;

            if (p.HouseUpgradeLevel >= target)
            {
                if (target == BasementLevel && CellarHelper.EnsureCellar())
                    _monitor.Log("Focus delivery: created the Cellar for the new basement.", LogLevel.Info);
                _store.Run.FocusHouseTarget = RunState.NoFocusHouseTarget;
                _monitor.Log($"Focus delivery: house is at level {p.HouseUpgradeLevel}.", LogLevel.Info);
                return;
            }

            if (p.daysUntilHouseUpgrade.Value <= 0)
            {
                p.daysUntilHouseUpgrade.Value = HouseUpgradeTonight;
                _monitor.Log($"Focus delivery: house still at level {p.HouseUpgradeLevel}, below {target}; upgrade armed for tonight again.", LogLevel.Warn);
            }
        }

        /// <summary>The HUD line for a focus building that now stands finished on the farm.</summary>
        public string ShowBuilding(string buildingType)
        {
            string name = buildingType != null && Game1.buildingData.TryGetValue(buildingType, out var data)
                ? TokenParser.ParseText(data.Name) ?? buildingType
                : buildingType;
            return Show("focus.delivered.building", Strings.Get("focus.delivered.building",
                new Dictionary<string, string> { ["building"] = name }));
        }

        private static string Show(string key, string text)
        {
            Game1.addHUDMessage(new HUDMessage(text));
            return key;
        }
    }
}
