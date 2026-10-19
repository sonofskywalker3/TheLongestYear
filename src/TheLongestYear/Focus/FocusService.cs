using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Integration;

namespace TheLongestYear.Focus
{
    /// <summary>Mod-side face of the focus cards: deals the hands from the run seed when due,
    /// quotes each card against the live world and buys the chosen card. State is read through
    /// the store on every call, never cached, because a reload replaces the run.</summary>
    internal sealed class FocusService
    {
        /// <summary>The rung a placement builds: the plain building (Coop, Barn, Silo, Fish Pond).</summary>
        private const int PlacedRung = 1;

        private static readonly FocusTheme[] ThemeOrder = { FocusTheme.Tools, FocusTheme.Buildings, FocusTheme.Knacks };

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly UpgradePurchaseService _purchases;

        public FocusService(IMonitor monitor, MetaStore store, GameplayConfig config, UpgradePurchaseService purchases)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _purchases = purchases;
            Delivery = new FocusDelivery(monitor, store);
        }

        /// <summary>Read per call: a reset re-stamps the difficulty mid-session.</summary>
        private double PriceFactor => _store.State.EffectiveDifficulty(_config).ShrinePriceFactor;

        /// <summary>Deals this run's hands if they have not been dealt yet. A card is dealt only
        /// when it is an offer now: a keep-only card (the world already has its rung) would do
        /// nothing until the fail night. A dealt card that later turns keep-only stays in the hand.</summary>
        public void EnsureDealt()
        {
            if (FocusPicks.NeedsDeal(_store.Run)) Deal();
        }

        /// <summary>Debug only: throws away this run's hands and picks and deals again.</summary>
        public void ForceDeal()
        {
            _store.Run.ResetFocus();
            Deal();
        }

        private void Deal()
        {
            RunState run = _store.Run;
            IReadOnlyDictionary<string, int> peaks = ToolTierScan.Peaks(Game1.player);
            List<FocusHand> hands = FocusDeal.Deal(run.Seed, chain =>
                FocusDeal.IsDealable(Quote(chain, peaks)));
            FocusPicks.Install(run, hands);
            _monitor.Log($"Focus cards dealt for run {run.RunNumber}.", LogLevel.Trace);
        }

        /// <summary>The live quote for a chain, or null for an unknown chain id.</summary>
        public FocusQuote QuoteFor(string chainId)
        {
            FocusChain chain = FocusChains.TryGet(chainId);
            if (chain == null) return null;
            IReadOnlyDictionary<string, int> peaks = chain.Theme == FocusTheme.Tools ? ToolTierScan.Peaks(Game1.player) : null;
            return Quote(chain, peaks);
        }

        private FocusQuote Quote(FocusChain chain, IReadOnlyDictionary<string, int> toolPeaks)
            => FocusMorph.Quote(chain, _store.State, FocusWorldReader.Rung(chain.Id, toolPeaks), PriceFactor);

        /// <summary>Every picked card with its live quote, in theme order; unpicked hands are skipped.</summary>
        public IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> ChosenCards()
        {
            var cards = new List<(FocusHand, FocusQuote)>();
            foreach (FocusTheme theme in ThemeOrder)
            {
                FocusHand hand = _store.Run.FocusHandFor(theme);
                if (hand?.Chosen == null) continue;
                FocusQuote quote = QuoteFor(hand.Chosen);
                if (quote != null) cards.Add((hand, quote));
            }
            return cards;
        }

        public bool CanAfford(FocusTheme theme)
        {
            FocusHand hand = _store.Run.FocusHandFor(theme);
            if (hand?.Chosen == null || hand.Bought) return false;
            FocusQuote quote = QuoteFor(hand.Chosen);
            return quote != null
                && quote.Kind is FocusQuoteKind.Offer or FocusQuoteKind.KeepOnly
                && _store.State.JunimoPoints >= quote.Price;
        }

        /// <summary>Hands a bought card over in the world (tools, knacks, house, shortcuts); its
        /// day hooks finish the overnight house upgrade.</summary>
        public FocusDelivery Delivery { get; }

        /// <summary>Buys the chosen card's keep only: JP and the kept rungs, nothing placed in the world.
        /// Host only: a farmhand's call changes nothing.</summary>
        public FocusBuyResult BuyKeepOnly(FocusTheme theme) => BuyKeep(theme, out _);

        /// <summary>Buys the chosen card and delivers it now. Host only: a farmhand's call changes
        /// nothing. A new coop, barn, silo or fish pond opens the placement screen and returns
        /// <see cref="FocusBuyResult.Placing"/>: the card is charged only when the building lands,
        /// and backing out costs nothing. A coop or barn upgrade is charged, then applied at once.</summary>
        public FocusBuyResult Buy(FocusTheme theme)
        {
            if (!Context.IsMainPlayer) return Logged(theme, null, FocusBuyResult.NotHost);

            string chosen = _store.Run.FocusHandFor(theme)?.Chosen;
            if (FocusDelivery.NeedsPlacement(chosen))
                return BuyBuilding(theme);

            FocusBuyResult result = BuyKeep(theme, out FocusQuote quote);
            if (result == FocusBuyResult.Success)
            {
                string message = Delivery.Deliver(quote, theme);
                _monitor.Log($"Focus delivery for '{quote.ChainId}': {message ?? "nothing to hand over (keep only)"}.", LogLevel.Trace);
            }
            return result;
        }

