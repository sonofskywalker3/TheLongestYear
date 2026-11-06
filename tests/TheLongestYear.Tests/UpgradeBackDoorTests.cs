using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Deep Woods adds a stop to the minecart network, and its first level leads into the Secret
/// Woods. In a year-1 loop the minecarts run from day 1 only for a save that owns Keep Minecarts, so
/// that back door is open on some saves and shut on others (Jeff, 2026-11-06: yes, open both in week 1
/// for those saves). A back door with WhenUpgrade gives such a save its own variant of the answers.</summary>
public class UpgradeBackDoorTests
{
    private const string Woods = MapWeekGate.SecretWoodsMap;
    private const string Carts = "keep_minecarts";
    private const string DeepWoods = "maxmakesmods.deepwoodsmod";
    private const string Game = "1.6.15";
    private const string Mod = "1.1.0";
    private const string Item = "(O)257";
    private static readonly Lazy<ObtainabilityInputs> Inputs = new(VanillaInputsFixture.Load);

    private const string Layer = "{\"UniqueId\": \"" + DeepWoods + "\", \"CheckedVersion\": \"1.0.0\", "
        + "\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 1, \"From\": \"DeepWoods\", \"WhenUpgrade\": \"" + Carts + "\"}]}";

    private static YearTwoMarks Marks()
        => LayerSet.Compose(null, new[] { LayerFile.ReadInputs(Layer) }, Array.Empty<LayerAnswers>(), _ => { }).Marks;

    private static AnswerVariant Variant(string detail)
    {
        var obtain = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
        {
            [Item] = new[] { new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail) },
        };
        return new AnswerVariant(obtain, obtain, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
    }

