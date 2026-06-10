using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ReplayableDetectionTests
{
    [Theory]
    [InlineData("addCraftingRecipe Furnace/end", "addCraftingRecipe")]
    [InlineData("addCookingRecipe Survival Burger/end", "addCookingRecipe")]
    [InlineData("speak Marlon \"...\"/addMailReceived guildMember/end", "addMailReceived")]
    [InlineData("mailReceived guildMember/end", "mailReceived")]
    [InlineData("addQuest 16/end", "addQuest")]
    public void MatchedGrantToken_finds_the_grant_command(string script, string expected)
    {
        Assert.Equal(expected, EventGatingTables.MatchedGrantToken(script));
    }

    [Theory]
    [InlineData("speak Lewis \"Welcome\"/pause 500/warp Town 10 10/end")]
    [InlineData("playSound doorClose/move farmer 0 1 2/end")]
    [InlineData("")]
    [InlineData(null)]
    public void MatchedGrantToken_returns_null_for_pure_narrative(string script)
    {
        Assert.Null(EventGatingTables.MatchedGrantToken(script));
    }

    [Fact]
    public void MatchedGrantToken_ignores_a_token_inside_dialogue_text()
    {
        // "mailReceived" appears only inside a speak argument, not at a command-segment start.
        string script = "speak Robin \"Did you get my mailReceived note?\"/end";
        Assert.Null(EventGatingTables.MatchedGrantToken(script));
    }

    [Fact]
    public void ScriptGrantsUnlock_is_true_only_when_a_grant_command_runs()
    {
        Assert.True(EventGatingTables.ScriptGrantsUnlock("addMailReceived guildMember/end"));
        Assert.False(EventGatingTables.ScriptGrantsUnlock("speak Lewis \"hi\"/end"));
    }
}
