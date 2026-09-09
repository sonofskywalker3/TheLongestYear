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
    // Quest-completion scenes (Nexus bug 1130863): Jodi's bass dinner (SamHouse 94) and Marnie's
    // cave carrot (AnimalShop 92) finish a quest the reset re-grants, so they must replay too.
    [InlineData("50s/6 15/farmer 4 19 0 Sam 17 13 0 Jodi 7 4 0/removeItem 136/removeQuest 22/speed Jodi 4/end", "removeQuest")]
    [InlineData("jaunty/13 17/farmer 13 19 0 Marnie 13 14 2/move farmer 0 -1 0/removeQuest 21/removeItem 78/friendship Marnie 100/end", "removeQuest")]
    // Gunther's farm visit (Farm 66) hands over the Rusty Key with its own command, not mail.
    [InlineData("continue/64 15/farmer 64 16 2 Gunther 64 18 0/broadcastEvent/rustyKey/skippable/pause 1500/end", "rustyKey")]
    // Willy's Copper Pan (Mountain 404798, gated on the ccFishTank mail the reset re-sends) and
    // the 14-heart world-state scenes (Sebastian's frog 9333220, Shane's saloon room 3917586).
    [InlineData("none/-1000 -1000/farmer 13 5 1 Willy 14 4 2/skippable/awardFestivalPrize Pan/speak Willy \"...\"/end", "awardFestivalPrize")]
    [InlineData("none/-1000 -1000/farmer 5 5 1/addWorldState sebastianFrog/end", "addWorldState")]
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
        Assert.True(EventGatingTables.ScriptGrantsUnlock("speak Caroline \"tea\"/mail CarolineTea/end"));
        Assert.True(EventGatingTables.ScriptGrantsUnlock("mailToday someLetter/end"));
        Assert.False(EventGatingTables.ScriptGrantsUnlock("speak Lewis \"hi\"/end"));
    }

    [Fact]
    public void CollectReplayableIds_flags_grants_excludes_narrative_and_unions_base()
    {
        var events = new (string id, string script)[]
        {
            ("100", "speak Lewis \"hi\"/end"),              // narrative → not flagged
            ("200", "addMailReceived guildMember/end"),     // grant → flagged
            ("300", "addCraftingRecipe Furnace/end"),       // grant → flagged
            ("191393", "addMailReceived ccDone/end"),       // grant BUT excluded → dropped
        };
        var baseIds = new[] { "992553", "65" };             // vanilla furnace/cave, always replayable
        var exclude = new System.Collections.Generic.HashSet<string> { "191393" };

        var result = EventGatingTables.CollectReplayableIds(events, baseIds, exclude);

        Assert.Contains("200", result);
        Assert.Contains("300", result);
        Assert.Contains("992553", result);
        Assert.Contains("65", result);
        Assert.DoesNotContain("100", result);     // narrative not flagged
        Assert.DoesNotContain("191393", result);  // excluded even though it grants
    }

    [Fact]
    public void PropagateChains_flags_a_scene_gated_on_a_replayable_scene()
    {
        // Sam's 14-heart chain: 3918600 is friendship-gated (a chain root, replays each loop);
        // 3918601..3 are "e <previous>" only and grant nothing until the boombox at the end.
        var keys = new (string id, string key)[]
        {
            ("3918600", "3918600/f Sam 3500/O Sam/t 610 1700/p Sam/L"),
            ("3918601", "3918601/e 3918600/O Sam/t 610 1700/A samJob1/p Sam/L"),
            ("3918602", "3918602/e 3918601/O Sam/t 610 1700/A samJob2/p Sam/L"),
            ("3918603", "3918603/e 3918602/O Sam/t 610 1700/A samJob3/p Sam/L"),
            ("777", "777/e 3918603/e 191393"),          // chains onto the chain AND a suppressed id
            ("191394", "191394/e 191393"),               // chains only onto a suppressed id: not flagged
            ("100", "100/t 600 1200"),                   // no chain
        };
        var flagged = new System.Collections.Generic.HashSet<string>();
        var roots = new System.Collections.Generic.HashSet<string> { "3918600" };
        var exclude = new System.Collections.Generic.HashSet<string> { "191393", "3918600" };

        EventGatingTables.PropagateChains(keys, flagged, roots, exclude);

        Assert.Contains("3918601", flagged);
        Assert.Contains("3918602", flagged);
        Assert.Contains("3918603", flagged);
        Assert.Contains("777", flagged);
        Assert.DoesNotContain("191394", flagged);
        Assert.DoesNotContain("100", flagged);
        Assert.DoesNotContain("3918600", flagged);      // roots are not re-added (excluded)
    }

    [Fact]
    public void PropagateChains_never_flags_an_excluded_id()
    {
        var keys = new (string id, string key)[] { ("191393", "191393/e 992553") };
        var flagged = new System.Collections.Generic.HashSet<string>();
        var roots = new System.Collections.Generic.HashSet<string> { "992553" };
        var exclude = new System.Collections.Generic.HashSet<string> { "191393" };
        EventGatingTables.PropagateChains(keys, flagged, roots, exclude);
        Assert.Empty(flagged);
    }
}
