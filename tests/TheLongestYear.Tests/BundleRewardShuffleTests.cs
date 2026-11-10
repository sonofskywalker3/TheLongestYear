using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class BundleRewardShuffleTests
{
    private static readonly string[] Pool = BundleRewardShuffle.CleanPool(new[] { "O 495 30", "BO 21 1", "O 472 10", "", "O 495 30" }).ToArray();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> OnePool = new Dictionary<string, IReadOnlyList<string>> { ["Pantry"] = Pool };

    private static BundleSpec Spec(string room, int i, string reward)
        => new(room, i, "N" + i, "N" + i, reward, 0, 4, new List<BundleSlotSpec>());

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Pools(params (string Room, string[] Rewards)[] rooms)
        => rooms.ToDictionary(r => r.Room, r => (IReadOnlyList<string>)r.Rewards);

    private static bool SkipJoja(string room) => BundleRewardShuffle.SkipsRoom(room);

    [Fact]
    public void The_pool_is_cleaned_and_sorted()
        => Assert.Equal(new[] { "BO 21 1", "O 472 10", "O 495 30" }, Pool);

    [Fact]
    public void The_pool_drops_rewards_with_a_slash()
        => Assert.Equal(new[] { "O 1 1" }, BundleRewardShuffle.CleanPool(new[] { "O 1 1", "O 2/1", null! }));

    [Fact]
    public void Only_the_abandoned_joja_mart_is_skipped()
    {
        Assert.True(BundleRewardShuffle.SkipsRoom("Abandoned Joja Mart"));
        Assert.False(BundleRewardShuffle.SkipsRoom("Vault"));
        Assert.False(BundleRewardShuffle.SkipsRoom("Pantry"));
    }

    [Fact]
    public void Every_reward_comes_from_the_pool_vault_is_shuffled_and_joja_is_untouched()
    {
        var specs = new[]
        {
            Spec("Pantry", 0, "x"), Spec("Crafts Room", 13, "y"), Spec("Vault", 23, "z"),
            Spec("Abandoned Joja Mart", 36, "joja"),
        };
        var after = BundleRewardShuffle.Apply(specs, 42, OnePool, SkipJoja);
        Assert.All(after.Take(3), s => Assert.Contains(s.RewardField, Pool));
        Assert.Equal("joja", after[3].RewardField);
    }

    [Fact]
    public void Data_form_shuffles_the_vault_reward_and_keeps_its_gold_fields()
    {
        var board = new Dictionary<string, string>
        {
            ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4/1//2,500g",
            ["Abandoned Joja Mart/36"] = "The Missing//348 1 1 807 1 0 74 1 0 454 5 2 795 1 2 445 1 0/1/5//The Missing",
        };
        var after = BundleRewardShuffle.ApplyToData(board, 3, OnePool, SkipJoja);
        var a = after["Vault/23"].Split('/'); var b = board["Vault/23"].Split('/');
        Assert.Contains(a[1], Pool);
        Assert.Equal(b.Where((_, i) => i != 1), a.Where((_, i) => i != 1));
        Assert.Equal(board["Abandoned Joja Mart/36"], after["Abandoned Joja Mart/36"]);
    }

    [Fact]
    public void The_same_board_seed_gives_the_same_rewards()
    {
        var specs = new[] { Spec("Pantry", 0, "x"), Spec("Pantry", 1, "x") };
        Assert.Equal(BundleRewardShuffle.Apply(specs, 7, OnePool, _ => false).Select(s => s.RewardField),
                     BundleRewardShuffle.Apply(specs, 7, OnePool, _ => false).Select(s => s.RewardField));
    }

    [Fact]
    public void Data_form_replaces_only_the_reward_field()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0/0/4//Spring Crops" };
        var after = BundleRewardShuffle.ApplyToData(board, 3, OnePool, _ => false);
        var a = after["Pantry/0"].Split('/'); var b = board["Pantry/0"].Split('/');
        Assert.Contains(a[1], Pool);
        Assert.Equal(b.Where((_, i) => i != 1), a.Where((_, i) => i != 1));
    }

    [Fact]
    public void An_empty_pool_leaves_rewards_alone()
        => Assert.Equal("x", BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x") }, 1, Pools(), _ => false)[0].RewardField);

    [Fact]
    public void Each_bundle_draws_from_its_own_rooms_pool_first()
    {
        var pools = Pools(("Pantry", new[] { "A", "B" }), ("Crafts Room", new[] { "C", "D" }));
        foreach (int seed in Enumerable.Range(0, 20))
        {
            var after = BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x"), Spec("Crafts Room", 13, "y") }, seed, pools, _ => false);
            Assert.Contains(after[0].RewardField, pools["Pantry"]);
            Assert.Contains(after[1].RewardField, pools["Crafts Room"]);
        }
    }

    [Fact]
    public void A_reward_two_rooms_share_is_handed_out_once()
    {
        // "C" is in both rooms' pools; once one room takes it the other cannot, so the board
        // holds every reward exactly once whatever the seed.
        var pools = Pools(("Pantry", new[] { "A", "C" }), ("Crafts Room", new[] { "C", "D", "E" }));
        var specs = new[] { Spec("Pantry", 0, "x"), Spec("Pantry", 1, "x"), Spec("Crafts Room", 13, "y"), Spec("Crafts Room", 14, "y") };
        foreach (int seed in Enumerable.Range(0, 20))
        {
            var after = BundleRewardShuffle.Apply(specs, seed, pools, _ => false).Select(s => s.RewardField).ToList();
            Assert.Equal(new[] { "A", "C" }, after.Take(2).OrderBy(r => r));
            Assert.Equal(new[] { "D", "E" }, after.Skip(2).OrderBy(r => r));
        }
    }

    [Fact]
    public void No_reward_repeats_until_every_reward_is_used()
    {
        // A one-reward room falls back to the board-wide pool, and a board of ten bundles over
        // four rewards cycles 4 + 4 + 2, each cycle without a repeat.
        var pools = Pools(("Pantry", new[] { "A" }), ("Crafts Room", new[] { "B", "C", "D" }));
        var specs = Enumerable.Range(0, 10).Select(i => Spec(i < 6 ? "Pantry" : "Crafts Room", i, "x")).ToList();
        foreach (int seed in Enumerable.Range(0, 20))
        {
            var rewards = BundleRewardShuffle.Apply(specs, seed, pools, _ => false).Select(s => s.RewardField).ToList();
            Assert.Equal("A", rewards[0]);
            Assert.Equal(new[] { 4, 4, 2 }, rewards.Chunk(4).Select(c => c.Distinct().Count()));
        }
    }

    [Fact]
    public void Spec_and_data_forms_agree_on_a_shuffled_multi_room_board()
    {
        var pools = Pools(("Pantry", new[] { "A", "B", "C" }), ("Crafts Room", new[] { "C", "D" }), ("Vault", new[] { "V" }));
        var specs = new[]
        {
            Spec("Crafts Room", 9002, "c2"), Spec("Pantry", 1, "p1"), Spec("Vault", 23, "v"), Spec("Abandoned Joja Mart", 36, "j"),
            Spec("Pantry", 9001, "p9"), Spec("Crafts Room", 13, "c1"), Spec("Pantry", 0, "p0"), Spec("Crafts Room", 14, "c3"),
        };
        var board = specs.Reverse().ToDictionary(s => $"{s.Room}/{s.Index}", s => $"{s.Name}/{s.RewardField}/1 1 0/0/1//{s.Name}");
        foreach (int seed in new[] { 0, 5, 77 })
        {
            var fromSpecs = BundleRewardShuffle.Apply(specs, seed, pools, SkipJoja);
            var fromData = BundleRewardShuffle.ApplyToData(board, seed, pools, SkipJoja);
            Assert.Equal(specs.Select(s => $"{s.Room}/{s.Index}"), fromSpecs.Select(s => $"{s.Room}/{s.Index}"));
            Assert.All(fromSpecs, s => Assert.Equal(s.RewardField, fromData[$"{s.Room}/{s.Index}"].Split('/')[1]));
        }
    }

    [Fact]
    public void Adjacent_board_seeds_give_different_boards()
    {
        string[] pool = Enumerable.Range(0, 20).Select(i => $"O {i} 1").ToArray();
        var specs = Enumerable.Range(0, 20).Select(i => Spec("Pantry", i, "x")).ToList();
        var pools = Pools(("Pantry", pool));
        var a = BundleRewardShuffle.Apply(specs, 100, pools, _ => false).Select(s => s.RewardField).ToList();
        var b = BundleRewardShuffle.Apply(specs, 101, pools, _ => false).Select(s => s.RewardField).ToList();
        Assert.True(a.Zip(b).Count(p => p.First != p.Second) >= 10);
    }

    // Random rewards off (Jeff, 2026-11-10): vanilla's rewards, and our authored bundles draw
    // from what the board's vanilla bundles left unused.

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Standard =
        BundleRewardShuffle.StandardRoomSets(VanillaBundleBoard.Standard);

    private static BundleSpec Vanilla(string key)
    {
        string[] room = key.Split('/');
        return Spec(room[0], int.Parse(room[1]), VanillaBundleBoard.Standard[key].Split('/')[1]);
    }

    private static IReadOnlyList<BundleSpec> KeepVanilla(IReadOnlyList<BundleSpec> specs, ISet<int> authored, int seed = 1)
        => BundleRewardShuffle.KeepVanillaRewards(specs, seed, Standard, s => authored.Contains(s.Index));

    [Fact]
    public void An_authored_bundle_takes_its_rooms_unused_reward_even_before_the_retained_owner_of_its_own()
    {
        // Preserver's (Keg, BO 12) sits at Pantry/3 ahead of Artisan at Pantry/5, which keeps its
        // Keg; the only Pantry reward left unused is Quality Crops' Preserves Jar.
        var specs = new[]
        {
            Vanilla("Pantry/0"), Vanilla("Pantry/1"), Vanilla("Pantry/2"), Spec("Pantry", 3, "BO 12 1"),
            Vanilla("Pantry/4"), Vanilla("Pantry/5"),
        };
        var after = KeepVanilla(specs, new HashSet<int> { 3 });
        Assert.Equal("BO 15 1", after[3].RewardField);
        Assert.Equal(specs.Where((_, i) => i != 3).Select(s => s.RewardField), after.Where((_, i) => i != 3).Select(s => s.RewardField));
    }

    [Fact]
    public void An_authored_bundle_in_a_used_up_room_takes_another_rooms_unused_reward()
    {
        var specs = Enumerable.Range(0, 6).Select(i => Vanilla($"Pantry/{i}")).Append(Spec("Pantry", 9001, "BO 15 1")).ToList();
        var others = Standard.Where(kv => kv.Key != "Pantry").SelectMany(kv => kv.Value).ToHashSet();
        foreach (int seed in Enumerable.Range(0, 10))
            Assert.Contains(KeepVanilla(specs, new HashSet<int> { 9001 }, seed)[6].RewardField, others);
    }

    [Fact]
    public void Authored_bundles_never_take_the_same_reward()
    {
        var specs = Enumerable.Range(0, 4).Select(i => Spec("Pantry", i, "BO 15 1")).ToList();
        var after = KeepVanilla(specs, new HashSet<int> { 0, 1, 2, 3 });
        Assert.Equal(4, after.Select(s => s.RewardField).Distinct().Count());
        Assert.All(after, s => Assert.Contains(s.RewardField, Standard["Pantry"]));
    }

    [Fact]
    public void An_authored_bundle_keeps_its_own_reward_when_every_set_is_used()
    {
        var sets = Pools(("Pantry", new[] { "A" }));
        var specs = new[] { Spec("Pantry", 0, "A"), Spec("Pantry", 1, "own") };
        var after = BundleRewardShuffle.KeepVanillaRewards(specs, 1, sets, s => s.Index == 1);
        Assert.Equal(new[] { "A", "own" }, after.Select(s => s.RewardField));
    }

    [Fact]
    public void Random_rewards_off_leaves_the_vault_alone_and_never_hands_out_its_rewards()
    {
        Assert.DoesNotContain("Vault", Standard.Keys);
        Assert.DoesNotContain("Abandoned Joja Mart", Standard.Keys);
        var vault = Vanilla("Vault/24");
        Assert.Same(vault, KeepVanilla(new[] { vault }, new HashSet<int> { 24 })[0]);
    }
}