    private static AnswerSet Vanilla()
        => AnswerSetMerge.With(
            new AnswerSet(Game, Mod, new[] { RowHashes.Vanilla }, new Dictionary<string, ulong> { ["obj|(O)1"] = 1 },
                new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant("vanilla") }),
            AnswerSet.PierreVariant, Variant("vanilla pierre"), new Dictionary<string, ulong> { ["obj|(O)1"] = 1, ["meta|pierre_year2"] = 2 },
            Game, Mod, new[] { RowHashes.Vanilla });

    private static LayerAnswers Answers(params (string Variant, string Detail)[] deltas)
        => new(new[] { DeepWoods }, DeepWoods + "@1.0.0",
            deltas.ToDictionary(d => d.Variant, d => (IReadOnlyDictionary<string, ulong>)new Dictionary<string, ulong> { ["row|" + d.Variant] = 7 }, StringComparer.Ordinal),
            deltas.ToDictionary(d => d.Variant, d => Variant(d.Detail), StringComparer.Ordinal));

    private static string DetailOf(AnswerSet set, string variant) => set.Resolve(variant)!.Obtain[Item].Single().Detail;

    [Fact]
    public void A_variant_is_named_by_its_stem_and_the_door_upgrades_owned()
    {
        Assert.Equal("base", AnswerSet.VariantName(false));
        Assert.Equal("base", AnswerSet.VariantName(false, Array.Empty<string>()));
        Assert.Equal("pierre_year2_seeds", AnswerSet.VariantName(true));
        Assert.Equal("base+keep_minecarts", AnswerSet.VariantName(false, new[] { Carts }));
        Assert.Equal("pierre_year2_seeds+a+keep_minecarts", AnswerSet.VariantName(true, new[] { Carts, "a" }));
        Assert.Equal("pierre_year2_seeds", AnswerSet.StemOf("pierre_year2_seeds+a+keep_minecarts"));
        Assert.Equal("base", AnswerSet.StemOf("base"));
    }

    [Fact]
    public void An_upgrade_back_door_is_shut_until_the_save_owns_the_upgrade()
    {
        YearTwoMarks marks = Marks();
        Assert.Empty(marks.OpenedEarly);
        Assert.Single(marks.UpgradeDoors);

        Assert.Empty(marks.DoorUpgrades(_ => false));
        Assert.Same(marks, marks.For(_ => false));

        Assert.Equal(new[] { Carts }, marks.DoorUpgrades(id => id == Carts));
        Assert.Equal(1, marks.For(id => id == Carts).OpenedEarly[Woods]);
        // The marks themselves are not changed for the next save.
        Assert.Empty(marks.OpenedEarly);
    }

    [Fact]
    public void A_door_upgrade_is_one_more_data_row()
    {
        IReadOnlyDictionary<string, ulong> plain = RowHashes.Of(Inputs.Value, false);
        IReadOnlyDictionary<string, ulong> none = RowHashes.Of(Inputs.Value, false, Array.Empty<string>());
        IReadOnlyDictionary<string, ulong> carts = RowHashes.Of(Inputs.Value, false, new[] { Carts });
        Assert.Equal(plain.Count, none.Count);
        Assert.Equal(plain.Count + 1, carts.Count);
        string added = carts.Keys.Except(plain.Keys).Single();
        Assert.Contains(Carts, added);
        Assert.Equal(RowHashes.Vanilla, RowHashes.Owner(added));
    }

    [Fact]
    public void A_layer_that_wrote_the_upgrade_variant_serves_it_over_vanillas_stem()
    {
        LayerInputs inputs = LayerFile.ReadInputs(Layer);
        LayerAnswers answers = Answers(("base", "woods week 4"), ("pierre_year2_seeds", "woods week 4 pierre"),
            ("base+keep_minecarts", "woods week 1"), ("pierre_year2_seeds+keep_minecarts", "woods week 1 pierre"));
        AnswerSet composed = LayerSet.Compose(Vanilla(), new[] { inputs }, new[] { answers }, _ => { }).Composed!;

        Assert.Equal("woods week 4", DetailOf(composed, "base"));
        Assert.Equal("woods week 1", DetailOf(composed, "base+keep_minecarts"));
        Assert.Equal("woods week 1 pierre", DetailOf(composed, "pierre_year2_seeds+keep_minecarts"));
        // Its rows are vanilla's for the stem plus the layer's own for the variant.
        IReadOnlyDictionary<string, ulong> rows = composed.RowHashesFor("pierre_year2_seeds+keep_minecarts");
        Assert.True(rows.ContainsKey("meta|pierre_year2"));
        Assert.True(rows.ContainsKey("row|pierre_year2_seeds+keep_minecarts"));
        Assert.False(rows.ContainsKey("row|pierre_year2_seeds"));
    }

    [Fact]
    public void A_layer_that_did_not_write_it_leaves_those_saves_without_shipped_answers()
    {
        LayerInputs inputs = LayerFile.ReadInputs(Layer);
        AnswerSet composed = LayerSet.Compose(Vanilla(), new[] { inputs }, new[] { Answers(("base", "woods week 4")) }, _ => { }).Composed!;
        Assert.NotNull(composed.Resolve("base"));
        Assert.Null(composed.Resolve("base+keep_minecarts"));
    }

    [Fact]
    public void A_save_with_the_upgrade_is_not_served_the_stems_answers()
    {
        var rows = RowHashes.Of(Inputs.Value, false);
        var shipped = new AnswerSet(Game, Mod, new[] { RowHashes.Vanilla }, rows,
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant("shipped") });
        int builds = 0;
        var resolver = new LexiconResolver(shipped, _ => null, (_, _) => { }, _ => { builds++; return Variant("built"); });

        Assert.Equal(AnswerOrigin.Shipped, resolver.Resolve(Inputs.Value, false, Game, Mod, new[] { RowHashes.Vanilla }).Origin);
        Resolved carts = resolver.Resolve(Inputs.Value, false, Game, Mod, new[] { RowHashes.Vanilla }, doorUpgrades: new[] { Carts });
        Assert.Equal(AnswerOrigin.Computed, carts.Origin);
        Assert.Equal(1, builds);
    }

    [Fact]
    public void Writing_the_upgrade_variant_measures_it_against_vanillas_stem()
    {
        LayerInputs inputs = LayerFile.ReadInputs(Layer);
        var liveRows = new Dictionary<string, ulong> { ["obj|(O)1"] = 1, ["meta|door|" + Carts] = 9 };
        LayerAnswers written = LayerDelta.Write(DeepWoods, Vanilla(), new[] { inputs }, Array.Empty<LayerAnswers>(),
            "base+keep_minecarts", Variant("woods week 1"), liveRows, _ => "1.0.0", null);
        Assert.Equal("woods week 1", written.Deltas["base+keep_minecarts"].Obtain[Item].Single().Detail);
        Assert.Equal(new[] { "meta|door|" + Carts }, written.VariantRowHashes["base+keep_minecarts"].Keys);
    }

    [Theory]
    [InlineData("\"WhenUpgrade\": \"\"")]
    [InlineData("\"WhenUpgrade\": \"a+b\"")]
    public void A_malformed_upgrade_is_refused(string member)
        => Assert.Throws<JsonException>(() => LayerFile.ReadInputs(
            "{\"UniqueId\": \"ModA\", \"CheckedVersion\": \"1.0.0\", \"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 1, \"From\": \"X\", " + member + "}]}"));

    [Fact]
    public void The_shipped_deep_woods_layer_names_the_minecart_back_door()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "lexicon", "layers", DeepWoods + LayerFile.InputsSuffix);
        MapBackDoor door = Assert.Single(LayerFile.ReadInputs(System.IO.File.ReadAllText(path)).MapBackDoors);
        Assert.Equal((Woods, 1, Carts), (door.Map, door.Week, door.WhenUpgrade));
        Assert.Equal(GiftLadder.KeepMinecartsId, door.WhenUpgrade);
    }
}
