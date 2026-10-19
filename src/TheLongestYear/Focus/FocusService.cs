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
        }

        /// <summary>Read per call: a reset re-stamps the difficulty mid-session.</summary>
        private double PriceFactor => _store.State.EffectiveDifficulty(_config).ShrinePriceFactor;

        /// <summary>Deals this run's hands if they have not been dealt yet. A card is dealt only
        /// when it can be bought now (an offer or a keep-only).</summary>
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
                Quote(chain, peaks).Kind is FocusQuoteKind.Offer or FocusQuoteKind.KeepOnly);
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

        /// <summary>Buys the chosen card's keep only: JP and the kept rungs, nothing placed in the world.</summary>
        public FocusBuyResult BuyKeepOnly(FocusTheme theme)
        {
            FocusHand hand = _store.Run.FocusHandFor(theme);
            if (hand == null) return Logged(theme, null, FocusBuyResult.NotChosen);
            if (hand.Bought) return Logged(theme, null, FocusBuyResult.AlreadyBought);
            if (hand.Chosen == null) return Logged(theme, null, FocusBuyResult.NotChosen);

            FocusQuote quote = QuoteFor(hand.Chosen);
            if (quote == null) return Logged(theme, null, FocusBuyResult.NotChosen);
            FocusBuyResult result = FocusPurchase.TryBuy(_store.State, hand, quote);
            if (result == FocusBuyResult.Success)
            {
                Game1.playSound("purchase");
                foreach (string id in quote.Grants)
                    _purchases?.NotifyPurchased(id);
            }
            return Logged(theme, quote, result);
        }

        /// <summary>Buys the chosen card. The keep part only for now; delivering the tool or
        /// building into the world is added on top of this.</summary>
        public FocusBuyResult Buy(FocusTheme theme) => BuyKeepOnly(theme);

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
