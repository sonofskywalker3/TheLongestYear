using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One question the two models answered differently.</summary>
public sealed record TravelMismatch(
    string ItemId, int HitDay, string Save, DifficultyStep Level, string Deadline, string WithTravel, string WithoutTravel)
{
    public override string ToString()
        => $"{ItemId} hit day {HitDay}, {Save}, {Level}, {Deadline}: with travel {WithTravel}; without {WithoutTravel}";
}

public sealed record TravelCheckResult(int Comparisons, IReadOnlyList<TravelMismatch> Mismatches);

/// <summary>The debug check behind <c>tly_sabotage travelcheck</c>: the mine-depth ruling puts the days
/// it takes to reach a floor into the model's landing days, and the fairness rule must still answer
/// exactly as it did with no travel in the model, because it judges every route on its undelayed table
/// and prices the save's real depth itself. This asks both models the same questions and lists every
/// answer that differs: each item, a spread of hit days, mine depths (and the bus, for Skull Cavern),
/// every level, and both the reversion and the tampering deadline.</summary>
public static class MineTravelCheck
{
    public static readonly IReadOnlyList<int> HitDays = new[] { 1, 29, 57, 85, 104, 108 };
    public static readonly IReadOnlyList<int> Depths = new[] { 0, 40, 80, 120 };
    private const string BusMail = "ccVault";
    private const string NeverLands = "never";

    /// <summary>The saves asked about: <paramref name="baseSave"/> (an empty save by default) at each
    /// depth, and at depth 0 with the bus running.</summary>
    public static IReadOnlyList<(string Name, SaveSnapshot Save)> Saves(SaveSnapshot? baseSave = null)
    {
        SaveSnapshot start = baseSave ?? SaveSnapshot.Empty;
        var saves = Depths
            .Select(d => ($"floor {d}", start with { DeepestMineFloor = d }))
            .ToList();
        var bus = new HashSet<string>(start.MailFlags, StringComparer.Ordinal) { BusMail };
        saves.Add(("bus running", start with { DeepestMineFloor = 0, MailFlags = bus }));
        return saves;
    }

    /// <param name="baseSave">What every asked save has besides its depth and the bus; an empty save
    /// when null, which leaves every route that needs a machine, a recipe or an animal out below
    /// Extreme.</param>
    public static TravelCheckResult Compare(
        ObtainabilityModel withTravel, ObtainabilityModel withoutTravel, SaveSnapshot? baseSave = null)
    {
        if (withTravel is null) throw new ArgumentNullException(nameof(withTravel));
        if (withoutTravel is null) throw new ArgumentNullException(nameof(withoutTravel));
        IEnumerable<string> items = withTravel.ItemIds.Union(withoutTravel.ItemIds, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);
        IReadOnlyList<(string Name, SaveSnapshot Save)> saves = Saves(baseSave);
        var mismatches = new List<TravelMismatch>();
        int comparisons = 0;
        foreach (string item in items)
            foreach (int hit in HitDays)
                foreach ((string name, SaveSnapshot save) in saves)
                    foreach (DifficultyStep level in Enum.GetValues<DifficultyStep>())
                    {
                        int reversion = FairnessRule.ReversionDeadline(hit, level);
                        foreach ((string deadlineName, int deadline) in new[]
                        {
                            ($"reversion (day {reversion})", reversion),
                            ($"tampering (day {FairnessRule.TamperDeadline})", FairnessRule.TamperDeadline),
                        })
                        {
                            comparisons++;
                            string a = Answer(FairnessRule.Judge(item, hit, deadline, level, save, withTravel));
                            string b = Answer(FairnessRule.Judge(item, hit, deadline, level, save, withoutTravel));
                            if (a != b) mismatches.Add(new TravelMismatch(item, hit, name, level, deadlineName, a, b));
                        }
                    }
        return new TravelCheckResult(comparisons, mismatches);
    }

    /// <summary>The verdict and the earliest landing among the counting routes.</summary>
    private static string Answer(FairnessVerdict verdict)
    {
        int? best = verdict.Routes.Where(r => r.Counts).Select(r => r.LandingDay).Min();
        return $"{(verdict.Counts ? "counts" : "out")}, lands {(best is int day ? day.ToString() : NeverLands)}";
    }
}
