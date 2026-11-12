using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The words of the loop recap page and the shop's suggested rows (spec 2026-11-10
/// shrine planning, features 1 and 2).</summary>
public static class LoopRecapText
{
    private const int One = 1;

    /// <summary>Text scale the recap page shrinks to before it drops lines.</summary>
    public const float MinScale = 0.7f;

    /// <summary>How much the text shrinks per fitting step.</summary>
    public const float ScaleStep = 0.05f;

    /// <summary>How the recap page fits a short screen: the largest text scale from 1 down to
    /// <see cref="MinScale"/> at which the body fits <paramref name="available"/> pixels, first with
    /// the per-source JP line and then without it. When nothing fits it returns the floor without
    /// the sources line (the page then trims lines from the bottom).</summary>
    /// <param name="bodyHeight">The body's drawn height at a scale, with or without the sources line.</param>
    public static (float Scale, bool ShowSources) Fit(Func<float, bool, int> bodyHeight, int available)
    {
        int steps = (int)Math.Round((1f - MinScale) / ScaleStep);
        foreach (bool sources in new[] { true, false })
            for (int step = 0; step <= steps; step++)
            {
                float scale = 1f - step * ScaleStep;
                if (bodyHeight(scale, sources) <= available)
                    return (scale, sources);
            }
        return (MinScale, false);
    }

    public static string Title(LoopSummary s)
        => Strings.Get("recap.title", new Dictionary<string, string> { ["n"] = s.LoopNumber.ToString() });

    public static string JpLine(LoopSummary s)
        => s.Earned.SinceUpdate
            ? Strings.Get("recap.jp-since-update", new Dictionary<string, string> { ["jp"] = s.Earned.Total().ToString() })
            : Strings.Get("recap.jp", new Dictionary<string, string> { ["jp"] = s.Earned.Total().ToString() });

    public static string SourcesLine(LoopSummary s)
        => Strings.Get("recap.jp-sources", new Dictionary<string, string>
        {
            ["a"] = s.Earned.Bundles.ToString(),
            ["b"] = s.Earned.Rooms.ToString(),
            ["c"] = s.Earned.WeeklyThemes.ToString(),
            ["d"] = s.Earned.ShrineGoals.ToString(),
            ["e"] = s.Earned.Checkpoints.ToString(),
        });

    /// <summary>The planning hub's one line: this loop's JP so far and where it came from, the
    /// same five sources the recap sums at the rewind.</summary>
    public static string HubLine(JpLedger earned)
        => Strings.Get(earned.SinceUpdate ? "menu.hub.jp-earned-since-update" : "menu.hub.jp-earned",
            new Dictionary<string, string>
            {
                ["jp"] = earned.Total().ToString(),
                ["a"] = earned.Bundles.ToString(),
                ["b"] = earned.Rooms.ToString(),
                ["c"] = earned.WeeklyThemes.ToString(),
                ["d"] = earned.ShrineGoals.ToString(),
                ["e"] = earned.Checkpoints.ToString(),
            });

    public static string BundlesLine(LoopSummary s)
        => Strings.Get("recap.bundles", new Dictionary<string, string>
        {
            ["done"] = s.BundlesDone.ToString(),
            ["total"] = s.BundlesTotal.ToString(),
        });

    public static string DonationsLine(LoopSummary s)
        => Strings.Get("recap.donations", new Dictionary<string, string> { ["n"] = s.DonationsStillNeeded.ToString() });

    /// <summary>Null when the darkness did nothing this loop (a loop that never left Spring).</summary>
    public static string? DarknessLine(LoopSummary s)
    {
        if (s.Tampers == 0 && s.BlightNights == 0)
            return null;
        // "1 ask" / "3 asks": each count with its own singular or plural noun.
        string asks = Strings.Get(s.Tampers == SingleCount ? "recap.darkness.ask" : "recap.darkness.asks",
            new Dictionary<string, string> { ["n"] = s.Tampers.ToString() });
        string nights = Strings.Get(s.BlightNights == SingleCount ? "recap.darkness.night" : "recap.darkness.nights",
            new Dictionary<string, string> { ["n"] = s.BlightNights.ToString() });
        return Strings.Get("recap.darkness", new Dictionary<string, string> { ["asks"] = asks, ["nights"] = nights });
    }

    private const int SingleCount = 1;

    public static string SuggestedHeader() => Strings.Get("recap.suggested");

    public static string ContinueButton() => Strings.Get("recap.continue");

    /// <summary>"Keep name: reason" for the recap list.</summary>
    public static string SuggestionLine(KeepSuggestion suggestion, bool heldBoard)
        => Strings.Get("recap.suggestion", new Dictionary<string, string>
        {
            ["keep"] = UpgradeCatalog.TryGet(suggestion.UpgradeId)?.DisplayName ?? suggestion.UpgradeId,
            ["reason"] = Reason(suggestion, heldBoard),
        });

