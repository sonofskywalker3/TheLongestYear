using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>No item asked twice across the board (Nexus report, TLY 1.0.0: Common Mushroom in both
/// Spring Foraging and Fall Foraging).</summary>
public class BoardNoRepeatsTests
{
    private static readonly Season Sp = Season.Spring, Su = Season.Summer, Fa = Season.Fall, Wi = Season.Winter;

    private static PoolItem Item(string id, params Season[] seasons)
        => new(id, 50, 3, seasons, Array.Empty<string>());

    /// <summary>The live Seasonal Forage pool (docs/engine-bundle-catalogue.md), seasons as the
    /// catalog has them.</summary>
    private static readonly PoolItem[] Forage =
    {
        Item("(O)410", Fa), Item("(O)18", Sp), Item("(O)719"), Item("(O)22", Sp), Item("(O)404", Sp, Fa),
        Item("(O)723"), Item("(O)16", Sp), Item("(O)372"), Item("(O)396", Su), Item("(O)718"),
        Item("(O)20", Sp), Item("(O)418", Wi), Item("(O)412", Wi), Item("(O)420", Sp, Su, Fa), Item("(O)90"),
        Item("(O)283", Wi), Item("(O)398", Su), Item("(O)402", Su), Item("(O)406", Fa), Item("(O)259", Su),
        Item("(O)408", Fa), Item("(O)416", Wi), Item("(O)88"), Item("(O)392", Wi), Item("(O)257", Sp),
        Item("(O)414", Wi), Item("(O)281", Fa), Item("(O)422", Fa, Wi), Item("(O)394", Su),
    };

    private static BundleSpec Seasonal(string name, int index, params string[] ids)
        => new("Crafts Room", index, name, name, "O 495 30", 0, ids.Length,
            ids.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static IReadOnlyList<IReadOnlyList<string?>> Board(IEnumerable<BundleSpec> bundles)
        => bundles.Select(b => (IReadOnlyList<string?>)b.Slots.Select(s => (string?)BundleParsing.NormalizeItemId(s.ItemId)).ToList()).ToList();

    /// <summary>The Crafts Room the way BundleEngine.Generate builds it: the Four Seasons Sampler
    /// composed up front, then Spring and Fall Foraging filled tightest pool first, each avoiding
    /// what is already asked. With <paramref name="noRepeatPass"/> the engine's last pass runs.</summary>
    private static List<BundleSpec> CraftsRoom(int seed, bool noRepeatPass)
    {
        var pools = new ItemPools { Forage = Forage };
        var tuning = new BundleGenerationTuning();
        AuthoredBundleDef sampler = AuthoredBundleCatalog.All.Single(d => d.Name == "Four Seasons Sampler");
        BundleSpec composedSampler = AuthoredBundleComposer.Compose(sampler, 3, pools, tuning, true, new Random(seed ^ 77))!;
        Assert.NotNull(composedSampler);

        BundleSpec spring = Seasonal("Spring Foraging", 0, "16", "18", "20", "22");
        BundleSpec fall = Seasonal("Fall Foraging", 2, "404", "406", "408", "410");
        var springMatch = new DomainMatch(PoolDomain.SeasonalForage, Sp);
        var fallMatch = new DomainMatch(PoolDomain.SeasonalForage, Fa);

        var asked = new HashSet<string>(composedSampler.Slots.Select(s => s.ItemId), StringComparer.Ordinal);
        var fills = new[] { (spring, springMatch), (fall, fallMatch) }
            .OrderBy(f => BundleSlotFiller.CandidateCount(f.Item1, f.Item2, pools))
            .ThenBy(f => f.Item1.Index)
            .ToList();
        var composed = new Dictionary<string, BundleSpec>();
        foreach ((BundleSpec pick, DomainMatch match) in fills)
        {
            BundleSpec filled = BundleSlotFiller.Fill(pick, match, pools, tuning, new Random(seed ^ (pick.Index * 6151)), avoid: asked);
            foreach (BundleSlotSpec slot in filled.Slots) asked.Add(slot.ItemId);
            composed[pick.Name] = filled;
        }

        var entries = new List<NoRepeatEntry>
        {
            new(composed["Spring Foraging"], spring, springMatch),
            new(composedSampler, composedSampler, Authored: sampler),
            new(composed["Fall Foraging"], fall, fallMatch),
        };
        if (!noRepeatPass)
            return entries.Select(e => e.Composed).ToList();
        IReadOnlyList<BundleSpec> result = BoardNoRepeatPass.Apply(entries, pools, null, tuning, true, null, seed, null, out _);
        return result.ToList();
    }

    [Fact]
    public void TheFillsAlone_LetTheSameForageIntoTwoBundles()
    {
        // The leak: once the Sampler and Spring Foraging have eaten into Fall Foraging's seven
        // candidates, its avoid set gives way wholesale and a repeat comes back.
        int leaks = Enumerable.Range(0, 300).Count(seed => BoardNoRepeats.Repeats(Board(CraftsRoom(seed, noRepeatPass: false))).Count > 0);
        int mushroom = Enumerable.Range(0, 300).Count(seed => BoardNoRepeats.Repeats(Board(CraftsRoom(seed, noRepeatPass: false))).Contains("(O)404"));
        Assert.True(mushroom > 0, $"{leaks} of 300 seeds repeat an item, {mushroom} of them the Common Mushroom");
    }

    [Fact]
    public void TheNoRepeatPass_LeavesEveryForageAskedOnce()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            List<BundleSpec> room = CraftsRoom(seed, noRepeatPass: true);
            Assert.Empty(BoardNoRepeats.Repeats(Board(room)));
            // Season-named bundles still ask only for their season's items.
            Assert.All(room[0].Slots, s => Assert.Contains(Sp, Forage.Single(f => f.ItemId == s.ItemId).Seasons));
            Assert.All(room[2].Slots, s => Assert.Contains(Fa, Forage.Single(f => f.ItemId == s.ItemId).Seasons));
            Assert.Equal(4, room[0].Slots.Count);
            Assert.Equal(4, room[2].Slots.Count);
        }
    }

    [Fact]
    public void TheNoRepeatPass_IsDeterministic()
    {
        for (int seed = 0; seed < 50; seed++)
            Assert.Equal(Board(CraftsRoom(seed, true)).SelectMany(b => b), Board(CraftsRoom(seed, true)).SelectMany(b => b));
    }

    [Fact]
    public void MineralAndRockhounds_ComposedBlind_NeverShareAnAskAfterThePass()
    {
        // Both authored defs draw from the minerals pool, composed before the board exists.
        var minerals = Enumerable.Range(538, 30)
            .Select(i => new PoolItem($"(O){i}", 50, 3, Array.Empty<Season>(), Array.Empty<string>(), ItemKindClassifier.MineralCategory))
            .ToList();
        var pools = new ItemPools { GeodeMinerals = minerals };
        var tuning = new BundleGenerationTuning();
        AuthoredBundleDef mineral = AuthoredBundleCatalog.All.Single(d => d.Name == "Mineral");
        AuthoredBundleDef rockhound = AuthoredBundleCatalog.All.Single(d => d.Name == "Rockhound's");
        bool sawRepeat = false;
        for (int seed = 0; seed < 100; seed++)
        {
            BundleSpec a = AuthoredBundleComposer.Compose(mineral, 30, pools, tuning, true, new Random(seed))!;
            BundleSpec b = AuthoredBundleComposer.Compose(rockhound, 31, pools, tuning, true, new Random(seed * 31 + 7))!;
            sawRepeat |= BoardNoRepeats.Repeats(Board(new[] { a, b })).Count > 0;
            IReadOnlyList<BundleSpec> after = BoardNoRepeatPass.Apply(
                new[] { new NoRepeatEntry(a, a, Authored: mineral), new NoRepeatEntry(b, b, Authored: rockhound) },
                pools, null, tuning, true, null, seed, null, out IReadOnlyList<string> left);
            Assert.Empty(left);
            Assert.Empty(BoardNoRepeats.Repeats(Board(after)));
            Assert.Equal(a.Slots.Count, after[0].Slots.Count);
            Assert.Equal(b.Slots.Count, after[1].Slots.Count);
        }
        Assert.True(sawRepeat, "the scenario should exercise a repeat");
    }

    [Fact]
    public void Resolve_MovesTheRepeatOntoAFreeItem()
    {
        var board = new List<IReadOnlyList<string?>> { new[] { "a", "b" }, new[] { "a", "c" } };
        var moves = BoardNoRepeats.Resolve(board,
            s => s == new BoardSlotRef(1, 0) ? new[] { "b", "d" } : null, out IReadOnlyList<string> left);
        Assert.Empty(left);
        Assert.Equal("d", moves[new BoardSlotRef(1, 0)]);
        Assert.Single(moves);
    }

    [Fact]
    public void Resolve_FreesAnItemThroughAnotherBundle_WhenTheRepeatHasNoFreeChoice()
    {
        // Fall Foraging (bundle 1) can only take items Spring Foraging (bundle 0) holds, but
        // Spring Foraging can give its mushroom up for a daffodil nobody asks for.
        var board = new List<IReadOnlyList<string?>> { new[] { "mushroom", "leek" }, new[] { "mushroom", "plum" } };
        IReadOnlyList<string>? Options(BoardSlotRef s)
            => s.Bundle == 0 ? new[] { "daffodil" } : new[] { "plum" };
        var moves = BoardNoRepeats.Resolve(board, Options, out IReadOnlyList<string> left);
        Assert.Empty(left);
        string?[][] after = board.Select(b => b.ToArray()).ToArray();
        foreach (var move in moves) after[move.Key.Bundle][move.Key.Slot] = move.Value;
        Assert.Empty(BoardNoRepeats.Repeats(after.Select(b => (IReadOnlyList<string?>)b).ToList()));
        Assert.Equal("mushroom", after[1][0]);
    }

    [Fact]
    public void Resolve_NeverMovesAFixedSlot_AndReportsWhatItCannotSeparate()
    {
        var board = new List<IReadOnlyList<string?>> { new[] { "a" }, new[] { "a" } };
        var moves = BoardNoRepeats.Resolve(board, _ => null, out IReadOnlyList<string> left);
        Assert.Empty(moves);
        Assert.Equal(new[] { "a" }, left);
    }

    [Fact]
    public void Resolve_LeavesACleanBoardAlone_AndIgnoresMoneySlots()
    {
        var board = new List<IReadOnlyList<string?>> { new string?[] { "a", null }, new string?[] { "b", null } };
        var moves = BoardNoRepeats.Resolve(board, _ => new[] { "z" }, out IReadOnlyList<string> left);
        Assert.Empty(moves);
        Assert.Empty(left);
    }
}
