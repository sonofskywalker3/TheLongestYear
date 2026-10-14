using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FestivalMemoryStoreTests
{
    private static RunState Run(int number) => new() { RunNumber = number };

    [Fact]
    public void RecordOutcome_SetsAttendanceAndOutcome()
    {
        var run = Run(3);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.Luau, FestivalOutcome.LuauBad, "(O)248", 1);
        FestivalMemory m = run.FestivalLog[FestivalIds.Luau];
        Assert.Equal(3, m.AttendedRun);
        Assert.Equal(3, m.OutcomeRun);
        Assert.Equal(FestivalOutcome.LuauBad, m.Outcome);
        Assert.Equal("(O)248", m.ItemId);
        Assert.Equal(1, m.ItemQuality);
    }

    [Fact]
    public void Commit_AttendanceOnly_KeepsOldOutcome()
    {
        var meta = new MetaState();
        meta.FestivalMemories[FestivalIds.Luau] = new FestivalMemory
        { Festival = FestivalIds.Luau, AttendedRun = 1, OutcomeRun = 1, Outcome = FestivalOutcome.LuauGood, ItemId = "(O)24" };
        var run = Run(2);
        FestivalMemoryStore.RecordAttendance(run, FestivalIds.Luau);
        FestivalMemoryStore.Commit(meta, run);
        FestivalMemory m = meta.FestivalMemories[FestivalIds.Luau];
        Assert.Equal(2, m.AttendedRun);
        Assert.Equal(1, m.OutcomeRun);
        Assert.Equal(FestivalOutcome.LuauGood, m.Outcome);
        Assert.Equal("(O)24", m.ItemId);
    }

    [Fact]
    public void Commit_Outcome_ReplacesEveryOutcomeField()
    {
        var meta = new MetaState();
        meta.FestivalMemories[FestivalIds.Fair] = new FestivalMemory
        { Festival = FestivalIds.Fair, AttendedRun = 1, OutcomeRun = 1, Outcome = FestivalOutcome.GrangeFirst, Score = 95, ItemId = "x", Npc = "y" };
        var run = Run(2);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.Fair, FestivalOutcome.GrangeLost, score: 40);
        FestivalMemoryStore.Commit(meta, run);
        FestivalMemory m = meta.FestivalMemories[FestivalIds.Fair];
        Assert.Equal(FestivalOutcome.GrangeLost, m.Outcome);
        Assert.Equal(40, m.Score);
        Assert.Equal("", m.ItemId);
        Assert.Equal("", m.Npc);
        Assert.Equal(2, m.OutcomeRun);
    }

    [Fact]
    public void Commit_MissingFestival_KeepsRecord()
    {
        var meta = new MetaState();
        var old = new FestivalMemory { Festival = FestivalIds.IceFestival, AttendedRun = 1, OutcomeRun = 1, Outcome = FestivalOutcome.IceWon };
        meta.FestivalMemories[FestivalIds.IceFestival] = old;
        FestivalMemoryStore.Commit(meta, Run(2));
        Assert.Same(old, meta.FestivalMemories[FestivalIds.IceFestival]);
        Assert.Equal(FestivalOutcome.IceWon, old.Outcome);
    }

    [Fact]
    public void Commit_SilentOutcome_ReplacesSpeakableOne()
    {
        var meta = new MetaState();
        meta.FestivalMemories[FestivalIds.Luau] = new FestivalMemory
        { Festival = FestivalIds.Luau, AttendedRun = 1, OutcomeRun = 1, Outcome = FestivalOutcome.LuauBad };
        var run = Run(2);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.Luau, FestivalOutcome.LuauEmpty);
        FestivalMemoryStore.Commit(meta, run);
        Assert.Null(FestivalOutcomes.MemoryFor(meta.FestivalMemories[FestivalIds.Luau]));
    }

    [Fact]
    public void Commit_HeardStampBeforeOutcome_NewWinRearmsGuarantee()
    {
        var meta = new MetaState();
        meta.FestivalMemories[FestivalIds.EggFestival] = new FestivalMemory
        { Festival = FestivalIds.EggFestival, AttendedRun = 1, OutcomeRun = 1, Outcome = FestivalOutcome.EggWon };
        var run = Run(2);
        FestivalMemoryStore.MarkHeard(run, FestivalIds.EggFestival);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.EggFestival, FestivalOutcome.EggWon, score: 10);
        FestivalMemoryStore.Commit(meta, run);
        FestivalMemory m = meta.FestivalMemories[FestivalIds.EggFestival];
        Assert.Equal(2, m.HeardRun);
        Assert.Equal(2, m.OutcomeRun);
        // What was heard in loop 2 was loop 1's win; loop 2's own win is not remembered yet.
        Assert.True(FestivalMemoryStore.EggGuaranteeArmed(meta));
        var run3 = Run(3);
        FestivalMemoryStore.MarkHeard(run3, FestivalIds.EggFestival);
        FestivalMemoryStore.Commit(meta, run3);
        Assert.False(FestivalMemoryStore.EggGuaranteeArmed(meta));
    }

    [Fact]
    public void EggGuarantee_ArmedAfterAWinUntilHeard()
    {
        var meta = new MetaState();
        Assert.False(FestivalMemoryStore.EggGuaranteeArmed(meta));
        var run = Run(1);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.EggFestival, FestivalOutcome.EggWon, score: 9);
        FestivalMemoryStore.Commit(meta, run);
        Assert.True(FestivalMemoryStore.EggGuaranteeArmed(meta));
        var run2 = Run(2);
        FestivalMemoryStore.MarkHeard(run2, FestivalIds.EggFestival);
        FestivalMemoryStore.Commit(meta, run2);
        Assert.False(FestivalMemoryStore.EggGuaranteeArmed(meta));
    }

    [Fact]
    public void EggGuarantee_NotArmedByALoss()
    {
        var meta = new MetaState();
        var run = Run(1);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.EggFestival, FestivalOutcome.EggLost, score: 3);
        FestivalMemoryStore.Commit(meta, run);
        Assert.False(FestivalMemoryStore.EggGuaranteeArmed(meta));
    }

    [Fact]
    public void Commit_DanceAndGift_AppendBonds()
    {
        var meta = new MetaState();
        var run = Run(1);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.Danced, npc: "Penny");
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.WinterStar, FestivalOutcome.GiftLiked, "(O)395", 0, "Gus");
        FestivalMemoryStore.Commit(meta, run);
        BondMemory partner = Assert.Single(meta.DancePartners);
        Assert.Equal("Penny", partner.Npc);
        Assert.Equal(1, partner.Run);
        Assert.Equal(FestivalOutcome.Danced, partner.Outcome);
        BondMemory gift = Assert.Single(meta.WinterStarRecipients);
        Assert.Equal("Gus", gift.Npc);
        Assert.Equal("(O)395", gift.ItemId);
        Assert.Equal(FestivalOutcome.GiftLiked, gift.Outcome);
    }

    [Fact]
    public void Commit_NoPartner_AppendsNothing()
    {
        var meta = new MetaState();
        var run = Run(1);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.NoPartner);
        FestivalMemoryStore.Commit(meta, run);
        Assert.Empty(meta.DancePartners);
    }

    [Fact]
    public void FivePartners_AllKeptOldestFirst()
    {
        var meta = new MetaState();
        string[] names = { "Penny", "Abigail", "Leah", "Maru", "Haley" };
        for (int i = 0; i < names.Length; i++)
        {
            var run = Run(i + 1);
            FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.Danced, npc: names[i]);
            FestivalMemoryStore.Commit(meta, run);
        }
        Assert.Equal(names, meta.DancePartners.Select(b => b.Npc));
        Assert.True(FestivalBonds.IsPastPartner(meta, "Penny"));
        Assert.False(FestivalBonds.IsPastPartner(meta, "Emily"));
    }

    [Fact]
    public void SamePartnerTwice_TwoEntries()
    {
        var meta = new MetaState();
        for (int i = 1; i <= 2; i++)
        {
            var run = Run(i);
            FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.Danced, npc: "Sam");
            FestivalMemoryStore.Commit(meta, run);
        }
        Assert.Equal(2, meta.DancePartners.Count);
    }

    [Fact]
    public void LatestGift_IsTheNewest()
    {
        var meta = new MetaState();
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Gus", Run = 1, Outcome = FestivalOutcome.GiftHated, ItemId = "(O)a" });
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Pam", Run = 2, Outcome = FestivalOutcome.GiftLoved, ItemId = "(O)b" });
        meta.WinterStarRecipients.Add(new BondMemory { Npc = "Gus", Run = 3, Outcome = FestivalOutcome.GiftLoved, ItemId = "(O)c" });
        BondMemory latest = FestivalBonds.LatestGift(meta, "Gus")!;
        Assert.Equal("(O)c", latest.ItemId);
        Assert.True(FestivalBonds.IsPastRecipient(meta, "Pam"));
        Assert.Null(FestivalBonds.LatestGift(meta, "Linus"));
    }

    [Fact]
    public void MarkHeard_OnlyOnce()
    {
        var run = Run(1);
        Assert.True(FestivalMemoryStore.MarkHeard(run, FestivalIds.Jellies));
        Assert.False(FestivalMemoryStore.MarkHeard(run, FestivalIds.Jellies));
        Assert.True(FestivalMemoryStore.IsSpent(run, FestivalIds.Jellies));
        Assert.False(FestivalMemoryStore.IsSpent(run, FestivalIds.Luau));
    }

    [Fact]
    public void BeginNewRun_ClearsLogAndHeard_KeepsMetaBonds()
    {
        var meta = new MetaState();
        var run = Run(1);
        FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.Danced, npc: "Leah");
        FestivalMemoryStore.MarkHeard(run, FestivalIds.Jellies);
        FestivalMemoryStore.Commit(meta, run);
        run.BeginNewRun(7);
        Assert.Empty(run.FestivalLog);
        Assert.Empty(run.FestivalMemoryHeard);
        Assert.Single(meta.DancePartners);
    }

    [Fact]
    public void Commit_IgnoresUntrackedFestivals()
    {
        var meta = new MetaState();
        var run = Run(1);
        FestivalMemoryStore.RecordAttendance(run, "NightMarket");
        FestivalMemoryStore.Commit(meta, run);
        Assert.Empty(meta.FestivalMemories);
    }
}
