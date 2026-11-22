using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class NarrationTests
{
    [Fact]
    public void Tracker_speaks_a_menu_once_then_only_when_the_focus_changes()
    {
        var tracker = new NarrationTracker();
        object hub = new object();
        object shrine = new object();

        Assert.Equal(NarrationCue.None, tracker.Observe(null, null));
        Assert.Equal(NarrationCue.MenuOpened, tracker.Observe(hub, 1));
        Assert.Equal(NarrationCue.None, tracker.Observe(hub, 1));
        Assert.Equal(NarrationCue.FocusChanged, tracker.Observe(hub, 2));
        Assert.Equal(NarrationCue.None, tracker.Observe(hub, 2));
        Assert.Equal(NarrationCue.MenuOpened, tracker.Observe(shrine, 2));
        // A menu that closed and came back (a notice over it was dismissed) is spoken again.
        Assert.Equal(NarrationCue.None, tracker.Observe(null, null));
        Assert.Equal(NarrationCue.MenuOpened, tracker.Observe(shrine, 2));
    }

    [Theory]
    [InlineData(new[] { "Pick a theme", "Pays 1.5x JP." }, "Pick a theme. Pays 1.5x JP.")]
    [InlineData(new[] { "Bonus:", "", null, "Wood" }, "Bonus: Wood.")]
    [InlineData(new[] { "Wrapped\nline", "  Done!  " }, "Wrapped line. Done!")]
    [InlineData(new string[0], "")]
    public void Sentences_joins_parts_with_sentence_ends(string[] parts, string expected)
        => Assert.Equal(expected, NarrationText.Sentences(parts));
}
