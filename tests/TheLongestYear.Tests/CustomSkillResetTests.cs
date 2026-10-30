using System.Text.Json.Nodes;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CustomSkillResetTests
{
    [Fact]
    public void SkillsToReset_NullApi_IsEmpty()
        => Assert.Empty(CustomSkillReset.SkillsToReset(null));

    [Fact]
    public void SkillsToReset_KeepsEverySkillOnceInOrder_DropsBlanks()
    {
        var skills = CustomSkillReset.SkillsToReset(new[] { "moonslime.Luck", "", "spacechase0.Cooking", "moonslime.Luck", " ", "moonslime.Archaeology" });
        Assert.Equal(new[] { "moonslime.Luck", "spacechase0.Cooking", "moonslime.Archaeology" }, skills);
    }

    [Theory]
    [InlineData(15000, -15000)]
    [InlineData(0, 0)]
    public void AmountToZero_IsTheNegativeOfTheXp(int xp, int expected)
        => Assert.Equal(expected, CustomSkillReset.AmountToZero(xp));

    [Fact]
    public void DataKey_MatchesSmapisSaveDataKeyForSpaceCoreSkills()
        => Assert.Equal("smapi/mod-data/spacechase0.spacecore/skills", CustomSkillReset.SpaceCoreSkillsDataKey);

    [Fact]
    public void ZeroPlayer_ZeroesOnlyThatPlayer_KeepsFarmhands()
    {
        const string json = "{\"111\":{\"moonslime.Luck\":15000,\"spacechase0.Cooking\":380,\"gone.Skill\":5},\"222\":{\"moonslime.Luck\":770}}";

        string? result = CustomSkillReset.ZeroPlayerInSavedTable(json, 111, out var zeroed);

        Assert.NotNull(result);
        var table = JsonNode.Parse(result!)!.AsObject();
        Assert.Equal(0, (int)table["111"]!["moonslime.Luck"]!);
        Assert.Equal(0, (int)table["111"]!["spacechase0.Cooking"]!);
        // Skills from a mod that is no longer installed reset too ("all of them").
        Assert.Equal(0, (int)table["111"]!["gone.Skill"]!);
        Assert.Equal(770, (int)table["222"]!["moonslime.Luck"]!);
        Assert.Equal(new[] { "moonslime.Luck", "spacechase0.Cooking", "gone.Skill" }, zeroed);
    }

    [Fact]
    public void ZeroPlayer_AlreadyZero_ReturnsNull()
    {
        Assert.Null(CustomSkillReset.ZeroPlayerInSavedTable("{\"111\":{\"moonslime.Luck\":0}}", 111, out var zeroed));
        Assert.Empty(zeroed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"222\":{\"moonslime.Luck\":770}}")]
    [InlineData("{\"111\":5}")]
    public void ZeroPlayer_NoEntryOrUnexpectedShape_ReturnsNull(string? json)
        => Assert.Null(CustomSkillReset.ZeroPlayerInSavedTable(json, 111, out _));

    [Fact]
    public void ZeroPlayer_NegativePlayerId_RoundTrips()
    {
        string? result = CustomSkillReset.ZeroPlayerInSavedTable("{\"-42\":{\"a\":10}}", -42, out var zeroed);
        Assert.Equal("{\"-42\":{\"a\":0}}", result);
        Assert.Single(zeroed);
    }
}
