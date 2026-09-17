using System;
using System.Linq;
using TheLongestYear.Core.Intro;
using Xunit;

public class OpeningScriptTests
{
    private static string Text(string key) => $"[{key}]";

    [Fact]
    public void The_script_visits_bus_stop_farm_hall_and_farm_in_that_order_and_ends_in_bed()
    {
        string[] commands = OpeningScript.Build(Text, "tly_intro_cc_seen").Split('/');
        var locations = commands.Where(c => c.StartsWith("changeLocation ", StringComparison.Ordinal))
            .Select(c => c.Substring("changeLocation ".Length)).ToArray();
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
    public void Morris_is_gone_before_the_hall_and_lewis_is_gone_before_the_tour()
    {
        string[] commands = OpeningScript.Build(Text, "flag").Split('/');
        int hall = Array.IndexOf(commands, "changeLocation CommunityCenter");
        int tour = Array.LastIndexOf(commands, "changeLocation Farm");
        Assert.Contains(commands.Take(hall), c => c == "warp Morris -100 -100");
        Assert.DoesNotContain(commands.Skip(hall), c => c.Contains("Morris"));
        Assert.Contains(commands.Skip(hall).Take(tour - hall), c => c == "warp Lewis -100 -100");
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

    private static int CountOf(string haystack, string needle)
        => (haystack.Length - haystack.Replace(needle, "").Length) / needle.Length;
}
