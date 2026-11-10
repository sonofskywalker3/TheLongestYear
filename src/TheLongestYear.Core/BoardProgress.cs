using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One bundle of the outgoing board with its progress, read from the live per-bundle
/// completion arrays before the reset (spec 2026-11-10 shrine planning, amendment A).</summary>
public sealed class BundleProgress
{
    public int Index { get; }
    public string Room { get; }
    public IReadOnlyList<BundleIngredient> Ingredients { get; }

    /// <summary>One flag per ingredient: donated (or, for a Vault bundle, paid).</summary>
    public IReadOnlyList<bool> Filled { get; }

    /// <summary>How many ingredients finish the bundle (X of a pick-X-of-Y bundle).</summary>
    public int Required { get; }

    public BundleProgress(int index, string room, IReadOnlyList<BundleIngredient> ingredients, IReadOnlyList<bool> filled, int required)
    {
        Index = index;
        Room = room ?? "";
        Ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
        Filled = filled ?? throw new ArgumentNullException(nameof(filled));
        Required = Math.Clamp(required, 0, ingredients.Count);
    }

    public int FilledCount => Filled.Count(f => f);

    public bool Done => FilledCount >= Required;

    /// <summary>Donations still needed to finish it: X minus what is in, 0 once finished. A
    /// money slot is one payment, never its gold.</summary>
    public int StillNeeded => Done ? 0 : Required - FilledCount;
}

/// <summary>Reads the board the loop ends on into <see cref="BundleProgress"/> rows.</summary>
public static class BoardProgress
{
    /// <summary>The Abandoned Joja Mart's bundle is outside the year's goal.</summary>
    public const string AbandonedJojaRoom = "Abandoned Joja Mart";

    public const string VaultRoom = "Vault";

    /// <param name="bundleData">Data/Bundles as the save holds it ("Room/index" to the value).</param>
    /// <param name="slotsOf">The live completion array for a bundle index, or null.</param>
    /// <param name="vaultPaid">Vault bundle indices paid this loop (<see cref="RunState.VaultBundlesPaid"/>).
    /// Vanilla sets only slot 0 of a paid money bundle, so either source marks it paid.</param>
    public static IReadOnlyList<BundleProgress> Read(
        IEnumerable<KeyValuePair<string, string>> bundleData,
        Func<int, IReadOnlyList<bool>?> slotsOf,
        IReadOnlyCollection<int> vaultPaid)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));
        if (slotsOf == null) throw new ArgumentNullException(nameof(slotsOf));
        vaultPaid ??= Array.Empty<int>();

        var rows = new List<BundleProgress>();
        foreach (KeyValuePair<string, string> kv in bundleData)
        {
            if (kv.Key == null || kv.Value == null) continue;
            ParsedBundle parsed = BundleParsing.Parse(kv.Key, kv.Value);
            if (parsed.Room == AbandonedJojaRoom) continue;
            IReadOnlyList<bool>? slots = slotsOf(parsed.Index);
            bool paidVault = parsed.Room == VaultRoom && vaultPaid.Contains(parsed.Index);
            var filled = new bool[parsed.Ingredients.Count];
            for (int i = 0; i < filled.Length; i++)
                filled[i] = (slots != null && i < slots.Count && slots[i])
                            || (paidVault && parsed.Ingredients[i].ItemRef == AskKinds.MoneyRef);
            rows.Add(new BundleProgress(parsed.Index, parsed.Room, parsed.Ingredients, filled, parsed.NumberOfSlots));
        }
        rows.Sort((a, b) => a.Index.CompareTo(b.Index));
        return rows;
    }
}
