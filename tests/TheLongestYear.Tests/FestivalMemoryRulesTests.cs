using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FestivalMemoryRulesTests
{
    private static GameplayConfig Config() => new();

    private static FestivalActor Villager(string name) => new(name);

    private static MetaState MetaWith(string festival, string outcome, string item = "")
    {
        var meta = new MetaState();
        meta.FestivalMemories[festival] = new FestivalMemory
        { Festival = festival, AttendedRun = 1, OutcomeRun = 1, Outcome = outcome, ItemId = item };
        return meta;
    }

    private static RunState Run2() => new() { RunNumber = 2, Seed = 1234 };

    private static List<PlannedMemory> Plan(MetaState meta, RunState run, string festival, int roll,
        params FestivalActor[] actors)
        => FestivalMemoryRules.PlanFestivalDay(meta, run, festival, actors, Config(), _ => roll, force: false).ToList();

    [Fact]
    public void NoRecord_NoLine()
        => Assert.Empty(Plan(new MetaState(), Run2(), FestivalIds.Luau, 0, Villager("Gus")));

    [Fact]
    public void SilentOutcome_NoLine()
        => Assert.Empty(Plan(MetaWith(FestivalIds.Luau, FestivalOutcome.LuauOkay), Run2(), FestivalIds.Luau, 0, Villager("Gus")));

    [Fact]
    public void Ordinary_RollsTwentyPercent_NoFriendshipGate()
    {
        MetaState meta = MetaWith(FestivalIds.Luau, FestivalOutcome.LuauBad, "(O)248");
        Assert.Empty(meta.VillagerFamiliarity);
        PlannedMemory hit = Assert.Single(Plan(meta, Run2(), FestivalIds.Luau, 19, Villager("Gus")));
        Assert.Equal(FestivalMemoryKeys.LuauBad, hit.Memory);
        Assert.Equal("Gus", hit.Npc);
        Assert.False(hit.IsBond);
        Assert.Empty(Plan(meta, Run2(), FestivalIds.Luau, 20, Villager("Gus")));
    }

    [Fact]
    public void EveryEligibleVillagerRolls()
    {
        MetaState meta = MetaWith(FestivalIds.SpiritsEve, FestivalOutcome.Pumpkin);
        var planned = Plan(meta, Run2(), FestivalIds.SpiritsEve, 0, Villager("Jas"), Villager("Vincent"), Villager("Pam"));
        Assert.Equal(new[] { "Jas", "Vincent", "Pam" }, planned.Select(p => p.Npc));
    }

    [Fact]
    public void HostSpouseChildAndNonSocial_AreSkipped()
    {
        MetaState meta = MetaWith(FestivalIds.Fair, FestivalOutcome.GrangeFirst);
        var planned = Plan(meta, Run2(), FestivalIds.Fair, 0,
            new FestivalActor("Lewis", IsHost: true),
            new FestivalActor("Penny", IsSpouse: true),
            new FestivalActor("Kid", IsChild: true),
            new FestivalActor("Governor", CanSocialize: false),
            Villager("Pierre"));
        Assert.Equal("Pierre", Assert.Single(planned).Npc);
    }

    [Fact]
    public void Budget_OnceHeard_NoOneGetsALine()
    {
        MetaState meta = MetaWith(FestivalIds.Luau, FestivalOutcome.LuauBad);
        RunState run = Run2();
        FestivalMemoryStore.MarkHeard(run, FestivalIds.Luau);
        Assert.Empty(Plan(meta, run, FestivalIds.Luau, 0, Villager("Gus")));
    }

    [Fact]
    public void Force_SkipsChanceAndBudget_ButNotGuards()
    {
        MetaState meta = MetaWith(FestivalIds.Luau, FestivalOutcome.LuauBad);
        RunState run = Run2();
        FestivalMemoryStore.MarkHeard(run, FestivalIds.Luau);
        var planned = FestivalMemoryRules.PlanFestivalDay(meta, run, FestivalIds.Luau,
            new[] { Villager("Gus"), new FestivalActor("Lewis", IsHost: true) }, Config(), _ => 99, force: true).ToList();
        Assert.Equal("Gus", Assert.Single(planned).Npc);
    }

    [Fact]
    public void FeatureOff_OrPhaseOneOff_Silent()
    {
        MetaState meta = MetaWith(FestivalIds.Luau, FestivalOutcome.LuauBad);
        var off = new GameplayConfig { EnableDejaVuFestivalMemories = false };
        Assert.Empty(FestivalMemoryRules.PlanFestivalDay(meta, Run2(), FestivalIds.Luau, new[] { Villager("Gus") }, off, _ => 0, true));
        var phase1Off = new GameplayConfig { EnableDejaVuDialogue = false };
        Assert.Empty(FestivalMemoryRules.PlanFestivalDay(meta, Run2(), FestivalIds.Luau, new[] { Villager("Gus") }, phase1Off, _ => 0, true));
    }

    [Fact]
    public void EggGuarantee_EveryoneAtNinetyNine()
    {
        MetaState meta = MetaWith(FestivalIds.EggFestival, FestivalOutcome.EggWon);
        var planned = Plan(meta, Run2(), FestivalIds.EggFestival, 99, Villager("Abigail"), Villager("Pam"));
        Assert.Equal(2, planned.Count);
        Assert.All(planned, p => Assert.True(p.Guaranteed));
    }

    [Fact]
    public void EggGuarantee_Spent_FallsBackToTwenty()
    {
        MetaState meta = MetaWith(FestivalIds.EggFestival, FestivalOutcome.EggWon);
        meta.FestivalMemories[FestivalIds.EggFestival].HeardRun = 2;
        Assert.Empty(Plan(meta, new RunState { RunNumber = 3 }, FestivalIds.EggFestival, 99, Villager("Abigail")));
        Assert.Single(Plan(meta, new RunState { RunNumber = 3 }, FestivalIds.EggFestival, 19, Villager("Abigail")));
    }

    [Fact]
    public void FlowerDance_AttendedLine_OnlyForNonDancers()
    {
        var meta = new MetaState();
        meta.FestivalMemories[FestivalIds.FlowerDance] = new FestivalMemory { Festival = FestivalIds.FlowerDance, AttendedRun = 1 };
        var planned = Plan(meta, Run2(), FestivalIds.FlowerDance, 0,
            new FestivalActor("Penny", CanDance: true), Villager("Pierre"));
        PlannedMemory p = Assert.Single(planned);
        Assert.Equal("Pierre", p.Npc);
        Assert.Equal(FestivalMemoryKeys.DanceAttended, p.Memory);
    }

    [Fact]
    public void WinterStar_PastRecipient_BondRollWithGiftVariant()
    {
        var meta = new MetaState();
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Gus", Run = 1, Outcome = FestivalOutcome.GiftDisliked, ItemId = "(O)a" });
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Pam", Run = 1, Outcome = FestivalOutcome.GiftNeutral });
        var planned = Plan(meta, Run2(), FestivalIds.WinterStar, 49, Villager("Gus"), Villager("Pam"), Villager("Linus"));
        Assert.Equal(2, planned.Count);
        Assert.Equal(FestivalMemoryKeys.WinterStarSeenDisliked, planned[0].Memory);
        Assert.Equal("(O)a", planned[0].ItemId);
        Assert.True(planned[0].IsBond);
        Assert.Equal(FestivalMemoryKeys.WinterStarSeen, planned[1].Memory);
        Assert.Empty(Plan(meta, Run2(), FestivalIds.WinterStar, 50, Villager("Gus")));
    }

    [Fact]
    public void WinterStarSeen_NeverForThisYearsSecretFriend()
    {
        var meta = new MetaState();
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Gus", Run = 1, Outcome = FestivalOutcome.GiftLiked });
        Assert.Empty(Plan(meta, Run2(), FestivalIds.WinterStar, 0, new FestivalActor("Gus", IsSecretFriend: true)));
    }

    [Fact]
    public void LuauLine_CarriesTheItem()
    {
        MetaState meta = MetaWith(FestivalIds.Luau, FestivalOutcome.LuauGood, "(O)24");
        Assert.Equal("(O)24", Assert.Single(Plan(meta, Run2(), FestivalIds.Luau, 0, Villager("Gus"))).ItemId);
    }

    // ---- post-result -------------------------------------------------------------------

    [Theory]
    [InlineData(FestivalIds.EggFestival, FestivalOutcome.EggLost, FestivalOutcome.EggLost, FestivalMemoryKeys.EggHuntLost)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauToxic, FestivalOutcome.LuauBad, FestivalMemoryKeys.LuauBad)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauBest, FestivalOutcome.LuauGood, FestivalMemoryKeys.LuauGood)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeFirst, FestivalOutcome.GrangeFirst, FestivalMemoryKeys.FairWon)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeThird, FestivalOutcome.GrangeLost, FestivalMemoryKeys.FairLost)]
    [InlineData(FestivalIds.IceFestival, FestivalOutcome.IceWon, FestivalOutcome.IceWon, FestivalMemoryKeys.IceFishWon)]
    public void After_SameResultAgain_Rolls(string festival, string past, string now, string memory)
    {
        MetaState meta = MetaWith(festival, past);
        Assert.Equal(memory, FestivalMemoryRules.PlanAfter(meta, Run2(), festival, now, Config(), 19, false));
        Assert.Null(FestivalMemoryRules.PlanAfter(meta, Run2(), festival, now, Config(), 20, false));
    }

    [Theory]
    [InlineData(FestivalIds.EggFestival, FestivalOutcome.EggLost, FestivalOutcome.EggWon)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauBad, FestivalOutcome.LuauGood)]
    [InlineData(FestivalIds.Fair, FestivalOutcome.GrangeLost, FestivalOutcome.GrangeShorts)]
    [InlineData(FestivalIds.Luau, FestivalOutcome.LuauOkay, FestivalOutcome.LuauOkay)]
    public void After_DifferentOrSilentResult_Nothing(string festival, string past, string now)
        => Assert.Null(FestivalMemoryRules.PlanAfter(MetaWith(festival, past), Run2(), festival, now, Config(), 0, false));

    [Fact]
    public void After_EggWinAgain_GuaranteedWhileArmed()
    {
        MetaState meta = MetaWith(FestivalIds.EggFestival, FestivalOutcome.EggWon);
        Assert.Equal(FestivalMemoryKeys.EggHuntWon,
            FestivalMemoryRules.PlanAfter(meta, Run2(), FestivalIds.EggFestival, FestivalOutcome.EggWon, Config(), 99, false));
    }

    [Fact]
    public void After_SpentFestival_Nothing_UnlessForced()
    {
        MetaState meta = MetaWith(FestivalIds.IceFestival, FestivalOutcome.IceWon);
        RunState run = Run2();
        FestivalMemoryStore.MarkHeard(run, FestivalIds.IceFestival);
        Assert.Null(FestivalMemoryRules.PlanAfter(meta, run, FestivalIds.IceFestival, FestivalOutcome.IceWon, Config(), 0, false));
        Assert.NotNull(FestivalMemoryRules.PlanAfter(meta, run, FestivalIds.IceFestival, FestivalOutcome.IceWon, Config(), 99, true));
    }

    [Fact]
    public void Bonds_DanceAgainAndOther_FiftyPercent()
    {
        var meta = new MetaState();
        meta.DancePartners.Add(new BondMemory { Npc = "Penny", Run = 1, Outcome = FestivalOutcome.Danced });
        RunState run = Run2();
        Assert.True(FestivalMemoryRules.BondHit(meta, run, FestivalIds.FlowerDance, "Penny", Config(), 49, false));
        Assert.False(FestivalMemoryRules.BondHit(meta, run, FestivalIds.FlowerDance, "Penny", Config(), 50, false));
        Assert.False(FestivalMemoryRules.BondHit(meta, run, FestivalIds.FlowerDance, "Emily", Config(), 0, false));
        FestivalMemoryStore.MarkHeard(run, FestivalIds.FlowerDance);
        Assert.False(FestivalMemoryRules.BondHit(meta, run, FestivalIds.FlowerDance, "Penny", Config(), 0, false));
    }

    [Fact]
    public void Bonds_WinterStarAgain_NeedsAPastRecipient()
    {
        var meta = new MetaState();
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Gus", Run = 1, Outcome = FestivalOutcome.GiftLiked });
        Assert.True(FestivalMemoryRules.BondHit(meta, Run2(), FestivalIds.WinterStar, "Gus", Config(), 0, false));
        Assert.False(FestivalMemoryRules.BondHit(meta, Run2(), FestivalIds.WinterStar, "Pam", Config(), 0, false));
    }

    // ---- stable roll ----------------------------------------------------------------------

    [Fact]
    public void StableRoll_SameInputsSameValue_DifferentRunsDiffer()
    {
        var run = new RunState { RunNumber = 2, Seed = 77 };
        int a = FestivalMemoryRules.StableRoll(run, FestivalIds.Luau, "day", "Gus");
        Assert.Equal(a, FestivalMemoryRules.StableRoll(run, FestivalIds.Luau, "day", "Gus"));
        Assert.InRange(a, 0, 99);
        var values = Enumerable.Range(2, 30)
            .Select(n => FestivalMemoryRules.StableRoll(new RunState { RunNumber = n, Seed = 77 + n }, FestivalIds.Luau, "day", "Gus"))
            .Distinct().Count();
        Assert.True(values > 5);
    }

    [Fact]
    public void StableRoll_RoughlyUniform()
    {
        var run = new RunState { RunNumber = 3, Seed = 5 };
        int hits = Enumerable.Range(0, 2000).Count(i => FestivalMemoryRules.StableRoll(run, FestivalIds.Fair, "day", "npc" + i) < 20);
        Assert.InRange(hits, 300, 500);
    }
}
