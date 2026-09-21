using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonTurnTests
{
    [Theory]
    [InlineData(Season.Summer, SeasonTurnKind.Summer)]
    [InlineData(Season.Fall, SeasonTurnKind.Fall)]
    [InlineData(Season.Winter, SeasonTurnKind.Winter)]
    public void ForSeasonStart_MapsTheThreeTurns(Season season, SeasonTurnKind expected)
        => Assert.Equal(expected, SeasonTurn.ForSeasonStart(season));

    [Fact]
    public void ForSeasonStart_SpringHasNoTurn()
        => Assert.Null(SeasonTurn.ForSeasonStart(Season.Spring));

    [Theory]
    [InlineData(SeasonTurnKind.Summer, 2)]
    [InlineData(SeasonTurnKind.Fall, 3)]
    [InlineData(SeasonTurnKind.Winter, 4)]
    public void JunimoCount_Escalates(SeasonTurnKind kind, int expected)
        => Assert.Equal(expected, SeasonTurn.JunimoCount(kind));

    [Theory]
    [InlineData(SeasonTurnKind.Summer, false)]
    [InlineData(SeasonTurnKind.Summer, true)]
    [InlineData(SeasonTurnKind.Fall, false)]
    [InlineData(SeasonTurnKind.Fall, true)]
    [InlineData(SeasonTurnKind.Winter, false)]
    [InlineData(SeasonTurnKind.Winter, true)]
    public void Lines_OnlyNameJunimosWhoArePresent(SeasonTurnKind kind, bool rewound)
    {
        var lines = SeasonTurn.Lines(kind, rewound);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.InRange(l.Junimo, 0, SeasonTurn.JunimoCount(kind) - 1));
        Assert.All(lines, l => Assert.StartsWith("event.turn." + kind.ToString().ToLowerInvariant() + "-", l.Key));
    }

    [Fact]
    public void Summer_closer_is_the_plain_one_on_a_save_never_rewound()
        => Assert.Equal("event.turn.summer-3", SeasonTurn.Lines(SeasonTurnKind.Summer, rewound: false)[2].Key);

    [Fact]
    public void Summer_closer_changes_once_the_save_has_been_rewound()
        => Assert.Equal("event.turn.summer-3-again", SeasonTurn.Lines(SeasonTurnKind.Summer, rewound: true)[2].Key);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fall_has_three_lines_one_per_junimo(bool rewound)
    {
        var lines = SeasonTurn.Lines(SeasonTurnKind.Fall, rewound);
        Assert.Equal(new[] { 0, 1, 2 }, lines.Select(l => l.Junimo).ToArray());
        Assert.Equal("event.turn.fall-3", lines[2].Key);
    }

    [Fact]
    public void Winter_keeps_four_lines_and_ignores_the_rewound_flag()
        => Assert.Equal(SeasonTurn.Lines(SeasonTurnKind.Winter, false), SeasonTurn.Lines(SeasonTurnKind.Winter, true));

    [Fact]
    public void AllLineKeys_covers_both_summer_closers_and_drops_fall_4()
    {
        Assert.Contains("event.turn.summer-3-again", SeasonTurn.AllLineKeys);
        Assert.DoesNotContain("event.turn.fall-4", SeasonTurn.AllLineKeys);
    }

    [Fact]
    public void IsSkippable_OnlyAfterTheTurnWasSeen()
    {
        var seen = new HashSet<string>();
        Assert.False(SeasonTurn.IsSkippable(SeasonTurnKind.Fall, seen));
        seen.Add(SeasonTurn.SeenName(SeasonTurnKind.Fall));
        Assert.True(SeasonTurn.IsSkippable(SeasonTurnKind.Fall, seen));
        Assert.False(SeasonTurn.IsSkippable(SeasonTurnKind.Winter, seen));
    }

    [Theory]
    [InlineData("summer", true, SeasonTurnKind.Summer)]
    [InlineData("Winter", true, SeasonTurnKind.Winter)]
    [InlineData("spring", false, SeasonTurnKind.Summer)]
    public void TryParse_AcceptsTheThreeNames(string text, bool ok, SeasonTurnKind expected)
    {
        Assert.Equal(ok, SeasonTurn.TryParse(text, out SeasonTurnKind kind));
        if (ok) Assert.Equal(expected, kind);
    }
}
