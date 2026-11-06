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

/// <summary>Seven Deadly Sins opens two tiles on the Forest's west edge, south of the large log, into
/// its own woods, and those woods lead into the Secret Woods: with the mod the Secret Woods needs no
/// steel axe (2026-11-06, Jeff: "if secret woods are open earlier, we can ask for things earlier").
/// A layer names such a back door and the gated map opens at its week everywhere the gate is read.
/// The mod leaves the way out when Stardew Valley Expanded is installed, so the back door names it.</summary>
public class LayerBackDoorTests
{
    private const string Woods = MapWeekGate.SecretWoodsMap;
    private const string ModWoods = "Custom_Woods1";
    private const string Sve = "FlashShifter.StardewValleyExpandedCP";
    private const string Sds = "ricehit.SevenDeadlySinsCP";

    private static readonly RawLocationLink[] Doors =
    {
        new("Farm", "Forest"), new("Forest", Woods), new("Forest", ModWoods), new(ModWoods, Woods),
        new("Farm", "BusStop"), new("BusStop", "Town"), new("Town", "Sewer"),
    };

    private static readonly IReadOnlyDictionary<string, int> Open = new Dictionary<string, int> { [Woods] = 1 };

    private static ObtainSource Fish(params string[] requires)
        => new(SourceKind.Fish, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { Requires = requires }, "test");

    private const string Head = "{\"UniqueId\": \"" + Sds + "\", \"CheckedVersion\": \"1.0.0\", ";
    private const string BackDoor = "\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 1, \"From\": \"" + ModWoods + "\", \"UnlessMods\": [\"" + Sve + "\"]}]}";

