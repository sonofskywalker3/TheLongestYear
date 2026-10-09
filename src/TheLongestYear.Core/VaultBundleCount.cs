using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>How the bundle-count dial reshapes the Vault on a TLY Custom board (Jeff, 2026-10-09:
/// "for easy drop the highest cost bundle, for hard add 1 more that's double the current highest
/// cost, and for extreme add another beyond that with another like x1.5-1.75 of the hard bundle").
///
/// Works on the board's own Vault prices, after <see cref="VaultAmountScaler"/>, so the ratios hold
/// whatever the Vault multiplier is: on the default +25% board (3,125 / 6,250 / 12,500 / 31,250g)
/// Hard adds 62,500g, and Extreme adds 62,500g and 100,000g. The change comes from
/// <see cref="BundleCountRule.VaultDelta"/>: -1 Easy, 0 Normal, +1 Hard, +2 Extreme.
/// Spec 2026-10-09-bundle-count-dial, amendment "The Vault" (2026-10-09).</summary>
public static class VaultBundleCount
{
    /// <summary>The first extra bundle costs this many times the board's priciest Vault bundle.</summary>
    public const double FirstExtraFactor = 2.0;

    /// <summary>Each further extra costs this many times the extra before it (Jeff: 1.5 to 1.75).</summary>
    public const double NextExtraFactor = 1.6;

    /// <summary>Extra prices are rounded to the nearest multiple of this, so they read as clean numbers.</summary>
    public const int PriceRounding = 500;

    /// <summary>Easy never empties the Vault: the bus repair needs at least one bundle to pay.</summary>
    public const int MinimumBundles = 1;

    private const string MoneySlotId = "-1";

    /// <summary>Bag tints for the extra bundles. Vanilla's four Vault bags use 4, 2, 3 and 1.</summary>
    private static readonly int[] ExtraColors = { 5, 6 };

    /// <summary>The gold a money bundle asks for (its money slot's stack), 0 when it has none.</summary>
    public static int Gold(BundleSpec spec)
        => spec?.Slots.FirstOrDefault(s => s.ItemId == MoneySlotId)?.Stack ?? 0;

    /// <summary>The Vault bundles a board keeps: all of them unless <paramref name="delta"/> is
    /// negative, then without the most expensive ones (never below <see cref="MinimumBundles"/>).
    /// Order is kept.</summary>
    public static IReadOnlyList<BundleSpec> Kept(IReadOnlyList<BundleSpec> vault, int delta)
    {
        if (vault == null) throw new ArgumentNullException(nameof(vault));
        if (delta >= 0)
            return vault;
        int dropCount = Math.Min(-delta, Math.Max(0, vault.Count - MinimumBundles));
        var dropped = new HashSet<int>(
            Enumerable.Range(0, vault.Count)
                .OrderByDescending(i => Gold(vault[i]))
                .ThenByDescending(i => vault[i].Index)
                .Take(dropCount));
        return vault.Where((_, i) => !dropped.Contains(i)).ToList();
    }

    /// <summary>The prices of the extra bundles for a positive <paramref name="delta"/>: the first
    /// is <see cref="FirstExtraFactor"/> times <paramref name="highest"/>, each next one
    /// <see cref="NextExtraFactor"/> times the one before, each rounded to <see cref="PriceRounding"/>.</summary>
    public static IReadOnlyList<int> ExtraPrices(int highest, int delta)
    {
        var prices = new List<int>();
        double price = highest;
        for (int n = 0; n < delta; n++)
        {
            price = Round(price * (n == 0 ? FirstExtraFactor : NextExtraFactor));
            prices.Add((int)price);
        }
        return prices;
    }

    /// <summary>The extra Vault bundles for a positive <paramref name="delta"/>, modelled on the
    /// board's priciest Vault bundle (same reward and layout), each with its own price, name, tint
    /// and an index from <paramref name="nextIndex"/> (the dial's reserved range). Empty when
    /// <paramref name="delta"/> is not positive or the Vault has no money bundle.</summary>
    public static IReadOnlyList<BundleSpec> Extras(IReadOnlyList<BundleSpec> vault, int delta, Func<int> nextIndex)
    {
        if (vault == null) throw new ArgumentNullException(nameof(vault));
        if (delta <= 0)
            return Array.Empty<BundleSpec>();
        if (nextIndex == null) throw new ArgumentNullException(nameof(nextIndex));
        BundleSpec? priciest = vault
            .Where(s => Gold(s) > 0)
            .OrderByDescending(Gold)
            .ThenByDescending(s => s.Index)
            .FirstOrDefault();
        if (priciest == null)
            return Array.Empty<BundleSpec>();

        var extras = new List<BundleSpec>();
        IReadOnlyList<int> prices = ExtraPrices(Gold(priciest), delta);
        for (int n = 0; n < prices.Count; n++)
        {
            int price = prices[n];
            string name = price.ToString("N0", CultureInfo.InvariantCulture) + "g";
            extras.Add(priciest with
            {
                Index = nextIndex(),
                Name = name,
                DisplayName = name,
                Color = ExtraColors[n % ExtraColors.Length],
                Slots = new[] { new BundleSlotSpec(MoneySlotId, price, price) },
            });
        }
        return extras;
    }

    private static double Round(double price)
        => Math.Round(price / PriceRounding, MidpointRounding.AwayFromZero) * PriceRounding;
}