    /// <summary>Why a keep is suggested: the asks missed last loop, or on a held board the asks
    /// the coming board makes.</summary>
    public static string Reason(KeepSuggestion suggestion, bool heldBoard)
    {
        string? item = ItemText(suggestion, heldBoard);
        if (item != null)
            return ItemReason(suggestion.Count, item, heldBoard);
        if (heldBoard)
            return Strings.Get("suggest.reason.held", new Dictionary<string, string>
            {
                ["count"] = suggestion.Count.ToString(),
                ["kind"] = suggestion.Count == One ? HeldKindOne(suggestion.Kind) : HeldKind(suggestion.Kind),
            });
        return suggestion.Count == One
            ? Strings.Get("suggest.reason.missed-one", new Dictionary<string, string> { ["kind"] = MissedKind(suggestion.Kind) })
            : Strings.Get("suggest.reason.missed", new Dictionary<string, string>
            {
                ["count"] = suggestion.Count.ToString(),
                ["kind"] = MissedKind(suggestion.Kind),
            });
    }

    /// <summary>The reason of an item-specific keep: "You missed 3 Garlic asks last loop.",
    /// "The board you're keeping asks for 2 year-two crops."</summary>
    private static string ItemReason(int count, string item, bool heldBoard)
    {
        if (heldBoard)
            return Strings.Get("suggest.reason.item-held", new Dictionary<string, string>
            {
                ["count"] = count.ToString(),
                ["item"] = item,
            });
        return count == One
            ? Strings.Get("suggest.reason.item-missed-one", new Dictionary<string, string> { ["item"] = item })
            : Strings.Get("suggest.reason.item-missed", new Dictionary<string, string>
            {
                ["count"] = count.ToString(),
                ["item"] = item,
            });
    }

    /// <summary>The item an item-specific keep names, or null for a broad keep. A missed reason
    /// reads "year-two crop asks", a held one "1 year-two crop" or "3 year-two crops"; Garlic and
    /// Red Cabbage use the game's display name as is.</summary>
    private static string? ItemText(KeepSuggestion suggestion, bool heldBoard) => suggestion.UpgradeId switch
    {
        YearTwoCrops.PierreUpgrade => Strings.Get(heldBoard && suggestion.Count != One
            ? "suggest.item.year-two-crops" : "suggest.item.year-two-crop"),
        KeepSuggestions.FortuneRareFish => Strings.Get("suggest.item.rare-fish"),
        _ => suggestion.Item,
    };

    /// <summary>The kind as it reads before "asks": "fish asks", "crop asks".</summary>
    private static string MissedKind(AskKind kind) => kind switch
    {
        AskKind.Fish => Strings.Get("suggest.kind.fish"),
        AskKind.Crops => Strings.Get("suggest.kind.crops"),
        AskKind.Forage => Strings.Get("suggest.kind.forage"),
        AskKind.Minerals => Strings.Get("suggest.kind.minerals"),
        AskKind.AnimalProducts => Strings.Get("suggest.kind.animal-products"),
        AskKind.CookedDishes => Strings.Get("suggest.kind.cooked-dishes"),
        AskKind.ArtisanGoods => Strings.Get("suggest.kind.artisan-goods"),
        AskKind.Gold => Strings.Get("suggest.kind.gold"),
        _ => "",
    };

    /// <summary>The kind as a plural noun: "asks for 5 crops".</summary>
    private static string HeldKind(AskKind kind) => kind switch
    {
        AskKind.Fish => Strings.Get("suggest.held-kind.fish"),
        AskKind.Crops => Strings.Get("suggest.held-kind.crops"),
        AskKind.Forage => Strings.Get("suggest.held-kind.forage"),
        AskKind.Minerals => Strings.Get("suggest.held-kind.minerals"),
        AskKind.AnimalProducts => Strings.Get("suggest.held-kind.animal-products"),
        AskKind.CookedDishes => Strings.Get("suggest.held-kind.cooked-dishes"),
        AskKind.ArtisanGoods => Strings.Get("suggest.held-kind.artisan-goods"),
        AskKind.Gold => Strings.Get("suggest.held-kind.gold"),
        _ => "",
    };

    /// <summary>The kind as a singular noun: "asks for 1 crop".</summary>
    private static string HeldKindOne(AskKind kind) => kind switch
    {
        AskKind.Fish => Strings.Get("suggest.held-kind-one.fish"),
        AskKind.Crops => Strings.Get("suggest.held-kind-one.crops"),
        AskKind.Forage => Strings.Get("suggest.held-kind-one.forage"),
        AskKind.Minerals => Strings.Get("suggest.held-kind-one.minerals"),
        AskKind.AnimalProducts => Strings.Get("suggest.held-kind-one.animal-products"),
        AskKind.CookedDishes => Strings.Get("suggest.held-kind-one.cooked-dishes"),
        AskKind.ArtisanGoods => Strings.Get("suggest.held-kind-one.artisan-goods"),
        AskKind.Gold => Strings.Get("suggest.held-kind-one.gold"),
        _ => "",
    };
}
