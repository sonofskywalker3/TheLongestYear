using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// The night-festival clock remembers the festival map it slowed so it can undo the write. Taking the
/// undo (every tick with nothing to slow, and FestivalTimeFlow.Reset on return to title) must drop
/// the reference, and must never reset a value someone else set.
/// </summary>
public class RememberedWriteTests
{
    private sealed class FakeLocation
    {
        public int ExtraMs;
    }

    [Fact]
    public void Undo_returns_the_target_while_it_still_holds_our_value()
    {
        var loc = new FakeLocation { ExtraMs = 700 };
        var w = new RememberedWrite<FakeLocation>();
        w.Remember(loc, 700);

        Assert.Same(loc, w.TakeForUndo(l => l.ExtraMs));
    }

    [Fact]
    public void Undo_leaves_a_value_someone_else_set()
    {
        var loc = new FakeLocation { ExtraMs = 300 }; // e.g. a mine level's own setting
        var w = new RememberedWrite<FakeLocation>();
        w.Remember(loc, 700);

        Assert.Null(w.TakeForUndo(l => l.ExtraMs));
    }

    [Fact]
    public void Taking_the_undo_always_drops_the_reference()
    {
        var w = new RememberedWrite<FakeLocation>();
        w.Remember(new FakeLocation { ExtraMs = 300 }, 700);

        w.TakeForUndo(l => l.ExtraMs);

        Assert.Null(w.Target);
        Assert.Equal(0, w.Value);
        Assert.Null(w.TakeForUndo(l => l.ExtraMs)); // a second reset does nothing
    }

    [Fact]
    public void Nothing_remembered_means_nothing_to_undo()
    {
        Assert.Null(new RememberedWrite<FakeLocation>().TakeForUndo(l => l.ExtraMs));
    }
}
