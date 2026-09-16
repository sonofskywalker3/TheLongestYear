using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-09-16: one setting governs every item a loop can only give once. The
/// legendary fish, the two gift-box books and the Golden Pumpkin stay at one however high the
/// Stack size dial goes; turning the setting off lets them scale like anything else. Items that
/// never stack (UnstackableAsks) are a separate, unconditional rule.</summary>
public class OncePerLoopAsksTests
{
    private static DifficultyProfile Profile(DifficultyStep step, bool onceAsksOne)
        => DifficultyResolver.Resolve(
            new DifficultySettings { StackSize = step },
            new GameplayConfig { OncePerLoopAsksOne = onceAsksOne });

    private static BundleSpec Spec(string room, params (string Id, int Stack)[] slots)
        => new(room, 1, "Test", "Test", "O 12 1", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, 0)).ToList());

    [Theory]
    [InlineData("(O)163")]         // Legend
    [InlineData("(O)159")]         // Crimsonfish
    [InlineData("(O)160")]         // Angler
    [InlineData("(O)775")]         // Glacierfish
    [InlineData("(O)682")]         // Mutant Carp
    [InlineData("(O)Book_Trash")]  // The Alleyway Buffet, one gift box
    [InlineData("(O)Book_Marlon")] // Mapping Cave Systems, one gift box
    [InlineData("(O)373")]         // Golden Pumpkin, the Spirit's Eve maze
    public void The_list_is_the_five_legendaries_the_two_gift_box_books_and_the_golden_pumpkin(string id)
        => Assert.True(OncePerLoopAsks.IsOncePerLoop(id));

    [Theory]
    [InlineData("(O)Book_Speed")]           // the bookseller restocks it every visit
    [InlineData("(O)Book_PriceCatalogue")]
    [InlineData("(O)Book_Bombs")]           // the Dwarf restocks daily
    [InlineData("(O)PurpleBook")]
    [InlineData("(O)797")]                  // Pearl, not on any board
    [InlineData("(O)388")]
    [InlineData("(H)8")]                    // unstackable, a different rule
    public void Everything_else_is_not(string id)
        => Assert.False(OncePerLoopAsks.IsOncePerLoop(id));

    [Fact]
    public void Bare_ids_count_too()
        => Assert.True(OncePerLoopAsks.IsOncePerLoop("163"));

    [Fact]
    public void ClampStack_holds_a_once_per_loop_item_at_one_when_enabled()
    {
        Assert.Equal(1, OncePerLoopAsks.ClampStack("(O)163", 3, enabled: true));
        Assert.Equal(1, OncePerLoopAsks.ClampStack("(O)Book_Trash", 2, enabled: true));
        Assert.Equal(3, OncePerLoopAsks.ClampStack("(O)388", 3, enabled: true));
    }

    [Fact]
    public void ClampStack_leaves_the_ask_alone_when_disabled()
        => Assert.Equal(3, OncePerLoopAsks.ClampStack("(O)163", 3, enabled: false));

    [Fact]
    public void The_setting_defaults_on_and_resolves_into_the_profile()
    {
        Assert.True(new GameplayConfig().OncePerLoopAsksOne);
        Assert.True(Profile(DifficultyStep.Normal, onceAsksOne: true).OncePerLoopAsksOne);
        Assert.False(Profile(DifficultyStep.Normal, onceAsksOne: false).OncePerLoopAsksOne);
    }

    [Theory]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void Engine_board_holds_once_per_loop_items_at_one_while_ordinary_items_scale(DifficultyStep step)
    {
        var scaled = StackScaling.Apply(
            Spec("Bulletin Board", ("(O)Book_Trash", 1), ("(O)373", 1), ("(O)163", 1), ("(O)388", 10)),
            Profile(step, onceAsksOne: true));

        Assert.Equal(1, scaled.Slots[0].Stack);
        Assert.Equal(1, scaled.Slots[1].Stack);
        Assert.Equal(1, scaled.Slots[2].Stack);
        Assert.True(scaled.Slots[3].Stack > 10);
    }

    [Fact]
    public void Engine_board_scales_once_per_loop_items_when_the_setting_is_off()
    {
        var scaled = StackScaling.Apply(
            Spec("Fish Tank", ("(O)163", 1), ("(O)Book_Marlon", 1)),
            Profile(DifficultyStep.Hard, onceAsksOne: false));

        Assert.Equal(2, scaled.Slots[0].Stack);
        Assert.Equal(2, scaled.Slots[1].Stack);
    }

    [Fact]
    public void Vanilla_board_pass_holds_once_per_loop_items_at_one_when_enabled()
    {
        const string key = "Bulletin Board/31";
        const string value = "Book/O 421 1/(O)Book_Trash 1 0 (O)388 10 0/2/2//Book";
        IDictionary<string, string> Board() => new Dictionary<string, string> { [key] = value };

        var on = BundleParsing.Parse(key, VanillaBoardDifficultyPass.Apply(
            Board(), Profile(DifficultyStep.Hard, onceAsksOne: true), new BundleGenerationTuning(), 7)[key]);
        var off = BundleParsing.Parse(key, VanillaBoardDifficultyPass.Apply(
            Board(), Profile(DifficultyStep.Hard, onceAsksOne: false), new BundleGenerationTuning(), 7)[key]);

        Assert.Equal(new[] { 1, 15 }, on.Ingredients.Select(i => i.Stack));
        Assert.Equal(new[] { 2, 15 }, off.Ingredients.Select(i => i.Stack));
    }

    [Fact]
    public void RepairBundleValue_lowers_once_per_loop_asks_above_one()
    {
        const string value = "Book/O 421 1/(O)Book_Trash 2 0 (O)Book_Speed 2 0 (O)373 3 0/3/2//Book";
        Assert.Equal(
            "Book/O 421 1/(O)Book_Trash 1 0 (O)Book_Speed 2 0 (O)373 1 0/3/2//Book",
            OncePerLoopAsks.RepairBundleValue(value));
    }

    [Fact]
    public void RepairBundleValue_returns_null_when_nothing_needs_lowering()
    {
        Assert.Null(OncePerLoopAsks.RepairBundleValue("Book/O 421 1/(O)Book_Trash 1 0 (O)Book_Speed 2 0/3/2//Book"));
        Assert.Null(OncePerLoopAsks.RepairBundleValue("Vault/-1 2500 2500/4/1"));
    }
}
