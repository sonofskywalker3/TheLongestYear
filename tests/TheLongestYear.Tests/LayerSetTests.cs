using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Supported-mod layers: the order they compose in, which apply, their rulings, their
/// injected sources, and how the resolver treats the composed set.</summary>
public sealed class LayerSetTests
{
    private const string Game = "1.6.15";
    private const string Mod = "1.1.0";
    private const string A = "ModA";
    private const string B = "ModB";
    private const string Item = "(O)Shared.Thing";
    private const string Olive = "(O)ModA.Olive";
    private const string Jelly = "(O)344";
    private const int FruitCategory = -79;
    private static readonly Lazy<ObtainabilityInputs> Inputs = new(VanillaInputsFixture.Load);

    private static ObtainSource Src(string detail)
        => new(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail);

    private static AnswerVariant Variant(params (string Id, string Detail)[] routes)
    {
        var obtain = routes.ToDictionary(r => r.Id, r => (IReadOnlyList<ObtainSource>)new[] { Src(r.Detail) }, StringComparer.Ordinal);
        return new AnswerVariant(obtain, obtain, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
    }

    private static AnswerSet VanillaSet(IReadOnlyDictionary<string, ulong>? rows = null)
        => new(Game, Mod, new[] { RowHashes.Vanilla }, rows ?? new Dictionary<string, ulong> { ["obj|(O)1"] = 1 },
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant((Item, "vanilla")) });

    private static LayerInputs In(string id, string version = "1.0.0",
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>>? extra = null,
        IReadOnlyDictionary<string, YearRuling>? years = null, IReadOnlyDictionary<string, int>? weeks = null)
        => new(id, version, extra ?? new Dictionary<string, IReadOnlyList<ObtainSource>>(),
            years ?? new Dictionary<string, YearRuling>(), weeks ?? new Dictionary<string, int>(), "");

    private static LayerAnswers Ans(IReadOnlyDictionary<string, AnswerVariant> deltas, IReadOnlyDictionary<string, ulong>? rows, params string[] mods)
        => new(mods, string.Join("+", mods.Select(m => m + "@1.0.0")),
            rows == null
                ? new Dictionary<string, IReadOnlyDictionary<string, ulong>>()
                : new Dictionary<string, IReadOnlyDictionary<string, ulong>> { [AnswerSet.BaseVariant] = rows },
            deltas);

