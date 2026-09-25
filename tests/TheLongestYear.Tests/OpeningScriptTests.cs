using System;
using System.Linq;
using TheLongestYear.Core.Intro;
using Xunit;

namespace TheLongestYear.Tests;

public class OpeningScriptTests
{
    private static string Text(string key) => $"[{key}]";

    [Fact]
    public void The_script_visits_bus_stop_farm_hall_and_farm_in_that_order_and_ends_in_bed()
    {
        string[] commands = OpeningScript.Build(Text, "tly_intro_cc_seen").Split('/');
        var locations = commands
            .Where(c => c.StartsWith("changeLocation ", StringComparison.Ordinal) || c.StartsWith("tlyChangeLocation ", StringComparison.Ordinal))
            .Select(c => c.Split(' ')[1]).ToArray();
        Assert.Equal(new[] { "Farm", "CommunityCenter", "Farm" }, locations);   // BusStop is where 60367 starts
        Assert.Equal("end beginGame", commands[^1]);
        Assert.Equal("addMailReceived tly_intro_cc_seen", commands[^2]);
    }

    [Fact]
    public void Every_line_key_is_spoken_once_and_wrapped_in_quotes()
    {
        string script = OpeningScript.Build(Text, "flag");
        foreach (string key in OpeningScript.LineKeys)
            Assert.Equal(1, CountOf(script, $"\"[{key}]\""));
    }

    [Fact]
    public void Morris_and_Robin_stay_on_the_farm_and_Lewis_stays_in_the_hall()
    {
        string[] commands = OpeningScript.Build(Text, "flag").Split('/');
        // tlyChangeLocation clears every actor, so leaving a scene is the scene change itself.
        int hall = Array.FindIndex(commands, c => c.StartsWith("tlyChangeLocation CommunityCenter", StringComparison.Ordinal));
        int tour = Array.FindLastIndex(commands, c => c.StartsWith("tlyChangeLocation Farm", StringComparison.Ordinal));
        Assert.True(hall > 0 && tour > hall);
        Assert.DoesNotContain(commands.Skip(hall), c => c.Contains("Morris"));
        Assert.DoesNotContain(commands.Skip(hall), c => c.Contains("Robin"));
        Assert.Contains(commands.Skip(hall).Take(tour - hall), c => c.StartsWith("addTemporaryActor Lewis ", StringComparison.Ordinal));
        Assert.DoesNotContain(commands.Skip(tour), c => c.Contains("Lewis"));
    }

    [Fact]
    public void The_script_never_names_the_dark_morris_sprite_and_is_not_skippable()
    {
        string script = OpeningScript.Build(Text, "flag");
        Assert.DoesNotContain("Morris_Dark", script);
        Assert.DoesNotContain("changeSprite", script);
        Assert.DoesNotContain("/skippable/", script);
    }

    [Fact]
    public void The_farmer_never_speaks_and_asks_with_the_question_emote()
    {
        string script = OpeningScript.Build(Text, "flag");
        Assert.DoesNotContain("message ", script);
        Assert.Contains("/emote farmer 8/", script);
        Assert.DoesNotContain("farmer-ask", script);
    }

    [Fact]
    public void No_two_addTemporaryActor_commands_share_an_override_name()
    {
        // Event.getActorByName returns the FIRST actor with a given name; a second
        // addTemporaryActor reusing a name would silently animate the stale actor instead of the
        // new one (the tour's Junimo warp-reuse fix relies on this staying unique).
        string[] commands = OpeningScript.Build(Text, "flag").Split('/');
        var overrideNames = commands
            .Where(c => c.StartsWith("addTemporaryActor ", StringComparison.Ordinal))
            // addTemporaryActor <sprite> <w> <h> <x> <y> <dir> <breather> <type> [overrideName]:
            // with no override the actor is named after its sprite.
            .Select(c => c.Split(' ') is var p && p.Length > 9 ? p[9] : c.Split(' ')[1])
            .ToList();
        Assert.Equal(overrideNames.Distinct().Count(), overrideNames.Count);
    }

    private static int CountOf(string haystack, string needle)
        => (haystack.Length - haystack.Replace(needle, "").Length) / needle.Length;
}
