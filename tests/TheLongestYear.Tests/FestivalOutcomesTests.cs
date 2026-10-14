using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FestivalOutcomesTests
{
    [Theory]
    [InlineData(0, FestivalOutcome.LuauToxic)]
    [InlineData(1, FestivalOutcome.LuauBad)]
    [InlineData(2, FestivalOutcome.LuauOkay)]
    [InlineData(3, FestivalOutcome.LuauGood)]
    [InlineData(4, FestivalOutcome.LuauBest)]
    [InlineData(5, FestivalOutcome.LuauEmpty)]
    [InlineData(6, FestivalOutcome.LuauShorts)]
    public void GovernorLevel_MapsToOutcome(int level, string expected)
        => Assert.Equal(expected, FestivalOutcomes.ClassifyLuau(level));

    [Fact]
    public void GovernorLevel_OutOfRange_IsEmpty()
        => Assert.Equal(FestivalOutcome.LuauEmpty, FestivalOutcomes.ClassifyLuau(9));

    [Theory]
    [InlineData(95, false, FestivalOutcome.GrangeFirst)]
    [InlineData(90, false, FestivalOutcome.GrangeFirst)]
    [InlineData(89, false, FestivalOutcome.GrangeSecond)]
    [InlineData(75, false, FestivalOutcome.GrangeSecond)]
    [InlineData(74, false, FestivalOutcome.GrangeThird)]
    [InlineData(60, false, FestivalOutcome.GrangeThird)]
    [InlineData(59, false, FestivalOutcome.GrangeLost)]
    [InlineData(-666, false, FestivalOutcome.GrangeShorts)]
    [InlineData(10, true, FestivalOutcome.GrangeNone)]
    [InlineData(-666, true, FestivalOutcome.GrangeShorts)]
    public void Grange_ScoreBands(int score, bool emptyDisplay, string expected)
        => Assert.Equal(expected, FestivalOutcomes.ClassifyGrange(score, emptyDisplay));

    [Theory]
    [InlineData(9, FestivalOutcome.EggWon)]
    [InlineData(12, FestivalOutcome.EggWon)]
    [InlineData(8, FestivalOutcome.EggLost)]
    [InlineData(0, FestivalOutcome.EggLost)]
    public void EggHunt_WinsAtNine(int eggs, string expected)
        => Assert.Equal(expected, FestivalOutcomes.ClassifyEggHunt(eggs));

    [Theory]
    [InlineData(5, FestivalOutcome.IceWon)]
    [InlineData(4, FestivalOutcome.IceLost)]
    public void IceFishing_WinsAtFive(int fish, string expected)
        => Assert.Equal(expected, FestivalOutcomes.ClassifyIceFishing(fish));

    [Theory]
    [InlineData(0, FestivalOutcome.GiftLoved)]
    [InlineData(2, FestivalOutcome.GiftLiked)]
    [InlineData(8, FestivalOutcome.GiftNeutral)]
    [InlineData(4, FestivalOutcome.GiftDisliked)]
    [InlineData(6, FestivalOutcome.GiftHated)]
    [InlineData(7, FestivalOutcome.GiftLoved)]   // stardrop tea counts as loved
    [InlineData(99, FestivalOutcome.GiftNeutral)]
    public void GiftTaste_MapsToOutcome(int taste, string expected)
        => Assert.Equal(expected, FestivalOutcomes.ClassifyGift(taste));

    [Theory]
    [InlineData(FestivalIds.EggFestival, FestivalOutcome.EggWon, FestivalMemoryKeys.EggHuntWon)]
    [InlineData(FestivalIds.EggFestival, FestivalOutcome.EggLost, FestivalMemoryKeys.EggHuntLost)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauToxic, FestivalMemoryKeys.LuauBad)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauBad, FestivalMemoryKeys.LuauBad)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauGood, FestivalMemoryKeys.LuauGood)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauBest, FestivalMemoryKeys.LuauGood)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauShorts, FestivalMemoryKeys.LuauShorts)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeFirst, FestivalMemoryKeys.FairWon)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeSecond, FestivalMemoryKeys.FairLost)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeThird, FestivalMemoryKeys.FairLost)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeLost, FestivalMemoryKeys.FairLost)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeShorts, FestivalMemoryKeys.FairShorts)]
    [InlineData(FestivalIds.SpiritsEve, FestivalOutcome.Pumpkin, FestivalMemoryKeys.SpiritsPumpkin)]
    [InlineData(FestivalIds.IceFestival, FestivalOutcome.IceWon, FestivalMemoryKeys.IceFishWon)]
    [InlineData(FestivalIds.IceFestival, FestivalOutcome.IceLost, FestivalMemoryKeys.IceFishLost)]
    public void Outcome_MapsToMemory(string festival, string outcome, string memory)
        => Assert.Equal(memory, FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = festival, Outcome = outcome, OutcomeRun = 1 }));

    [Theory]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauOkay)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauEmpty)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeNone)]
    [InlineData(FestivalIds.SpiritsEve, "")]
    public void NeutralOutcomes_AreSilent(string festival, string outcome)
        => Assert.Null(FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = festival, Outcome = outcome, AttendedRun = 1 }));

    [Fact]
    public void AttendanceFestivals_SpeakFromAttendance()
    {
        Assert.Equal(FestivalMemoryKeys.JelliesAttended,
            FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = FestivalIds.Jellies, AttendedRun = 1 }));
        Assert.Equal(FestivalMemoryKeys.DanceAttended,
            FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = FestivalIds.FlowerDance, AttendedRun = 2 }));
        Assert.Null(FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = FestivalIds.Jellies, AttendedRun = -1 }));
    }

    [Fact]
    public void WinterStar_HasNoOrdinaryMemory()
        => Assert.Null(FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = FestivalIds.WinterStar, AttendedRun = 1, Outcome = FestivalOutcome.GiftLoved }));

    [Theory]
    [InlineData(FestivalOutcome.GiftLoved, FestivalMemoryKeys.WinterStarSeenLiked)]
    [InlineData(FestivalOutcome.GiftLiked, FestivalMemoryKeys.WinterStarSeenLiked)]
    [InlineData(FestivalOutcome.GiftNeutral, FestivalMemoryKeys.WinterStarSeen)]
    [InlineData(FestivalOutcome.GiftDisliked, FestivalMemoryKeys.WinterStarSeenDisliked)]
    [InlineData(FestivalOutcome.GiftHated, FestivalMemoryKeys.WinterStarSeenDisliked)]
    public void WinterStarSeen_VariantFromGift(string outcome, string memory)
        => Assert.Equal(memory, FestivalOutcomes.WinterStarSeenMemory(outcome));

    [Theory]
    [InlineData("spring13", true)]
    [InlineData("winter25", true)]
    [InlineData("NightMarket", false)]
    [InlineData("summer15", false)]
    public void KnownFestivals(string id, bool known) => Assert.Equal(known, FestivalIds.IsTracked(id));

    [Theory]
    [InlineData("festival_spring13", "spring13")]
    [InlineData("spring13", "spring13")]
    [InlineData("festival_", null)]
    [InlineData(null, null)]
    public void FromEventId(string? eventId, string? expected) => Assert.Equal(expected, FestivalIds.FromEventId(eventId));
}
