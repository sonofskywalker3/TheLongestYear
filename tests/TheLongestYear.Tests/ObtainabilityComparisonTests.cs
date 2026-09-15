using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityComparisonTests
{
    private static ObtainSource Source(WeekMask weeks, string detail = "test") =>
        new(SourceKind.Forage, DayTable.InWeeks(weeks), Reliability.Dependable, ObtainConditions.None with { Requires = new[] { "location:Town" } }, detail);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)1"] = new[] { Source(WeekMask.FromWeekOnwardOf(2)) },
        ["(O)2"] = new[] { Source(WeekMask.FromWeekOnwardOf(9), "Forage at Town"), Source(WeekMask.Of(10), "second"), Source(WeekMask.Of(11), "third"), Source(WeekMask.Of(12), "fourth") },
        ["(O)5"] = new[] { Source(WeekMask.Of(4)) },
        ["(O)6"] = new[] { Source(WeekMask.FromWeekOnwardOf(3)) },
    });

    private static readonly Dictionary<string, (int Pacing, int Hard, string Basis)> Existing = new()
    {
        ["(O)1"] = (3, 2, "rule a"), ["(O)2"] = (5, 5, "rule b"), ["(O)3"] = (1, 1, "rule c"), ["(O)6"] = (8, 8, "rule d"),
    };

    private static IReadOnlyList<CompareRow> Rows() => ObtainabilityComparison.Compare(
        new[] { "(O)1", "2", "(O)3", "(O)4", "(O)6" }, id => Existing.ContainsKey(id), id => Existing[id], Model);

    [Fact]
    public void Verdicts_compare_the_existing_hard_week_with_the_new_earliest_week()
    {
        var byId = Rows().ToDictionary(r => r.ItemId, r => r.Verdict);
        Assert.Equal(CompareVerdict.Agree, byId["(O)1"]);
        Assert.Equal(CompareVerdict.NewLater, byId["(O)2"]);
        Assert.Equal(CompareVerdict.OnlyExisting, byId["(O)3"]);
        Assert.False(byId.ContainsKey("(O)4"));                 // neither side knows it
        Assert.Equal(CompareVerdict.OnlyNew, byId["(O)5"]);
        Assert.Equal(CompareVerdict.NewEarlier, byId["(O)6"]);
        Assert.Equal("rule c", Rows().Single(r => r.ItemId == "(O)3").ExistingBasis);
    }

    [Fact]
    public void The_report_has_a_summary_every_source_the_existing_basis_and_diagnostics()
    {
        string text = ObtainabilityComparison.Render(Rows(), Model, new[] { "LOCATION_FISH Beach X | Forage at Beach" }, id => "Name " + id, "0.18.4");
        Assert.Contains("# Item obtainability comparison", text);
        Assert.Contains("| NewLater | 1 |", text);
        Assert.Contains("## NewEarlier", text);
        Assert.Contains("## OnlyExisting", text);
        Assert.Contains("Name (O)2", text);
        Assert.Contains("fourth", text);                       // all four sources, not the first three
        Assert.Contains("needs location:Town", text);          // conditions shown
        Assert.Contains("existing basis: rule b", text);
        Assert.Contains("## Unresolved sources (1)", text);
        Assert.Contains("LOCATION_FISH Beach X", text);
        Assert.DoesNotContain("\u2014", text);                 // no em dashes
    }
}
