namespace TheLongestYear.Core;

/// <summary>How a focus card purchase went. <c>Placing</c>: the building placement screen is open
/// and the card is charged only once a building lands. <c>Busy</c>: the placement screen could not
/// open (a scene, or a menu that will not close); nothing was charged.</summary>
public enum FocusBuyResult { Success, NotChosen, AlreadyBought, Done, Locked, NotEnoughJp, NotHost, Placing, Busy }

/// <summary>The purchase rule for a focus card. It deliberately ignores a rung's prerequisite and
/// run-reach gate: the quote already grants every lower rung, and a card is a way to get the
/// rung without reaching it. Nothing is mutated unless the result is Success.</summary>
public static class FocusPurchase
{
    public static FocusBuyResult TryBuy(MetaState meta, FocusHand hand, FocusQuote quote)
    {
        if (hand.Bought) return FocusBuyResult.AlreadyBought;
        if (hand.Chosen == null || hand.Chosen != quote.ChainId) return FocusBuyResult.NotChosen;
        if (quote.Kind == FocusQuoteKind.Done) return FocusBuyResult.Done;
        if (quote.Kind == FocusQuoteKind.Locked) return FocusBuyResult.Locked;
        if (meta.JunimoPoints < quote.Price) return FocusBuyResult.NotEnoughJp;

        meta.JunimoPoints -= quote.Price;
        foreach (string id in quote.Grants)
            if (!meta.HasUpgrade(id)) meta.OwnedUpgrades.Add(id);
        hand.Bought = true;
        hand.BoughtUpgradeId = quote.UpgradeId;
        return FocusBuyResult.Success;
    }
}
