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

/// <summary>The year-2 sections of a layer inputs file: week/effort rulings and the maps and shops
/// whose data routes count as year 2 or Ginger Island.</summary>
public sealed class YearTwoLayerTests
{
    private const string Id = "(O)X";

    private static string Doc(string sections)
        => "{\"UniqueId\": \"ModC\", \"CheckedVersion\": \"1.0.0\"" + (sections.Length == 0 ? "" : ", " + sections) + "}";

    private static string Ruling(int week, int effort, string basis)
        => $"\"YearTwoRulings\": {{\"{Id}\": {{\"Week\": {week}, \"Effort\": {effort}, \"Basis\": \"{basis}\"}}}}";

    [Fact]
    public void ReadInputs_reads_year_two_sections()
    {
        LayerInputs read = LayerFile.ReadInputs(Doc(
            Ruling(21, 7, "b") + ", \"YearTwoMaps\": {\"Custom_Highlands\": \"GingerIsland\"}, \"YearTwoShops\": {\"Shop1\": \"YearTwo\"}"));
        Assert.Equal(new YearTwoRuling(21, 7, "b"), Assert.Single(read.YearTwoRulings).Value);
        Assert.Equal(Id, read.YearTwoRulings.Keys.Single());
        Assert.Equal(YearTwoFlag.GingerIsland, Assert.Single(read.YearTwoMaps).Value);
        Assert.Equal("Custom_Highlands", read.YearTwoMaps.Keys.Single());
        Assert.Equal(YearTwoFlag.YearTwo, Assert.Single(read.YearTwoShops).Value);
        Assert.Equal("Shop1", read.YearTwoShops.Keys.Single());
    }

