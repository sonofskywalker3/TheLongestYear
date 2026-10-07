using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class BundleRewardShuffleTests
{
    private static readonly string[] Pool = BundleRewardShuffle.CleanPool(new[] { "O 495 30", "BO 21 1", "O 472 10", "", "O 495 30" }).ToArray();
    private static BundleSpec Spec(string room, int i, string reward)
        => new(room, i, "N" + i, "N" + i, reward, 0, 4, new List<BundleSlotSpec>());

    [Fact]
    public void The_pool_is_cleaned_and_sorted()
        => Assert.Equal(new[] { "BO 21 1", "O 472 10", "O 495 30" }, Pool);

    [Fact]
    public void The_pool_drops_rewards_with_a_slash()
        => Assert.Equal(new[] { "O 1 1" }, BundleRewardShuffle.CleanPool(new[] { "O 1 1", "O 2/1", null! }));

    private static bool SkipJoja(string room) => BundleRewardShuffle.SkipsRoom(room);

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
        var after = BundleRewardShuffle.Apply(specs, 42, Pool, SkipJoja);
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
        var after = BundleRewardShuffle.ApplyToData(board, 3, Pool, SkipJoja);
        var a = after["Vault/23"].Split('/'); var b = board["Vault/23"].Split('/');
        Assert.Contains(a[1], Pool);
        Assert.Equal(b.Where((_, i) => i != 1), a.Where((_, i) => i != 1));
        Assert.Equal(board["Abandoned Joja Mart/36"], after["Abandoned Joja Mart/36"]);
    }

    [Fact]
    public void The_same_board_seed_gives_the_same_rewards()
    {
        var specs = new[] { Spec("Pantry", 0, "x"), Spec("Pantry", 1, "x") };
        Assert.Equal(BundleRewardShuffle.Apply(specs, 7, Pool, _ => false).Select(s => s.RewardField),
                     BundleRewardShuffle.Apply(specs, 7, Pool, _ => false).Select(s => s.RewardField));
    }

    [Fact]
    public void Data_form_replaces_only_the_reward_field()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0/0/4//Spring Crops" };
        var after = BundleRewardShuffle.ApplyToData(board, 3, Pool, _ => false);
        var a = after["Pantry/0"].Split('/'); var b = board["Pantry/0"].Split('/');
        Assert.Contains(a[1], Pool);
        Assert.Equal(b.Where((_, i) => i != 1), a.Where((_, i) => i != 1));
    }

    [Fact]
    public void Data_form_leaves_skipped_rooms_byte_identical()
    {
        var board = new Dictionary<string, string> { ["Abandoned Joja Mart/36"] = "The Missing//348 1 1/1/1//The Missing" };
        var after = BundleRewardShuffle.ApplyToData(board, 3, Pool, SkipJoja);
        Assert.Equal(board["Abandoned Joja Mart/36"], after["Abandoned Joja Mart/36"]);
    }

    [Fact]
    public void Spec_and_data_forms_agree_for_the_same_key()
    {
        var spec = BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x") }, 11, Pool, _ => false)[0];
        var data = BundleRewardShuffle.ApplyToData(new Dictionary<string, string> { ["Pantry/0"] = "n/x/1 1 0/0/1//n" }, 11, Pool, _ => false);
        Assert.Equal(spec.RewardField, data["Pantry/0"].Split('/')[1]);
    }

    [Fact]
    public void An_empty_pool_leaves_rewards_alone()
        => Assert.Equal("x", BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x") }, 1, Array.Empty<string>(), _ => false)[0].RewardField);

    [Fact]
    public void Picks_across_one_board_are_spread_over_the_pool()
    {
        // .NET's seeded Random gives near-identical first draws for adjacent seeds, so a weak
        // seed mix would hand every bundle on a board the same reward. 30 keys, 20-reward pool.
        string[] pool = BundleRewardShuffle.CleanPool(Enumerable.Range(0, 20).Select(i => $"O {i} 1")).ToArray();
        string[] rooms = { "Pantry", "Crafts Room", "Fish Tank", "Boiler Room", "Bulletin Board" };
        foreach (int boardSeed in new[] { 0, 1, 2, 12345 })
        {
            var picks = rooms.SelectMany(r => Enumerable.Range(0, 6).Select(i => BundleRewardShuffle.RewardFor(boardSeed, $"{r}/{i}", pool))).ToList();
            Assert.True(picks.Distinct().Count() >= 10, $"seed {boardSeed}: only {picks.Distinct().Count()} distinct rewards");
        }
    }

    [Fact]
    public void Adjacent_board_seeds_give_different_boards()
    {
        string[] pool = BundleRewardShuffle.CleanPool(Enumerable.Range(0, 20).Select(i => $"O {i} 1")).ToArray();
        var keys = Enumerable.Range(0, 20).Select(i => $"Pantry/{i}").ToList();
        var a = keys.Select(k => BundleRewardShuffle.RewardFor(100, k, pool)).ToList();
        var b = keys.Select(k => BundleRewardShuffle.RewardFor(101, k, pool)).ToList();
        Assert.True(a.Zip(b).Count(p => p.First != p.Second) >= 10);
    }
}
