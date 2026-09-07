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
    [InlineData(SeasonTurnKind.Summer)]
    [InlineData(SeasonTurnKind.Fall)]
    [InlineData(SeasonTurnKind.Winter)]
    public void Lines_OnlyNameJunimosWhoArePresent(SeasonTurnKind kind)
    {
        var lines = SeasonTurn.Lines(kind);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.InRange(l.Junimo, 0, SeasonTurn.JunimoCount(kind) - 1));
        Assert.All(lines, l => Assert.StartsWith("event.turn." + kind.ToString().ToLowerInvariant() + "-", l.Key));
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
