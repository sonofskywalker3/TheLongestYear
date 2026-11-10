using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The words of the loop recap page and the shop's suggested rows (spec 2026-11-10
/// shrine planning, features 1 and 2).</summary>
public static class LoopRecapText
{
    private const int One = 1;

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
        => s.Tampers == 0 && s.BlightNights == 0
            ? null
            : Strings.Get("recap.darkness", new Dictionary<string, string>
            {
                ["tampers"] = s.Tampers.ToString(),
                ["blights"] = s.BlightNights.ToString(),
            });

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
