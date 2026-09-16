using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Derived.Input under every filter: a derived item's table under filter F is what the chain
/// gives from its inputs' own tables under F, including the combinations the trim dropped and the ones
/// neither input says anything about alone.</summary>
public class DerivedTests
{
    private const string A = "(O)1";
    private const string B = "(O)2";
    private const string Made = "(O)3";

    private static ObtainSource From(int day, bool island = false, bool yearTwo = false, bool owned = false)
        => new(SourceKind.Forage, DayTable.Available(d => d >= day), Reliability.Dependable,
            ObtainConditions.None with { GingerIsland = island, YearTwo = yearTwo, OwnedOnly = owned }, $"from {day}");

    private static IEnumerable<ObtainFilter> AllFilters()
    {
        foreach (bool island in new[] { false, true })
            foreach (bool yearTwo in new[] { false, true })
                foreach (bool owned in new[] { false, true })
                    foreach (ObtainFilter f in new[] { ObtainFilter.DependableOnly, ObtainFilter.Any })
                        yield return f with { IncludeGingerIsland = island, IncludeYearTwo = yearTwo, IncludeOwnedOnly = owned };
    }

    private static void AssertBothUnderEveryFilter(ObtainSource[] a, ObtainSource[] b)
    {
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [A] = a, [B] = b });
        ObtainSource[] made = Derived.Of(snapshot, A).Both(Derived.Of(snapshot, B))
            .Emit(SourceKind.Cooking, t => t, ObtainConditions.None, "made").ToArray();
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [Made] = made });
        foreach (ObtainFilter f in AllFilters())
        {
            Assert.Equal(snapshot.Table(A, f).Latest(snapshot.Table(B, f)), model.Table(Made, f));
            Assert.Equal(snapshot.UndelayedTable(A, f).Latest(snapshot.UndelayedTable(B, f)), model.UndelayedTable(Made, f));
        }
    }

    [Fact]
    public void A_trimmed_combination_reads_as_the_earliest_of_the_variants_it_includes()
    {
        // The reviewer's case: A says nothing new under year 2 or island plus year 2, so both are
        // trimmed; under island plus year 2 it must read as its island table (10), not its plain one.
        ObtainSource[] a = { From(50), From(10, island: true) };
        ObtainSource[] b = { From(50), From(40, island: true), From(45, yearTwo: true), From(20, island: true, yearTwo: true) };
        AssertBothUnderEveryFilter(a, b);

        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [A] = a, [B] = b });
        ObtainSource[] made = Derived.Of(snapshot, A).Both(Derived.Of(snapshot, B))
            .Emit(SourceKind.Cooking, t => t, ObtainConditions.None, "made").ToArray();
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [Made] = made });
        Assert.Equal(20, model.Lands(Made, 1, ObtainFilter.DependableOnly with { IncludeGingerIsland = true, IncludeYearTwo = true }));
    }

    [Fact]
    public void Two_inputs_flagged_differently_meet_under_the_filter_that_admits_both()
    {
        // A is island-only, B year 2-only: neither says anything under island plus year 2 alone, but
        // together they land there and nowhere else.
        AssertBothUnderEveryFilter(new[] { From(10, island: true) }, new[] { From(30, yearTwo: true) });
        AssertBothUnderEveryFilter(new[] { From(90), From(10, owned: true) }, new[] { From(80), From(30, island: true) });
    }

    [Fact]
    public void The_undelayed_side_is_derived_the_same_way()
    {
        ObtainSource deep = MineDepth.WithTravel(From(1) with
        {
            Conditions = ObtainConditions.None with { Requires = new[] { MineDepth.FloorPrefix + 100 } },
        });
        ObtainSource islandDeep = deep with { Conditions = deep.Conditions with { GingerIsland = true }, Detail = "island" };
        AssertBothUnderEveryFilter(new[] { deep, From(20, yearTwo: true) }, new[] { From(5), islandDeep });
    }

    private static ObtainabilityModel DeepOre(int floor)
    {
        ObtainSource deep = MineDepth.WithTravel(From(1) with
        {
            Conditions = ObtainConditions.None with { Requires = new[] { MineDepth.FloorPrefix + floor } },
        });
        return new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [A] = new[] { deep } });
    }

    [Fact]
    public void A_made_route_keeps_its_undelayed_landings_past_the_end_of_its_delayed_table()
    {
        DayTable lateWinter = DayTable.Available(d => d >= 110);
        ObtainSource made = Assert.Single(Derived.Of(DeepOre(80), A)
            .Emit(SourceKind.Machine, t => t.Then(lateWinter).Delay(1), ObtainConditions.None, "made"));
        // From day 106 the ore lands day 113 at the earliest, so the delayed table is empty from there.
        Assert.Equal(111, made.Lands.Lands(1));
        Assert.Null(made.Lands.Lands(106));
        Assert.Equal(111, made.Undelayed.Lands(106));
        Assert.Equal(A, Assert.Single(Assert.Single(made.Inputs)));
    }

    [Fact]
    public void A_made_route_whose_delayed_table_never_lands_is_still_emitted()
    {
        // The window closes on day 5, before a floor 120 ore can arrive from nothing (day 12), so only
        // a save already that deep can make it: the route must exist for the fairness rule to see.
        const int WindowCloses = 5;
        DayTable earlyWindow = DayTable.Exact(p => p <= WindowCloses ? WindowCloses : null);
        ObtainSource made = Assert.Single(Derived.Of(DeepOre(120), A)
            .Emit(SourceKind.Machine, t => t.Then(earlyWindow), ObtainConditions.None, "made"));
        Assert.True(made.Lands.IsEmpty);
        Assert.Equal(WindowCloses, made.Undelayed.Lands(1));
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { [Made] = new[] { made } });
        Assert.Null(model.Lands(Made, 1, ObtainFilter.Any));   // never lands from nothing
    }
}
