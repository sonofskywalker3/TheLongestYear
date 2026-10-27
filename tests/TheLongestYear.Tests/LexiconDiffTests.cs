using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public sealed class LexiconDiffTests
{
    private static ObtainSource Src(string detail, params string[][] inputs)
        => new(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail)
        {
            Inputs = inputs.Select(g => (IReadOnlyList<string>)g).ToList(),
        };

    private static AnswerVariant Variant(
        Dictionary<string, IReadOnlyList<ObtainSource>>? obtain = null,
        Dictionary<string, AvailabilityAnswers>? availability = null,
        string[]? unreached = null)
        => new(obtain ?? new Dictionary<string, IReadOnlyList<ObtainSource>>(), new Dictionary<string, IReadOnlyList<ObtainSource>>(),
            Array.Empty<string>(), 1, new Dictionary<string, PlaceWeek>(), unreached ?? Array.Empty<string>(), new Dictionary<string, PlaceWeek>())
        {
            Availability = availability ?? new Dictionary<string, AvailabilityAnswers>(),
        };

    private static Dictionary<string, IReadOnlyList<ObtainSource>> Obtain(string id, params ObtainSource[] sources)
        => new() { [id] = sources };

    private static AvailabilityAnswers Answers(string inputs, int effort = 1)
        => new(new Dictionary<string, ItemAvailability> { ["(O)24"] = new(Season.Spring, effort, "b") },
            new Dictionary<string, ItemEffort>(), new Dictionary<string, double[]> { ["(O)200"] = new[] { 1.0, 2.0 } },
            new Dictionary<string, int>()) { Inputs = inputs };

    [Fact]
    public void DiffOfIdenticalIsEmpty()
    {
        var v = Variant(Obtain("(O)24", Src("a")), new() { ["Normal|kitchen"] = Answers("x") });
        Assert.Empty(LexiconDiff.Compare(v, v with { }));
    }

    [Fact]
    public void DiffNamesTheItemAndField()
    {
        var a = Variant(Obtain("(O)24", Src("a"), Src("b"), Src("c")));
        var b = Variant(Obtain("(O)24", Src("a"), Src("b"), Src("c"), Src("d")));
        string line = Assert.Single(LexiconDiff.Compare(a, b));
        Assert.StartsWith("obtain (O)24: base 3 sources, live 4; first difference:", line);
    }

    [Fact]
    public void DiffIgnoresTheAvailabilityInputsStamp()
    {
        var a = Variant(availability: new() { ["Normal|kitchen"] = Answers("aaaa") });
        var b = Variant(availability: new() { ["Normal|kitchen"] = Answers("bbbb") });
        Assert.Empty(LexiconDiff.Compare(a, b));
        var c = Variant(availability: new() { ["Normal|kitchen"] = Answers("aaaa", effort: 9) });
        Assert.Contains("availability Normal|kitchen derived (O)24", Assert.Single(LexiconDiff.Compare(a, c)));
    }

    [Fact]
    public void DiffSeesAnInputsOnlyChange()
    {
        var a = Variant(Obtain("(O)24", Src("a", new[] { "(O)1" })));
        var b = Variant(Obtain("(O)24", Src("a", new[] { "(O)2" })));
        string line = Assert.Single(LexiconDiff.Compare(a, b));
        Assert.Contains("inputs", line);
    }

    [Fact]
    public void GapsGroupsByOwner()
    {
        var obtain = new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = Array.Empty<ObtainSource>(),
            ["(O)Foo_Thing"] = Array.Empty<ObtainSource>(),
            ["(O)Foo_Other"] = Array.Empty<ObtainSource>(),
            ["(O)Bar_Thing"] = new[] { Src("a") },
        };
        string report = LexiconGaps.Report(Variant(obtain, unreached: new[] { "Mod_Cave" }), new ObtainabilityInputs());
        Assert.Contains("## Foo (2)", report);
        Assert.Contains("## vanilla (1)", report);
        Assert.DoesNotContain("Bar_Thing", report);
        Assert.Contains("unreached map: Mod_Cave", report);
    }

    [Fact]
    public void GapsAttributesMapsAndUnresolvedText()
    {
        var v = Variant(unreached: new[] { "BathHouse_Entry", "Mod_Cave" }) with
        {
            Unresolved = new[] { "RECIPE (O)Foo_Pie | none", "LOCATION_FISH Beach X | Forage at Beach" },
        };
        string report = LexiconGaps.Report(v, new ObtainabilityInputs());
        Assert.Contains("## vanilla (1)", report);
        Assert.Contains("## Mod (1)", report);
        Assert.Contains("## Foo (1)", report);
        Assert.Contains("## unattributed (1)", report);
        Assert.DoesNotContain("## BathHouse", report);
    }
}