    [Fact]
    public void ReadInputs_missing_year_two_sections_read_as_empty()
    {
        LayerInputs read = LayerFile.ReadInputs("{\"UniqueId\": \"ModC\"}");
        Assert.Empty(read.YearTwoRulings);
        Assert.Empty(read.YearTwoMaps);
        Assert.Empty(read.YearTwoShops);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(33)]
    public void ReadInputs_refuses_week_outside_year_two(int week)
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(week, 5, "b"))));
        Assert.Contains(Id, e.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    public void ReadInputs_refuses_effort_outside_scale(int effort)
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(20, effort, "b"))));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_blank_basis()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(20, 5, " "))));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_ruling()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc($"\"YearTwoRulings\": {{\"{Id}\": null}}")));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_map_flag()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc("\"YearTwoMaps\": {\"M\": null}")));
        Assert.Contains("M", e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_shop_flag()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc("\"YearTwoShops\": {\"S\": null}")));
        Assert.Contains("S", e.Message);
    }

    [Fact]
    public void WriteInputs_round_trips_year_two_sections()
    {
        LayerInputs read = LayerFile.ReadInputs(Doc(
            Ruling(32, 12, "b") + ", \"YearTwoMaps\": {\"M\": \"GingerIsland\"}, \"YearTwoShops\": {\"S\": \"YearTwo\"}"));
        LayerInputs back = LayerFile.ReadInputs(LayerFile.WriteInputs(read));
        Assert.Equal(read.YearTwoRulings.OrderBy(p => p.Key), back.YearTwoRulings.OrderBy(p => p.Key));
        Assert.Equal(read.YearTwoMaps.OrderBy(p => p.Key), back.YearTwoMaps.OrderBy(p => p.Key));
        Assert.Equal(read.YearTwoShops.OrderBy(p => p.Key), back.YearTwoShops.OrderBy(p => p.Key));
        Assert.Single(back.YearTwoRulings);
    }

    private const string Olive = "(O)ModA.Olive";

    private static ObtainSource Route(params string[] requires)
        => new(SourceKind.Forage, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = requires }, "route");

    private static IReadOnlyDictionary<string, YearTwoFlag> Flags(string key, YearTwoFlag flag)
        => new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal) { [key] = flag };

    private static readonly IReadOnlyDictionary<string, YearTwoFlag> NoFlags = new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal);

    [Fact]
    public void Mark_sets_island_flag_for_route_on_named_map()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), NoFlags);
        ObtainSource marked = marks.Mark(Route("location:Custom_Highlands"));
        Assert.True(marked.Conditions.GingerIsland);
        Assert.False(marked.Conditions.YearTwo);
    }

    [Fact]
    public void Mark_sets_year_two_flag_for_route_at_named_shop()
    {
        var marks = new YearTwoMarks(NoFlags, Flags("S1", YearTwoFlag.YearTwo));
        ObtainSource marked = marks.Mark(Route("shop:S1"));
        Assert.True(marked.Conditions.YearTwo);
        Assert.False(marked.Conditions.GingerIsland);
    }

    [Fact]
    public void Mark_matches_whole_name_only()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), Flags("S1", YearTwoFlag.YearTwo));
        ObtainSource cavern = Route("location:Custom_HighlandsCavern");
        ObtainSource shop = Route("shop:S10");
        Assert.Same(cavern, marks.Mark(cavern));
        Assert.Same(shop, marks.Mark(shop));
    }

    private static ObtainabilityBuild BuildOlive(YearTwoMarks? marks)
    {
        var extra = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
        {
            [Olive] = new[] { Route("location:Custom_Highlands") },
        };
        return ObtainabilityBuilder.Build(VanillaInputsFixture.Load(), extraDirect: extra, marks: marks);
    }

    [Fact]
    public void Build_flags_data_route_and_keeps_it_out_of_year_one_fold()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), NoFlags);
        ObtainabilityModel model = BuildOlive(marks).Model;
        Assert.True(model.Sources(Olive).Single().Conditions.GingerIsland);
        Assert.Null(model.Table(Olive, ObtainFilter.DependableOnly).LandingWeek(1));
        Assert.Equal(1, model.Table(Olive, ObtainFilter.DependableOnly with { IncludeGingerIsland = true }).LandingWeek(1));
    }

    [Fact]
    public void Build_without_marks_is_unchanged()
    {
        ObtainabilityModel model = BuildOlive(null).Model;
        Assert.False(model.Sources(Olive).Single().Conditions.GingerIsland);
        Assert.Equal(1, model.Table(Olive, ObtainFilter.DependableOnly).LandingWeek(1));
    }

    [Fact]
    public void Compose_merges_maps_and_shops_later_wins_with_warn()
    {
        LayerInputs a = LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", \"YearTwoMaps\": {\"M\": \"YearTwo\"}, \"YearTwoShops\": {\"S\": \"YearTwo\"}}");
        LayerInputs b = LayerFile.ReadInputs("{\"UniqueId\": \"ModB\", \"YearTwoMaps\": {\"M\": \"GingerIsland\"}}");
        var warns = new List<string>();
        LayerSet set = LayerSet.Compose(null, new[] { a, b }, Array.Empty<LayerAnswers>(), warns.Add);
        Assert.Equal(YearTwoFlag.GingerIsland, set.Marks.Maps["M"]);
        Assert.Equal(YearTwoFlag.YearTwo, set.Marks.Shops["S"]);
        string warn = Assert.Single(warns);
        Assert.Contains("ModA", warn);
        Assert.Contains("ModB", warn);
    }

    private static ItemAvailability Avail(int week, int effort)
        => new(Season.Spring, effort, "b", EffortSource.Derived, week);

    private static AnswerVariant Variant(params (string Id, ItemAvailability A)[] yearTwo)
        => new(new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal),
               new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal),
               Array.Empty<string>(), 1, new Dictionary<string, PlaceWeek>(StringComparer.Ordinal),
               Array.Empty<string>(), new Dictionary<string, PlaceWeek>(StringComparer.Ordinal))
        {
            YearTwo = yearTwo.ToDictionary(p => p.Id, p => p.A, StringComparer.Ordinal),
        };

    [Fact]
    public void LayDelta_merges_year_two_per_id_later_wins()
    {
        AnswerVariant below = Variant(("(O)A", Avail(17, 3)), (Id, Avail(18, 4)));
        AnswerVariant delta = Variant((Id, Avail(25, 9)), ("(O)B", Avail(20, 5)));
        AnswerVariant laid = LayerSet.LayDelta(below, delta);
        Assert.Equal(3, laid.YearTwo.Count);
        Assert.Equal(Avail(17, 3), laid.YearTwo["(O)A"]);
        Assert.Equal(Avail(25, 9), laid.YearTwo[Id]);
        Assert.Equal(Avail(20, 5), laid.YearTwo["(O)B"]);
    }

    [Fact]
    public void Remove_keeps_year_two()
    {
        AnswerVariant below = Variant((Id, Avail(18, 4)));
        var removal = new LayerRemovals(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(Avail(18, 4), LayerSet.Remove(below, removal).YearTwo[Id]);
    }

    [Fact]
    public void Diff_holds_only_changed_year_two_entries_and_IsEmpty_sees_them()
    {
        AnswerVariant below = Variant(("(O)A", Avail(17, 3)), (Id, Avail(18, 4)));
        Assert.True(LayerDelta.IsEmpty(LayerDelta.Diff(below, Variant(("(O)A", Avail(17, 3)), (Id, Avail(18, 4))))));
        AnswerVariant live = Variant(("(O)A", Avail(17, 3)), (Id, Avail(25, 9)), ("(O)B", Avail(20, 5)));
        AnswerVariant diff = LayerDelta.Diff(below, live);
        Assert.Equal(new[] { "(O)B", Id }, diff.YearTwo.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.False(LayerDelta.IsEmpty(diff));
    }

    [Fact]
    public void ReadAnswers_refuses_null_year_two_entry()
    {
        string json = "{\"Mods\": [\"ModC\"], \"CheckedVersions\": \"1.0.0\", \"Deltas\": {\"base\": {\"Tables\": [], \"Obtain\": {}, \"YearSort\": {}, \"Unresolved\": [], \"Passes\": 1, \"MapWeeks\": {}, \"UnreachedMaps\": [], \"ShopWeeks\": {}, \"Availability\": {}, \"CatchRates\": {}, \"YearTwo\": {\"" + Id + "\": null}}}}";
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadAnswers(json));
        Assert.Contains("YearTwo", e.Message);
    }

    [Fact]
    public void LexiconDiff_reports_year_two_difference()
    {
        var lines = LexiconDiff.Compare(Variant((Id, Avail(18, 4))), Variant((Id, Avail(25, 9))));
        string line = Assert.Single(lines);
        Assert.Contains(Id, line);
        Assert.StartsWith("year 2 ", line);
    }

    private static IReadOnlyDictionary<string, YearTwoRuling> Rule(int week, int effort = 5)
        => new Dictionary<string, YearTwoRuling> { [Id] = new(week, effort, "why") };

    private static IReadOnlyDictionary<string, AvailabilityAnswers> Avails(
        Dictionary<string, ItemAvailability>? derived = null, Dictionary<string, ItemEffort>? effort = null)
        => new Dictionary<string, AvailabilityAnswers>
        {
            ["Normal|no_kitchen"] = new(
                derived ?? new Dictionary<string, ItemAvailability>(), effort ?? new Dictionary<string, ItemEffort>(),
                new Dictionary<string, double[]>(), new Dictionary<string, int>()),
        };

    [Theory]
    [InlineData(17, Season.Spring)]
    [InlineData(21, Season.Summer)]
    [InlineData(32, Season.Winter)]
    public void Build_stores_week_effort_season_and_prefixed_basis(int week, Season season)
    {
        var warns = new List<string>();
        var table = YearTwoAnswers.Build(Rule(week, 7), Avails(), warns.Add);
        ItemAvailability a = Assert.Single(table).Value;
        Assert.Equal(week, a.EarliestWeek);
        Assert.Equal(7, a.Effort);
        Assert.Equal("year 2 ruling: why", a.Basis);
        Assert.Equal(EffortSource.Ruling, a.Source);
        Assert.Equal(season, a.EarliestSeason);
        Assert.Equal(season, a.GateSeason);
        Assert.Empty(warns);
    }

    [Fact]
    public void Build_refuses_id_with_placed_year_one_week_in_Derived()
    {
        var warns = new List<string>();
        var avail = Avails(derived: new() { [Id] = new ItemAvailability(Season.Spring, 3, "b", EffortSource.Derived, 6) });
        Assert.Empty(YearTwoAnswers.Build(Rule(20), avail, warns.Add));
        string warn = Assert.Single(warns);
        Assert.Contains(Id, warn);
        Assert.Contains("6", warn);
    }

    [Fact]
    public void Build_refuses_id_with_placed_week_in_EffortDerived()
    {
        var warns = new List<string>();
        var avail = Avails(effort: new() { [Id] = new ItemEffort(5, "b", 9) });
        Assert.Empty(YearTwoAnswers.Build(Rule(20), avail, warns.Add));
        Assert.Contains("9", Assert.Single(warns));
    }

    [Fact]
    public void Build_accepts_id_in_EffortDerived_without_a_week()
    {
        var warns = new List<string>();
        var avail = Avails(effort: new() { [Id] = new ItemEffort(5, "b", EarliestWeek: null) });
        Assert.Single(YearTwoAnswers.Build(Rule(20), avail, warns.Add));
        Assert.Empty(warns);
    }

    [Fact]
    public void Compose_year_two_rulings_later_wins_with_warn()
    {
        LayerInputs a = LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", " + Ruling(20, 3, "a") + "}");
        LayerInputs b = LayerFile.ReadInputs("{\"UniqueId\": \"ModB\", " + Ruling(25, 4, "b") + "}");
        var warns = new List<string>();
        LayerSet set = LayerSet.Compose(null, new[] { a, b }, Array.Empty<LayerAnswers>(), warns.Add);
        Assert.Equal(25, set.YearTwoRulings[Id].Week);
        string warn = Assert.Single(warns);
        Assert.Contains("ModA", warn);
        Assert.Contains("ModB", warn);
    }

    [Fact]
    public void Build_does_not_touch_year_one_availability()
    {
        var avail = Avails();
        YearTwoAnswers.Build(Rule(20), avail, _ => { });
        Assert.Empty(avail["Normal|no_kitchen"].Derived);
        Assert.Empty(avail["Normal|no_kitchen"].EffortDerived);
    }

    [Fact]
    public void Factory_leaves_year_two_table_empty_so_write_vanilla_stays_empty()
    {
        ObtainabilityInputs inputs = VanillaInputsFixture.Load();
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        AnswerVariant variant = AnswerVariantFactory.From(build, build, inputs, null, Array.Empty<string>(), null);
        Assert.Empty(variant.YearTwo);
    }

    private static AnswerSet Composed(string name, AnswerVariant variant)
        => new("1.6", "1.0.0", Array.Empty<string>(), new Dictionary<string, ulong>(),
               new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [name] = variant });

    [Fact]
    public void TableFor_serves_the_current_variant_table_when_it_has_one()
    {
        AnswerVariant current = Variant((Id, Avail(25, 9)));
        AnswerSet composed = Composed(AnswerSet.BaseVariant, Variant((Id, Avail(18, 4))));
        Assert.Equal(Avail(25, 9), YearTwoAnswers.TableFor(current, composed, AnswerSet.BaseVariant)[Id]);
    }

    [Fact]
    public void TableFor_falls_back_to_the_composed_variant_on_a_computed_load()
    {
        AnswerVariant computed = Variant();
        AnswerSet composed = Composed(AnswerSet.PierreVariant, Variant((Id, Avail(18, 4))));
        Assert.Equal(Avail(18, 4), YearTwoAnswers.TableFor(computed, composed, AnswerSet.PierreVariant)[Id]);
        Assert.Empty(YearTwoAnswers.TableFor(computed, composed, AnswerSet.BaseVariant));
    }

    [Fact]
    public void TableFor_is_empty_with_nothing_to_serve()
    {
        Assert.Empty(YearTwoAnswers.TableFor(null, null, AnswerSet.BaseVariant));
        Assert.Empty(YearTwoAnswers.TableFor(Variant(), null, AnswerSet.BaseVariant));
    }

    [Fact]
    public void Year_one_model_never_serves_a_ruled_year_two_entry()
    {
        const int ruledWeek = 25;
        var answers = Avails()["Normal|no_kitchen"];
        AnswerVariant variant = Variant((Id, new ItemAvailability(Season.Summer, 9, YearTwoAnswers.BasisPrefix + "why",
            EffortSource.Ruling, ruledWeek, Season.Summer))) with
        {
            Availability = new Dictionary<string, AvailabilityAnswers>(StringComparer.Ordinal) { ["Normal|no_kitchen"] = answers },
        };
        ItemAvailabilityModel model = ItemAvailabilityBuilder.FromAnswers(
            variant.Availability["Normal|no_kitchen"], null, null, WeekMode.Pacing, DifficultyStep.Normal);

        ItemAvailability yearOne = model.For(Id);
        Assert.NotEqual(EffortSource.Ruling, yearOne.Source);
        Assert.NotEqual(ruledWeek, yearOne.EarliestWeek);

        ItemAvailability yearTwo = YearTwoAnswers.TableFor(variant, null, AnswerSet.BaseVariant)[Id];
        Assert.Equal(EffortSource.Ruling, yearTwo.Source);
        Assert.Equal(ruledWeek, yearTwo.EarliestWeek);
    }

    [Fact]
    public void Ruled_id_with_an_effort_only_entry_keeps_its_year_one_effort_for_the_board()
    {
        const int ruledWeek = 25;
        var warns = new List<string>();
        var avail = Avails(effort: new() { [Id] = new ItemEffort(5, "b", EarliestWeek: null) });

        var table = YearTwoAnswers.Build(Rule(ruledWeek, 9), avail, warns.Add);
        Assert.Single(table);
        Assert.Empty(warns);

        ItemAvailabilityModel model = ItemAvailabilityBuilder.FromAnswers(
            avail["Normal|no_kitchen"], null, null, WeekMode.Pacing, DifficultyStep.Normal);
        ItemAvailability yearOne = model.For(Id);
        Assert.Equal(5, yearOne.Effort);
        Assert.NotEqual(EffortSource.Ruling, yearOne.Source);

        AnswerVariant variant = Variant() with { YearTwo = table };
        ItemAvailability yearTwo = YearTwoAnswers.TableFor(variant, null, AnswerSet.BaseVariant)[Id];
        Assert.Equal(EffortSource.Ruling, yearTwo.Source);
        Assert.Equal(ruledWeek, yearTwo.EarliestWeek);
    }

    [Fact]
    public void Vanilla_json_round_trip_is_byte_identical()
    {
        string? dir = AppContext.BaseDirectory;
        string? path = null;
        while (dir != null && path == null)
        {
            string candidate = System.IO.Path.Combine(dir, "src", "TheLongestYear", "assets", "lexicon", "vanilla.json");
            if (System.IO.File.Exists(candidate)) path = candidate;
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        if (path == null) return;   // not a source checkout
        string text = System.IO.File.ReadAllText(path).Replace("\r\n", "\n");   // a Windows checkout may convert endings
        Assert.Equal(text, LexiconJson.Write(LexiconJson.Read(text)));
    }

    /// <summary>The shipped Stardew Valley Expanded inputs file loads, and its year-2 rulings stay inside
    /// the year-2 calendar (a bad ruling would make the whole layer fail to load in game).</summary>
    [Fact]
    public void Shipped_sve_inputs_file_reads_with_its_year_two_sections()
    {
        string? path = RepoFile("src", "TheLongestYear", "assets", "lexicon", "layers", "FlashShifter.StardewValleyExpandedCP.inputs.json");
        if (path == null) return;   // not a source checkout
        LayerInputs read = LayerFile.ReadInputs(System.IO.File.ReadAllText(path));
        Assert.Equal("FlashShifter.StardewValleyExpandedCP", read.UniqueId);
        Assert.NotEmpty(read.YearTwoMaps);
        // The Sprite Spring is locked behind the post-Community-Center Magic Ink event, so it and
        // its cave leave the year-1 walk (Goldenfish and Meteor Carp were week 1 from its rows).
        Assert.Equal(YearTwoFlag.YearTwo, read.YearTwoMaps["Custom_SpriteSpring2"]);
        Assert.Equal(YearTwoFlag.YearTwo, read.YearTwoMaps["Custom_SpriteSpringCave"]);
        Assert.NotEmpty(read.YearTwoShops);
        Assert.NotEmpty(read.YearTwoRulings);
        Assert.All(read.YearTwoRulings.Values, r => Assert.InRange(r.Week, YearTwoWeeks.First, YearTwoWeeks.Last));
        Assert.All(read.ExtraSources.Values.SelectMany(s => s), s => Assert.False(string.IsNullOrWhiteSpace(s.Detail)));
    }

    private static string? RepoFile(params string[] parts)
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = System.IO.Path.GetDirectoryName(dir))
        {
            string candidate = System.IO.Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