    [Fact]
    public void The_walk_alone_keeps_the_woods_at_their_own_week_whatever_leads_in()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false);
        Assert.True(walk.TryGet(Woods, out PlaceWeek week));
        Assert.Equal(new PlaceWeek(MapWeekGate.WoodsOpeningWeek, MapWeekGate.WoodsOpeningWeek, MapWeekGate.WoodsOpeningWeek), week);
        Assert.Empty(walk.OpenedEarly);
    }

    [Fact]
    public void A_back_door_opens_the_woods_at_its_week_in_every_column()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false, openedEarly: Open);
        Assert.True(walk.TryGet(Woods, out PlaceWeek week));
        Assert.Equal(new PlaceWeek(1, 1, 1), week);
        Assert.Equal(new PlaceWeek(1, 1, 1), Assert.Single(walk.OpenedEarly).Value);
        // No other gated map moves.
        Assert.True(walk.TryGet("Sewer", out PlaceWeek sewer));
        Assert.Equal(MapWeekGate.SewerOpeningWeek, sewer.Week);
    }

    [Fact]
    public void A_back_door_later_than_the_gate_changes_nothing()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false, openedEarly: new Dictionary<string, int> { [Woods] = 9 });
        Assert.True(walk.TryGet(Woods, out PlaceWeek week));
        Assert.Equal(MapWeekGate.WoodsOpeningWeek, week.Week);
        Assert.Empty(walk.OpenedEarly);
    }

    [Fact]
    public void A_layer_week_on_the_same_map_still_holds_it_later()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false,
            openingWeeks: new Dictionary<string, int> { [Woods] = 3 }, openedEarly: Open);
        Assert.True(walk.TryGet(Woods, out PlaceWeek week));
        Assert.Equal(3, week.Week);
    }

    [Fact]
    public void A_recorded_walk_gives_the_early_opening_back()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false, openedEarly: Open);
        LocationWeeks again = LocationWeeks.FromReached(walk.Reached);
        Assert.Equal(new PlaceWeek(1, 1, 1), again.OpenedEarly[Woods]);
    }

    [Fact]
    public void The_gate_read_by_name_follows_the_walk()
    {
        IReadOnlyDictionary<string, PlaceWeek> opened = LocationWeeks.Build(Doors, _ => false, openedEarly: Open).OpenedEarly;
        Assert.Equal(MapWeekGate.WoodsOpeningWeek, LocationGating.WeekFor(Woods));
        Assert.Equal(1, LocationGating.WeekFor(Woods, opened));
        Assert.Equal(1, LocationGating.HardWeekFor(Woods, WeekMode.HardAll, opened));
        Assert.Equal(1, LocationGating.WeekForAny(new[] { Woods }, opened));
        Assert.Equal(1, LocationGating.HardWeekForAny(new[] { Woods }, WeekMode.HardGates, opened));
        Assert.Equal(LocationGating.WeekFor("Sewer"), LocationGating.WeekFor("Sewer", opened));
        // "Backwoods" is not the Secret Woods and never was gated.
        Assert.Equal(1, LocationGating.WeekFor("Backwoods", opened));
    }

    [Fact]
    public void A_fish_read_by_map_name_opens_with_the_back_door()
    {
        var woodskip = new PoolItem("(O)734", 75, 1, Array.Empty<Season>(), new[] { Woods });
        IReadOnlyDictionary<string, PlaceWeek> opened = LocationWeeks.Build(Doors, _ => false, openedEarly: Open).OpenedEarly;
        Assert.Equal(MapWeekGate.WoodsOpeningWeek, FishAvailability.Derive(woodskip, null).EarliestWeek);
        ItemAvailability early = FishAvailability.Derive(woodskip, null, opened: opened);
        Assert.Equal(1, early.EarliestWeek);
        Assert.Equal(1, early.HardWeek);
    }

    [Fact]
    public void A_route_from_the_woods_lands_from_day_one_and_deep_woods_follows()
    {
        Assert.Equal(MapWeekGate.WoodsOpeningDay, MapWeekGate.Apply(Fish("location:Woods")).Lands.Lands(1));
        Assert.Equal(1, MapWeekGate.Apply(Fish("location:Woods"), openedEarly: Open).Lands.Lands(1));
        Assert.Equal(1, MapWeekGate.Apply(Fish("location:" + MapWeekGate.DeepWoodsMap), openedEarly: Open).Lands.Lands(1));
        // The Sewer is not named by the back door.
        Assert.Equal(MapWeekGate.FirstDayOf(MapWeekGate.SewerOpeningWeek),
            MapWeekGate.Apply(Fish("location:Sewer"), openedEarly: Open).Lands.Lands(1));
        // A layer week on the map still counts after the back door.
        Assert.Equal(MapWeekGate.FirstDayOf(2),
            MapWeekGate.Apply(Fish("location:Woods"), new Dictionary<string, int> { [Woods] = 2 }, Open).Lands.Lands(1));
    }

    [Fact]
    public void An_inputs_file_carries_a_back_door_into_the_marks()
    {
        LayerInputs read = LayerFile.ReadInputs(Head + BackDoor);
        MapBackDoor door = Assert.Single(read.MapBackDoors);
        Assert.Equal((Woods, 1, ModWoods), (door.Map, door.Week, door.From));
        Assert.Equal(Sve, Assert.Single(door.UnlessMods!));

        LayerSet set = LayerSet.Compose(null, new[] { read }, Array.Empty<LayerAnswers>(), _ => { });
        Assert.Equal(1, set.Marks.OpenedEarly[Woods]);

        LayerInputs back = LayerFile.ReadInputs(LayerFile.WriteInputs(read));
        Assert.Equal(door.Map, Assert.Single(back.MapBackDoors).Map);
        Assert.Empty(LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", \"CheckedVersion\": \"1.0.0\"}").MapBackDoors);
        Assert.Empty(YearTwoMarks.None.OpenedEarly);
    }

    [Fact]
    public void A_mod_that_closes_the_back_door_drops_it_when_installed()
    {
        LayerInputs read = LayerFile.ReadInputs(Head + BackDoor);
        var (alone, _) = LayerSet.Active(new[] { read }, Array.Empty<LayerAnswers>(), id => id == Sds, _ => "1.0.0", _ => { });
        Assert.Single(Assert.Single(alone).MapBackDoors);

        var (withSve, _) = LayerSet.Active(new[] { read }, Array.Empty<LayerAnswers>(), id => id == Sds || id == Sve, _ => "1.0.0", _ => { });
        Assert.Empty(Assert.Single(withSve).MapBackDoors);
        Assert.Empty(LayerSet.Compose(null, withSve, Array.Empty<LayerAnswers>(), _ => { }).Marks.OpenedEarly);
    }

    [Theory]
    [InlineData("\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 0, \"From\": \"Custom_X\"}]")]
    [InlineData("\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 17, \"From\": \"Custom_X\"}]")]
    [InlineData("\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 1}]")]
    [InlineData("\"MapBackDoors\": [{\"Week\": 1, \"From\": \"Custom_X\"}]")]
    [InlineData("\"MapBackDoors\": [null]")]
    [InlineData("\"MapBackDoors\": [{\"Map\": \"Woods\", \"Week\": 1, \"From\": \"Custom_X\", \"UnlessMods\": [\"\"]}]")]
    public void A_malformed_back_door_is_refused(string body)
        => Assert.Throws<JsonException>(() => LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", \"CheckedVersion\": \"1.0.0\", " + body + "}"));

    [Fact]
    public void The_shipped_layer_names_the_back_door_into_the_secret_woods()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "lexicon", "layers", Sds + LayerFile.InputsSuffix);
        LayerInputs layer = LayerFile.ReadInputs(System.IO.File.ReadAllText(path));
        MapBackDoor door = Assert.Single(layer.MapBackDoors);
        Assert.Equal((Woods, 1, ModWoods), (door.Map, door.Week, door.From));
        Assert.Contains(Sve, door.UnlessMods!);
    }
}
