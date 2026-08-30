using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ForageYieldSimulatorTests
{
    /// <summary>Game defaults: 1-4 attempts a day, at most 6 lying about at once.</summary>
    private static RawLocationForageRate Rate(string location, int min = 1, int max = 4, int atOnce = 6)
        => new(location, min, max, atOnce);

    private static RawSpawnEntry Forage(string id, Season? season, string location, double chance = 1.0)
        => new(id, season, null, location, chance);

    [Fact]
    public void One_item_alone_on_a_map_gets_every_attempt()
    {
        var result = ForageYieldSimulator.SimulateTo(
            10,
            new[] { Forage("(O)1", Season.Spring, "Beach") },
            new[] { Rate("Beach") });

        // Mean attempts 2.5 a day, one candidate, Chance 1.0 -> 2.5 * 10 days.
        Assert.Equal(25.0, result["(O)1"].ExpectedTotal, 3);
        Assert.Equal(10, result["(O)1"].SpawningDays);
    }

    [Fact]
    public void Competing_forage_splits_the_same_attempts()
    {
        var spawns = Enumerable.Range(1, 5)
            .Select(i => Forage($"(O){i}", Season.Spring, "Beach"))
            .ToList();

        var result = ForageYieldSimulator.SimulateTo(10, spawns, new[] { Rate("Beach") });

        // 2.5 attempts split five ways = 0.5 each a day.
        Assert.Equal(5.0, result["(O)1"].ExpectedTotal, 3);
        Assert.All(result.Values, r => Assert.Equal(5.0, r.ExpectedTotal, 3));
    }

    [Fact]
    public void Chance_scales_the_yield_down()
    {
        var full = ForageYieldSimulator.SimulateTo(
            10, new[] { Forage("(O)1", Season.Spring, "Beach", 1.0) }, new[] { Rate("Beach") });
        var quarter = ForageYieldSimulator.SimulateTo(
            10, new[] { Forage("(O)1", Season.Spring, "Beach", 0.25) }, new[] { Rate("Beach") });

        Assert.Equal(full["(O)1"].ExpectedTotal * 0.25, quarter["(O)1"].ExpectedTotal, 3);
    }

    [Fact]
    public void Out_of_season_forage_never_accumulates()
    {
        // Whole of Spring; a Summer-only item cannot appear.
        var result = ForageYieldSimulator.SimulateTo(
            Calendar.DaysPerMonth,
            new[] { Forage("(O)1", Season.Summer, "Beach") },
            new[] { Rate("Beach") });

        Assert.False(result.ContainsKey("(O)1"));
    }

    [Fact]
    public void Seasonless_forage_accumulates_all_year()
    {
        var result = ForageYieldSimulator.SimulateTo(
            Calendar.DaysPerYear, new[] { Forage("(O)1", null, "Beach") }, new[] { Rate("Beach") });

        Assert.Equal(Calendar.DaysPerYear, result["(O)1"].SpawningDays);
    }

    /// <summary>The Secret Woods needs the Steel Axe (LocationGating week 4), so its forage cannot
    /// be banked from day 1 - the reason the simulator consults the gate at all.</summary>
    [Fact]
    public void A_gated_map_contributes_nothing_before_its_week()
    {
        var spawns = new[] { Forage("(O)1", null, "Woods") };
        var rates = new[] { Rate("Woods") };

        // Week 4 starts on day 22, so a day-21 cutoff is still entirely before the gate.
        Assert.False(ForageYieldSimulator.SimulateTo(21, spawns, rates).ContainsKey("(O)1"));
        Assert.True(ForageYieldSimulator.SimulateTo(28, spawns, rates).ContainsKey("(O)1"));
    }

    [Fact]
    public void A_map_with_no_rate_row_is_skipped()
    {
        var result = ForageYieldSimulator.SimulateTo(
            10, new[] { Forage("(O)1", null, "Beach") }, Array.Empty<RawLocationForageRate>());

        Assert.Empty(result);
    }

    [Fact]
    public void Max_at_once_caps_a_greedy_map()
    {
        // Mean of 10-20 is 15, but only 6 may be on the map at a time.
        var result = ForageYieldSimulator.SimulateTo(
            1, new[] { Forage("(O)1", null, "Beach") }, new[] { Rate("Beach", 10, 20, 6) });

        Assert.Equal(6.0, result["(O)1"].ExpectedTotal, 3);
    }

    [Fact]
    public void Yield_accumulates_across_the_maps_an_item_appears_on()
    {
        var result = ForageYieldSimulator.SimulateTo(
            10,
            new[] { Forage("(O)1", null, "Beach"), Forage("(O)1", null, "Mountain") },
            new[] { Rate("Beach"), Rate("Mountain") });

        Assert.Equal(50.0, result["(O)1"].ExpectedTotal, 3);
        Assert.Equal(new[] { "Beach", "Mountain" }, result["(O)1"].Locations);
    }

    /// <summary>Nijah's report (2026-08-30): a Summer Foraging bundle asked for 95 Rainbow Shells.
    /// Modelled on the real shape of the beach - Rainbow Shell competing with the other Summer
    /// beach forage over Summer's 28 days - the honest ceiling is nowhere near that, which is the
    /// whole reason the quantity clamp exists.</summary>
    [Fact]
    public void Rainbow_shell_over_one_summer_is_far_short_of_ninety_five()
    {
        string[] summerBeach = { "(O)394", "(O)372", "(O)718", "(O)719", "(O)723", "(O)397" };
        var spawns = summerBeach.Select(id => Forage(id, Season.Summer, "Beach")).ToList();

        // Summer is days 29-56; simulate the whole year so Summer is fully covered.
        var result = ForageYieldSimulator.SimulateTo(Calendar.DaysPerYear, spawns, new[] { Rate("Beach") });

        double rainbowShell = result["(O)394"].ExpectedTotal;

        Assert.Equal(Calendar.DaysPerMonth, result["(O)394"].SpawningDays);
        Assert.True(rainbowShell < 95,
            $"a 95-shell ask must exceed the simulated ceiling; simulated {rainbowShell:F1}");
        // 2.5 attempts / 6 competing types * 28 days ~= 11.7.
        Assert.InRange(rainbowShell, 10.0, 13.0);
    }

    [Fact]
    public void Day_out_of_range_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ForageYieldSimulator.SimulateTo(0, Array.Empty<RawSpawnEntry>(), Array.Empty<RawLocationForageRate>()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ForageYieldSimulator.SimulateTo(Calendar.DaysPerYear + 1, Array.Empty<RawSpawnEntry>(), Array.Empty<RawLocationForageRate>()));
    }
}
