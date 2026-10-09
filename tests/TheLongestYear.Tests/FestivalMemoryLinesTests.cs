using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class FestivalMemoryLinesTests
{
    private readonly I18nFixture _fixture;
    public FestivalMemoryLinesTests(I18nFixture fixture) => _fixture = fixture;

    private System.Collections.Generic.List<string> Keys => _fixture.Map.Keys.ToList();

    [Fact]
    public void All72ApprovedLinesArePresent()
        => Assert.Equal(72, FestivalMemoryLines.AllKeys(Keys).Count());

    [Fact]
    public void OwnPool_ElseFallback()
    {
        Assert.Equal(new[] { "festmem.egghunt.won.abigail.1" },
            FestivalMemoryLines.KeysFor(FestivalMemoryKeys.EggHuntWon, "Abigail", false, Keys));
        Assert.Equal(new[] { "festmem.egghunt.won.default.1" },
            FestivalMemoryLines.KeysFor(FestivalMemoryKeys.EggHuntWon, "Pam", false, Keys));
    }

    [Fact]
    public void AfterPool_IsSeparate_AndHasNoFallbackForOtherVillagers()
    {
        Assert.Equal(new[] { "festmem.egghunt.won.abigail.after.1" },
            FestivalMemoryLines.KeysFor(FestivalMemoryKeys.EggHuntWon, "Abigail", true, Keys));
        Assert.Empty(FestivalMemoryLines.KeysFor(FestivalMemoryKeys.EggHuntWon, "Pam", true, Keys));
        Assert.Equal(new[] { "festmem.winterstar.again.default.after.1" },
            FestivalMemoryLines.KeysFor(FestivalMemoryKeys.WinterStarAgain, "Gus", true, Keys));
    }

    [Fact]
    public void BeforePool_NeverPicksTheAfterLine()
        => Assert.DoesNotContain(FestivalMemoryLines.KeysFor(FestivalMemoryKeys.LuauBad, "Gus", false, Keys),
            k => k.Contains(".after."));

    [Theory]
    [InlineData(FestivalMemoryKeys.EggHuntWon, "abigail")]
    [InlineData(FestivalMemoryKeys.EggHuntLost, "abigail")]
    [InlineData(FestivalMemoryKeys.LuauBad, "gus")]
    [InlineData(FestivalMemoryKeys.LuauGood, "gus")]
    [InlineData(FestivalMemoryKeys.FairWon, "pierre")]
    [InlineData(FestivalMemoryKeys.FairLost, "pierre")]
    [InlineData(FestivalMemoryKeys.IceFishWon, "willy")]
    public void AfterSpeakers(string memory, string slug)
        => Assert.Equal(new[] { slug }, FestivalMemoryLines.AfterSpeakers(memory, Keys));

    [Fact]
    public void AfterSpeakers_NoneForLostIceFishing()
        => Assert.Empty(FestivalMemoryLines.AfterSpeakers(FestivalMemoryKeys.IceFishLost, Keys));

    [Fact]
    public void Item_IsSubstituted()
        => Assert.Equal("Didn't you put Garlic in my soup once? I remember the Governor's face. You've never been to a Luau.",
            FestivalMemoryLines.Pick(FestivalMemoryKeys.LuauBad, "Gus", false, Keys, _ => 0, "Garlic", null));

    [Fact]
    public void MissingItem_DropsTheLine()
        => Assert.Null(FestivalMemoryLines.Pick(FestivalMemoryKeys.LuauBad, "Gus", false, Keys, _ => 0, null, null));

    [Fact]
    public void Partner_IsSubstituted()
        => Assert.Equal("Dude, Abigail? I swear you were my partner last time. Wait, you weren't even here.",
            FestivalMemoryLines.Pick(FestivalMemoryKeys.DanceOther, "Sam", false, Keys, _ => 0, null, "Abigail"));

    [Fact]
    public void MissingPartner_DropsTheLine()
        => Assert.Null(FestivalMemoryLines.Pick(FestivalMemoryKeys.DanceOther, "Sam", false, Keys, _ => 0, null, null));

    [Fact]
    public void LineWithoutTokens_NeedsNone()
        => Assert.Equal("Weird. This feels like a rerun.",
            FestivalMemoryLines.Pick(FestivalMemoryKeys.DanceAgain, "Sebastian", false, Keys, _ => 0, null, null));

    [Fact]
    public void ModNpc_UsesFallback()
        => Assert.Equal("I swear we've danced before, but this is your first time right?",
            FestivalMemoryLines.Pick(FestivalMemoryKeys.DanceAgain, "SomeModNpc", false, Keys, _ => 0, null, null));

    [Fact]
    public void EveryMemoryHasAFallback()
    {
        string[] withFallback =
        {
            FestivalMemoryKeys.EggHuntWon, FestivalMemoryKeys.EggHuntLost, FestivalMemoryKeys.DanceAttended,
            FestivalMemoryKeys.DanceAgain, FestivalMemoryKeys.DanceOther, FestivalMemoryKeys.LuauBad,
            FestivalMemoryKeys.LuauGood, FestivalMemoryKeys.LuauShorts, FestivalMemoryKeys.JelliesAttended,
            FestivalMemoryKeys.FairWon, FestivalMemoryKeys.FairLost, FestivalMemoryKeys.FairShorts,
            FestivalMemoryKeys.SpiritsPumpkin, FestivalMemoryKeys.IceFishWon, FestivalMemoryKeys.IceFishLost,
            FestivalMemoryKeys.WinterStarSeen, FestivalMemoryKeys.WinterStarSeenLiked, FestivalMemoryKeys.WinterStarSeenDisliked,
        };
        foreach (string memory in withFallback)
            Assert.NotEmpty(FestivalMemoryLines.KeysFor(memory, "SomeModNpc", false, Keys));
    }

    [Fact]
    public void NoEmDashes()
        => Assert.DoesNotContain(FestivalMemoryLines.AllKeys(Keys), k => _fixture.Map[k].Contains('—'));
}