    private static LayerAnswers Ans(string detail, params string[] mods)
        => Ans(new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant((Item, detail)) }, null, mods);

    private static string DetailOf(AnswerSet set, string id) => set.Resolve(AnswerSet.BaseVariant)!.Obtain[id].Single().Detail;

    private static ObtainabilityInputs WithOlive(ObtainabilityInputs inputs, int price = 50)
    {
        var objects = new Dictionary<string, ObjInfo>(inputs.Objects, StringComparer.Ordinal)
        {
            [Olive] = new ObjInfo(Olive, "Olive", FruitCategory, price, new[] { "category_fruits", "fruit_item" }, false),
        };
        return inputs with { Objects = objects };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> OliveSource()
        => new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal) { [Olive] = new[] { Src("layer: olive grove") } };

    [Fact]
    public void ComposeOrderLaterWins()
    {
        var warnings = new List<string>();
        // Singles in file order (B's file before A's), so A's delta is the later one.
        LayerSet singles = LayerSet.Compose(VanillaSet(), new[] { In(A), In(B) }, new[] { Ans("B", B), Ans("A", A) }, warnings.Add);
        Assert.Equal("A", DetailOf(singles.Overlay!, Item));

        // An interaction delta comes after every single, wherever its file sits.
        LayerSet withPair = LayerSet.Compose(VanillaSet(), new[] { In(A), In(B) },
            new[] { Ans("A+B", A, B), Ans("B", B), Ans("A", A) }, warnings.Add);
        Assert.Equal("A+B", DetailOf(withPair.Composed!, Item));
        Assert.Equal(new[] { RowHashes.Vanilla, A, B, A + "+" + B }, withPair.Composed!.Layers);
        Assert.Equal(new[] { A, B }, withPair.Ids);
        Assert.Empty(warnings);
    }

    [Fact]
    public void InteractionDeltaAppliesOnlyWhenAllModsActive()
    {
        var warnings = new List<string>();
        LayerInputs[] inputs = { In(A), In(B) };
        LayerAnswers[] answers = { Ans("A", A), Ans("B", B), Ans("A+B", A, B) };
        var (activeInputs, activeAnswers) = LayerSet.Active(inputs, answers, id => id == A, _ => "1.0.0", warnings.Add);
        Assert.Equal(new[] { A }, activeInputs.Select(i => i.UniqueId));
        Assert.Equal(new[] { A }, activeAnswers.Select(a => LayerFile.Name(a.Mods)));

        LayerSet set = LayerSet.Compose(VanillaSet(), activeInputs, activeAnswers, warnings.Add);
        Assert.Equal("A", DetailOf(set.Composed!, Item));
        Assert.Equal(new[] { RowHashes.Vanilla, A }, set.Composed!.Layers);

        // Compose itself also skips a delta naming a mod that is not active.
        LayerSet direct = LayerSet.Compose(VanillaSet(), new[] { In(A) }, answers, warnings.Add);
        Assert.Equal("A", DetailOf(direct.Composed!, Item));
        Assert.Empty(warnings);
    }

    [Fact]
    public void ConflictingRulingWarns()
    {
        var warnings = new List<string>();
        LayerInputs a = In(A, years: new Dictionary<string, YearRuling> { [Olive] = YearRuling.YearTwo, ["(O)Same"] = YearRuling.YearTwo },
            weeks: new Dictionary<string, int> { [Olive] = 3 });
        LayerInputs b = In(B, years: new Dictionary<string, YearRuling> { [Olive] = YearRuling.YearOne, ["(O)Same"] = YearRuling.YearTwo },
            weeks: new Dictionary<string, int> { [Olive] = 5 });
        LayerSet set = LayerSet.Compose(VanillaSet(), new[] { a, b }, Array.Empty<LayerAnswers>(), warnings.Add);

        Assert.Equal(YearRuling.YearOne, set.YearRuling(Olive));
        Assert.Equal(YearRuling.YearTwo, set.YearRuling("(O)Same"));
        Assert.Null(set.YearRuling("(O)Unruled"));
        Assert.Equal(5, set.WeekRulings[Olive]);
        Assert.Equal(2, warnings.Count);   // the year and the week conflict on Olive; the agreeing ruling is silent
        Assert.All(warnings, w => { Assert.Contains(A, w); Assert.Contains(B, w); Assert.Contains(Olive, w); });
    }

    [Fact]
    public void MismatchedVersionStillApplies()
    {
        ObtainabilityInputs vanillaInputs = Inputs.Value;
        ObtainabilityInputs checkedInputs = WithOlive(vanillaInputs, price: 50);   // the data the layer was mapped on
        ObtainabilityInputs live = WithOlive(vanillaInputs, price: 60);            // the installed version changed a row
        IReadOnlyDictionary<string, ulong> vanillaRows = RowHashes.Of(vanillaInputs, false);
        var layerRows = RowHashes.Of(checkedInputs, false).Where(p => !vanillaRows.ContainsKey(p.Key))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

        var warnings = new List<string>();
        LayerInputs[] inputs = { In(A, "1.0.0", OliveSource(), new Dictionary<string, YearRuling> { [Olive] = YearRuling.YearTwo }) };
        LayerAnswers[] answers =
        {
            Ans(new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant((Olive, "mapped")) }, layerRows, A),
        };
        var (activeInputs, activeAnswers) = LayerSet.Active(inputs, answers, _ => true, _ => "1.1.0", warnings.Add);
        Assert.Equal($"Lexicon layer {A}: checked against 1.0.0, installed 1.1.0; applying its routes, rows that changed are recomputed",
            Assert.Single(warnings));
        Assert.Single(activeInputs);
        Assert.Single(activeAnswers);

        LayerSet set = LayerSet.Compose(VanillaSet(vanillaRows), activeInputs, activeAnswers, warnings.Add);
        var resolver = new LexiconResolver(set.Composed, _ => null, (_, _) => { },
            (inp, _) =>
            {
                ObtainabilityBuild built = ObtainabilityBuilder.Build(inp, extraDirect: set.ExtraDirectSources);
                return AnswerVariantFactory.From(built, built, inp, null, Array.Empty<string>(), null);
            });

        // At the checked data the layer is served as shipped; at the installed data it is recomputed.
        Assert.Equal(AnswerOrigin.Shipped, resolver.Resolve(checkedInputs, false, Game, Mod, set.Composed!.Layers).Origin);
        Resolved r = resolver.Resolve(live, false, Game, Mod, set.Composed!.Layers);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Contains(r.Variant.Obtain[Olive], s => s.Detail == "layer: olive grove");

        try
        {
            YearTwoCartPool.LayerRuling = set.YearRuling;
            Assert.True(YearTwoCartPool.Offers(new CartCandidate(Olive, "(O)", true, YearTwoReason.YearOne)));
        }
        finally
        {
            YearTwoCartPool.LayerRuling = null;
        }
        Assert.False(YearTwoCartPool.Offers(new CartCandidate(Olive, "(O)", true, YearTwoReason.YearOne)));
    }

    [Fact]
    public void InjectedExtraSourceFeedsDerivedRoutes()
    {
        ObtainabilityInputs inputs = WithOlive(Inputs.Value);
        LayerSet set = LayerSet.Compose(VanillaSet(), new[] { In(A, extra: OliveSource()) }, Array.Empty<LayerAnswers>(), _ => { });

        ObtainabilityBuild plain = ObtainabilityBuilder.Build(inputs);
        ObtainabilityBuild injected = ObtainabilityBuilder.Build(inputs, extraDirect: set.ExtraDirectSources);

        static bool FromOlive(ObtainabilityModel m) => m.Sources(Jelly).Any(s => s.Kind == SourceKind.Machine && s.Inputs.Any(g => g.Contains(Olive)));
        Assert.Empty(plain.Model.Sources(Olive));
        Assert.False(FromOlive(plain.Model));
        Assert.Contains(injected.Model.Sources(Olive), s => s.Detail == "layer: olive grove");
        Assert.True(FromOlive(injected.Model));
    }

    [Fact]
    public void EmptyDeltaFileCountsAsMapped()
    {
        var warnings = new List<string>();
        LayerInputs[] inputs = { In(A), In(B) };
        var empty = Ans(new Dictionary<string, AnswerVariant>(StringComparer.Ordinal), null, B, A);

        // The file round-trips with "Deltas": {} under the sorted pair name.
        string json = LayerFile.WriteAnswers(empty);
        Assert.Contains("\"Deltas\": {}", json);
        Assert.Equal("ModA+ModB.answers.json", LayerFile.AnswersFileName(empty.Mods));
        LayerAnswers read = LayerFile.ReadAnswers(json);
        Assert.Empty(read.Deltas);

        LayerSet unmapped = LayerSet.Compose(VanillaSet(), inputs, new[] { Ans("A", A), Ans("B", B) }, warnings.Add);
        Assert.Equal(new[] { A + "+" + B }, unmapped.UnmappedPairs);
        Assert.Empty(unmapped.Composed!.Variants);   // never served as shipped until the pair is mapped

        LayerSet mapped = LayerSet.Compose(VanillaSet(), inputs, new[] { Ans("A", A), Ans("B", B), read }, warnings.Add);
        Assert.Empty(mapped.UnmappedPairs);
        Assert.Equal("B", DetailOf(mapped.Composed!, Item));
        Assert.Equal(new[] { RowHashes.Vanilla, A, B, A + "+" + B }, mapped.Composed!.Layers);
        Assert.Empty(warnings);
    }

    [Fact]
    public void GeneratorWritesTheDeltaOverTheLayersBelow()
    {
        var vanillaRows = new Dictionary<string, ulong>(StringComparer.Ordinal) { ["obj|(O)1"] = 1, ["shop|SeedShop|(O)1|"] = 2 };
        var liveRows = new Dictionary<string, ulong>(vanillaRows, StringComparer.Ordinal) { ["obj|" + Olive] = 3, ["shop|SeedShop|(O)1|"] = 4 };
        AnswerSet vanilla = VanillaSet(vanillaRows);
        AnswerVariant live = Variant((Item, "vanilla"), (Olive, "grove"));

        LayerAnswers single = LayerDelta.Write(A, vanilla, new[] { In(A) }, Array.Empty<LayerAnswers>(),
            AnswerSet.BaseVariant, live, liveRows, _ => "1.1.0", existing: null);
        Assert.Equal(new[] { A }, single.Mods);
        Assert.Equal("ModA@1.1.0", single.CheckedVersions);
        Assert.Equal(new[] { Olive }, single.Deltas[AnswerSet.BaseVariant].Obtain.Keys);
        // The mod's own row, and the vanilla row it edits.
        Assert.Equal(new[] { "obj|" + Olive, "shop|SeedShop|(O)1|" }, single.VariantRowHashes[AnswerSet.BaseVariant].Keys.OrderBy(k => k, StringComparer.Ordinal));

        // Over vanilla + A, the pair adds nothing: an empty map, and the file says so.
        LayerAnswers pair = LayerDelta.Write(B + "+" + A, vanilla, new[] { In(A), In(B) }, new[] { single },
            AnswerSet.BaseVariant, live, liveRows, _ => "1.0.0", existing: null);
        Assert.Equal(new[] { A, B }, pair.Mods);
        Assert.Empty(pair.Deltas);
        Assert.Empty(pair.VariantRowHashes);
        Assert.Contains("\"Deltas\": {}", LayerFile.WriteAnswers(pair));

        // Round trip, then the composed set with both is served as shipped data would be.
        LayerAnswers back = LayerFile.ReadAnswers(LayerFile.WriteAnswers(single));
        Assert.Equal("grove", back.Deltas[AnswerSet.BaseVariant].Obtain[Olive].Single().Detail);
        LayerSet set = LayerSet.Compose(vanilla, new[] { In(A), In(B) }, new[] { back, pair }, _ => { });
        Assert.Empty(set.UnmappedPairs);
        Assert.Equal(liveRows.OrderBy(p => p.Key), set.Composed!.RowHashes.OrderBy(p => p.Key));
        Assert.Equal("grove", DetailOf(set.Composed!, Olive));
    }

    [Fact]
    public void InputsFileRoundTrips()
    {
        LayerInputs layer = In(A, "2.0.0", OliveSource(), new Dictionary<string, YearRuling> { [Olive] = YearRuling.NeverSell },
            new Dictionary<string, int> { [Olive] = 7 });
        LayerInputs back = LayerFile.ReadInputs(LayerFile.WriteInputs(layer));
        Assert.Equal(A, back.UniqueId);
        Assert.Equal("2.0.0", back.CheckedVersion);
        Assert.Equal(OliveSource()[Olive], back.ExtraSources[Olive]);
        Assert.Equal(YearRuling.NeverSell, back.YearRulings[Olive]);
        Assert.Equal(7, back.WeekRulings[Olive]);

        LayerInputs sparse = LayerFile.ReadInputs("{\"UniqueId\": \"ModC\", \"CheckedVersion\": \"1.0.0\"}");
        Assert.Empty(sparse.ExtraSources);
        Assert.Empty(sparse.YearRulings);
        Assert.Empty(sparse.WeekRulings);
    }

    [Fact]
    public void RewriteAfterARemovedRowHoldsExactlyTheLiveRows()
    {
        var vanillaRows = new Dictionary<string, ulong>(StringComparer.Ordinal) { ["obj|(O)1"] = 1 };
        AnswerSet vanilla = VanillaSet(vanillaRows);
        var oldRows = new Dictionary<string, ulong>(StringComparer.Ordinal) { ["obj|" + Olive] = 3, ["obj|(O)ModA.Retired"] = 9 };
        LayerAnswers existing = Ans(new Dictionary<string, AnswerVariant>(StringComparer.Ordinal)
            { [AnswerSet.BaseVariant] = Variant((Olive, "old")) }, oldRows, A);
        // The mod's update dropped the Retired item.
        var liveRows = new Dictionary<string, ulong>(vanillaRows, StringComparer.Ordinal) { ["obj|" + Olive] = 3 };

        LayerAnswers rewritten = LayerDelta.Write(A, vanilla, new[] { In(A) }, Array.Empty<LayerAnswers>(),
            AnswerSet.BaseVariant, Variant((Item, "vanilla"), (Olive, "grove")), liveRows, _ => "1.1.0", existing);
        Assert.Equal(new[] { "obj|" + Olive }, rewritten.VariantRowHashes[AnswerSet.BaseVariant].Keys);

        LayerSet set = LayerSet.Compose(vanilla, new[] { In(A) }, new[] { rewritten }, _ => { });
        Assert.Equal(liveRows.OrderBy(p => p.Key), set.Composed!.RowHashesFor(AnswerSet.BaseVariant).OrderBy(p => p.Key));
    }

    [Fact]
    public void EachVariantMatchesOnlyItsOwnLiveRows()
    {
        const string modRow = "obj|" + Olive;
        var baseRows = new Dictionary<string, ulong>(StringComparer.Ordinal) { ["obj|(O)1"] = 1, ["meta|pierre_year2"] = 100 };
        var pierreRows = new Dictionary<string, ulong>(StringComparer.Ordinal) { ["obj|(O)1"] = 1, ["meta|pierre_year2"] = 200 };
        AnswerSet vanilla = AnswerSetMerge.With(null, AnswerSet.BaseVariant, Variant((Item, "vanilla")), baseRows, Game, Mod, new[] { RowHashes.Vanilla });
        vanilla = AnswerSetMerge.With(vanilla, AnswerSet.PierreVariant, Variant((Item, "pierre")), pierreRows, Game, Mod, new[] { RowHashes.Vanilla });

        // The mod's row reads differently in each Pierre state.
        var liveBase = new Dictionary<string, ulong>(baseRows, StringComparer.Ordinal) { [modRow] = 10 };
        var livePierre = new Dictionary<string, ulong>(pierreRows, StringComparer.Ordinal) { [modRow] = 11 };
        LayerAnswers first = LayerDelta.Write(A, vanilla, new[] { In(A) }, Array.Empty<LayerAnswers>(),
            AnswerSet.BaseVariant, Variant((Item, "vanilla"), (Olive, "base")), liveBase, _ => "1.0.0", null);
        LayerAnswers both = LayerDelta.Write(A, vanilla, new[] { In(A) }, Array.Empty<LayerAnswers>(),
            AnswerSet.PierreVariant, Variant((Item, "pierre"), (Olive, "pierre")), livePierre, _ => "1.0.0", first);
        Assert.Equal(10UL, both.VariantRowHashes[AnswerSet.BaseVariant][modRow]);
        Assert.Equal(11UL, both.VariantRowHashes[AnswerSet.PierreVariant][modRow]);

        LayerSet set = LayerSet.Compose(vanilla, new[] { In(A) }, new[] { LayerFile.ReadAnswers(LayerFile.WriteAnswers(both)) }, _ => { });
        Assert.Equal(liveBase.OrderBy(p => p.Key), set.Composed!.RowHashesFor(AnswerSet.BaseVariant).OrderBy(p => p.Key));
        Assert.Equal(livePierre.OrderBy(p => p.Key), set.Composed!.RowHashesFor(AnswerSet.PierreVariant).OrderBy(p => p.Key));
        Assert.Equal("pierre", set.Composed!.Resolve(AnswerSet.PierreVariant)!.Obtain[Olive].Single().Detail);
    }

    [Fact]
    public void EditingALayerFileChangesTheCacheKeyNotTheLayers()
    {
        string text = LayerFile.WriteInputs(In(A));
        LayerInputs before = LayerFile.ReadInputs(text);
        LayerInputs after = LayerFile.ReadInputs(text.Replace("\"Notes\": \"\"", "\"Notes\": \"edited\""));
        LayerSet one = LayerSet.Compose(VanillaSet(), new[] { before }, Array.Empty<LayerAnswers>(), _ => { });
        LayerSet two = LayerSet.Compose(VanillaSet(), new[] { after }, Array.Empty<LayerAnswers>(), _ => { });

        Assert.Equal(one.Composed!.Layers, two.Composed!.Layers);
        Assert.Equal(new[] { RowHashes.Vanilla, A }, two.Composed!.Layers);
        Assert.NotEqual(one.KeyLayers, two.KeyLayers);
        Assert.StartsWith(A + "#", one.KeyLayers[1]);

        ObtainabilityInputs live = Inputs.Value;
        string KeyOf(LayerSet s) => new LexiconResolver(s.Composed, _ => null, (_, _) => { }, (_, _) => Variant())
            .Resolve(live, false, Game, Mod, s.Layers, s.KeyLayers).Key;
        Assert.NotEqual(KeyOf(one), KeyOf(two));
    }
}