        /// <summary>The building branch of <see cref="Buy"/>. A keep-only card (the world already has
        /// the top rung) is bought as usual; an offer either upgrades the farm's building in place or,
        /// with none on the farm, places one through <see cref="FocusBuildSession"/>.</summary>
        private FocusBuyResult BuyBuilding(FocusTheme theme)
        {
            FocusBuyResult check = Validate(theme, out FocusHand hand, out FocusQuote quote);
            if (check != FocusBuyResult.Success) return Logged(theme, quote, check);
            if (quote.Kind != FocusQuoteKind.Offer) return Charge(theme, hand, quote);

            if (!CanAfford(theme))
            {
                Game1.addHUDMessage(new HUDMessage(Strings.Get("focus.cant-afford",
                    new Dictionary<string, string> { ["jp"] = quote.Price.ToString() })));
                return Logged(theme, quote, FocusBuyResult.NotEnoughJp);
            }

            if (FocusWorldReader.Rung(quote.ChainId) > 0)
            {
                FocusBuyResult upgraded = Charge(theme, hand, quote);
                if (upgraded != FocusBuyResult.Success) return upgraded;
                if (FocusBuildingUpgrade.Apply(quote.ChainId, quote.TargetRung))
                    Delivery.ShowBuilding(FocusWorldRungs.BuildingTypeForRung(quote.ChainId, quote.TargetRung));
                else
                    _monitor.Log($"Focus delivery: no farm building of '{quote.ChainId}' could go to rung {quote.TargetRung}; keep recorded, nothing upgraded.", LogLevel.Warn);
                return upgraded;
            }

            string placeType = FocusWorldRungs.BuildingTypeForRung(quote.ChainId, PlacedRung);
            // The quote is fixed now: once the building lands the world rung rises, and a fresh
            // quote would offer (and charge) the next rung up instead of this card.
            bool opened = FocusBuildSession.Start(placeType,
                onPlaced: () => OnPlaced(theme, quote),
                onCancelled: () => _monitor.Log($"Focus card '{quote.ChainId}': placement backed out; nothing charged, card still unbought.", LogLevel.Info));
            if (!opened)
            {
                Game1.addHUDMessage(new HUDMessage(Strings.Get("focus.build-busy")));
                return Logged(theme, quote, FocusBuyResult.Busy);
            }
            _monitor.Log($"Focus card '{quote.ChainId}': placement screen open for '{placeType}'; charged once it is placed.", LogLevel.Info);
            return FocusBuyResult.Placing;
        }

        /// <summary>The building landed: charge the card at the quote it was opened with, then raise
        /// it further when the kept rungs put the card above a plain new building.</summary>
        private void OnPlaced(FocusTheme theme, FocusQuote quote)
        {
            FocusHand hand = _store.Run.FocusHandFor(theme);
            FocusBuyResult result = hand == null ? FocusBuyResult.NotChosen : Charge(theme, hand, quote);
            if (result != FocusBuyResult.Success)
            {
                _monitor.Log($"Focus card '{quote.ChainId}': the building was placed but the charge failed ({result}).", LogLevel.Warn);
                return;
            }
            if (quote.TargetRung > PlacedRung && !FocusBuildingUpgrade.Apply(quote.ChainId, quote.TargetRung))
                _monitor.Log($"Focus delivery: the new '{quote.ChainId}' building could not go to rung {quote.TargetRung}.", LogLevel.Warn);
            Delivery.ShowBuilding(FocusWorldRungs.BuildingTypeForRung(quote.ChainId, quote.TargetRung));
        }

        private FocusBuyResult BuyKeep(FocusTheme theme, out FocusQuote quote)
        {
            FocusBuyResult check = Validate(theme, out FocusHand hand, out quote);
            if (check != FocusBuyResult.Success) return Logged(theme, quote, check);
            return Charge(theme, hand, quote);
        }

        /// <summary>The checks before any purchase: host, a chosen and unbought card, a known chain.</summary>
        private FocusBuyResult Validate(FocusTheme theme, out FocusHand hand, out FocusQuote quote)
        {
            quote = null;
            hand = null;
            if (!Context.IsMainPlayer) return FocusBuyResult.NotHost;
            hand = _store.Run.FocusHandFor(theme);
            if (hand == null) return FocusBuyResult.NotChosen;
            if (hand.Bought) return FocusBuyResult.AlreadyBought;
            if (hand.Chosen == null) return FocusBuyResult.NotChosen;
            quote = QuoteFor(hand.Chosen);
            return quote == null ? FocusBuyResult.NotChosen : FocusBuyResult.Success;
        }

        private FocusBuyResult Charge(FocusTheme theme, FocusHand hand, FocusQuote quote)
        {
            FocusBuyResult result = FocusPurchase.TryBuy(_store.State, hand, quote);
            if (result == FocusBuyResult.Success)
            {
                Game1.playSound("purchase");
                foreach (string id in quote.Grants)
                    _purchases?.NotifyPurchased(id);
            }
            return Logged(theme, quote, result);
        }

        private FocusBuyResult Logged(FocusTheme theme, FocusQuote quote, FocusBuyResult result)
        {
            if (result == FocusBuyResult.Success)
                _monitor.Log(
                    $"Bought the {theme} focus card '{quote.ChainId}' ({quote.UpgradeId}) for {quote.Price} JP, " +
                    $"granting {string.Join(", ", quote.Grants)}. JP remaining: {_store.State.JunimoPoints}.",
                    LogLevel.Info);
            else
                _monitor.Log(
                    $"Cannot buy the {theme} focus card{(quote == null ? "" : $" '{quote.ChainId}' ({quote.Price} JP, have {_store.State.JunimoPoints})")}: {result}.",
                    LogLevel.Info);
            return result;
        }
    }
}
