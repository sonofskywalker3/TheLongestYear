using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityComparisonTests
{
    private static ObtainSource Source(DayTable lands, Reliability r = Reliability.Dependable, string detail = "test") =>
        new(SourceKind.Forage, lands, r, ObtainConditions.None with { Requires = new[] { "location:Town" } }, detail);

    private static ObtainSource Guessed(DayTable lands, string detail = "unlock:none") =>
        new(SourceKind.Cooking, lands, Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "recipe:Omelet" }, Unresolved = true }, detail);

    private static DayTable FromWeek(int week) => DayTable.Available(d => WeekMask.WeekOfDay(d) >= week);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)1"] = new[] { Source(FromWeek(2)) },
        ["(O)2"] = new[] { Source(FromWeek(9), detail: "Forage at Town"), Source(FromWeek(10), detail: "second"), Source(FromWeek(11), detail: "third"), Source(FromWeek(12), detail: "fourth") },
        ["(O)5"] = new[] { Source(FromWeek(4)) },
        ["(O)6"] = new[] { Source(FromWeek(3)) },
        ["(O)7"] = new[] { Source(DayTable.Always, Reliability.Chance, "shop Traveler") },
        // Dependable from week 1, but only through a source the model could not read; the one
        // dependable source it did read lands in week 6.
        ["(O)8"] = new[] { Guessed(FromWeek(1)), Source(FromWeek(6), detail: "shop SeedShop") },
    });

    private static readonly Dictionary<string, (int Pacing, int Hard, string Basis)> Existing = new()
    {
        ["(O)1"] = (3, 2, "rule a"), ["(O)2"] = (5, 5, "rule b"), ["(O)3"] = (1, 1, "rule c"), ["(O)6"] = (8, 8, "rule d"), ["(O)7"] = (4, 4, "rule e"),
    ["(O)8"] = (6, 6, "rule f"),
    };

    private static IReadOnlyList<CompareRow> Rows() => ObtainabilityComparison.Compare(
        new[] { "(O)1", "2", "(O)3", "(O)4", "(O)6", "(O)7", "(O)8" }, id => Existing.ContainsKey(id), id => Existing[id], Model);

    [Fact]
    public void Verdicts_compare_the_existing_hard_week_with_the_new_dependable_landing_week_from_day_1()
    {
        var byId = Rows().ToDictionary(r => r.ItemId, r => r.Verdict);
        Assert.Equal(CompareVerdict.Agree, byId["(O)1"]);
        Assert.Equal(CompareVerdict.NewLater, byId["(O)2"]);
        Assert.Equal(CompareVerdict.OnlyExisting, byId["(O)3"]);
        Assert.False(byId.ContainsKey("(O)4"));
        Assert.Equal(CompareVerdict.OnlyNew, byId["(O)5"]);
        Assert.Equal(CompareVerdict.NewEarlier, byId["(O)6"]);
        Assert.Equal(CompareVerdict.LuckOnly, byId["(O)7"]);
        CompareRow cart = Rows().Single(r => r.ItemId == "(O)7");
        Assert.Null(cart.NewDependable);
        Assert.Equal(1, cart.NewAny);
    }

    [Fact]
    public void An_item_dependable_only_through_an_unresolved_source_says_so_and_is_counted()
    {
        CompareRow row = Rows().Single(r => r.ItemId == "(O)8");
        Assert.Equal(1, row.NewDependable);                 // the headline week, unresolved source included
        Assert.Equal(6, row.NewDependableKnown);            // the earliest week a source the model read reaches
        Assert.Equal(CompareVerdict.NewEarlier, row.Verdict);   // the verdict still rests on NewDependable
        string text = ObtainabilityComparison.Render(Rows(), Model, new string[0], id => "Name " + id, "0.18.4");
        Assert.Contains("| Dependable only through an unresolved source | 1 |", text);
        Assert.Contains("- new dependable 1 (unresolved; known-source week 6), any 1", text);
        Assert.Contains("- new dependable 3, any 3", text);   // (O)6: both agree, so no parenthesis
    }

    [Fact]
    public void The_report_details_only_the_verdicts_that_need_a_ruling_and_lists_only_new_by_name()
    {
        string text = ObtainabilityComparison.Render(Rows(), Model, new[] { "LOCATION_FISH Beach X | Forage at Beach" }, id => "Name " + id, "0.18.4");
        Assert.Contains("# Item obtainability comparison", text);
        Assert.Contains("| NewLater | 1 |", text);
        Assert.Contains("| LuckOnly | 1 |", text);
        Assert.Contains("| OnlyNew | 1 |", text);
        Assert.Contains("## NewEarlier", text);
        Assert.Contains("## LuckOnly", text);
        Assert.Contains("## OnlyExisting", text);
        Assert.Contains("Name (O)2", text);
        Assert.Contains("fourth", text);
        Assert.Contains("lands wk9/wk9/wk9/wk13", text);
        Assert.Contains("needs location:Town", text);
        Assert.Contains("existing basis: rule b", text);
        Assert.Contains("## OnlyNew (names only)", text);
        Assert.Contains("- (O)5 Name (O)5", text);
        int onlyNewAt = text.IndexOf("## OnlyNew (names only)");
        Assert.True(text.IndexOf("## Agree") < onlyNewAt);
        Assert.DoesNotContain("### (O)5", text);                 // no detail block for OnlyNew
        Assert.Contains("## Unresolved sources (1)", text);
        Assert.DoesNotContain("\u2014", text);
    }
}
