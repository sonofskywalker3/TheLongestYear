using System.Collections.Generic;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>What a focus card shows, shared by the pick menu, the statue's Focus tab and the
    /// hub strip: the icon, name and description of one rung of the card's chain, and the
    /// overnight note for a house card.</summary>
    internal sealed class FocusCardFace
    {
        private const string HouseChain = "house";

        public string ChainId { get; private init; }
        public Item Icon { get; private init; }
        public string Name { get; private init; }
        public string Description { get; private init; }

        /// <summary>"Ready the next morning." on a house card that still delivers; empty otherwise.</summary>
        public string Note { get; private init; }

        /// <summary>The face for a card. The rung is the quote's target, else the chain's top rung.
        /// <paramref name="boughtUpgradeId"/>, when set, pins the face to the rung that was bought,
        /// because the live quote has moved on once the world has it.</summary>
        public static FocusCardFace For(string chainId, FocusQuote quote, string boughtUpgradeId = null)
        {
            FocusChain chain = FocusChains.TryGet(chainId);
            int rung = quote?.TargetRung ?? chain?.Rungs.Count ?? 1;
            string upgradeId = quote?.UpgradeId
                ?? (chain != null && rung >= 1 && rung <= chain.Rungs.Count ? chain.Rungs[rung - 1] : null);
            if (boughtUpgradeId != null && chain != null)
            {
                int boughtIndex = IndexOf(chain.Rungs, boughtUpgradeId);
                if (boughtIndex >= 0)
                {
                    rung = boughtIndex + 1;
                    upgradeId = boughtUpgradeId;
                }
            }
            UpgradeDefinition def = upgradeId != null ? UpgradeCatalog.TryGet(upgradeId) : null;
            bool delivers = boughtUpgradeId == null && quote?.Kind == FocusQuoteKind.Offer;
            return new FocusCardFace
            {
                ChainId = chainId,
                Icon = FocusIcons.For(chainId, rung),
                Name = def?.DisplayName ?? chainId,
                Description = def?.Description ?? "",
                // Only a card that delivers a house upgrade waits for the morning; a keep-only one delivers nothing.
                Note = chainId == HouseChain && delivers ? Strings.Get("focus.card.overnight") : "",
            };
        }

        private static int IndexOf(IReadOnlyList<string> rungs, string id)
        {
            for (int i = 0; i < rungs.Count; i++)
                if (rungs[i] == id) return i;
            return -1;
        }
    }
}
