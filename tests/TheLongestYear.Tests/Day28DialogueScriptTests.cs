using System.Linq;
using TheLongestYear.Core.Day28;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class Day28DialogueScriptTests
{
    public Day28DialogueScriptTests(I18nFixture _) { }

    [Fact]
    public void Substitutes_player_name_for_at_symbol()
    {
        var pages = Day28DialogueScript.ToPages("Hello, @.", "Rodger");
        Assert.Single(pages);
        Assert.Equal("Hello, Rodger.", pages[0]);
    }

    [Fact]
    public void Splits_on_page_break_token()
    {
        var pages = Day28DialogueScript.ToPages("First page.#$b#Second page.", "X");
        Assert.Equal(2, pages.Count);
        Assert.Equal("First page.", pages[0]);
        Assert.Equal("Second page.", pages[1]);
    }

    [Fact]
    public void Strips_pose_emote_codes_and_stray_hashes()
    {
        var pages = Day28DialogueScript.ToPages("Great job!$h", "X");
        Assert.Single(pages);
        Assert.Equal("Great job!", pages[0]);
        Assert.DoesNotContain("$", pages[0]);
        Assert.DoesNotContain("#", pages[0]);
    }

    [Fact]
    public void Collapses_runs_of_three_or_more_spaces_left_by_stripping_codes()
    {
        // A pose code between two spaces leaves 4 spaces after stripping; a single double-space
        // pass would leave 2. The whole run must collapse to one space.
        var pages = Day28DialogueScript.ToPages("word  $h  word", "X");
        Assert.Single(pages);
        Assert.Equal("word word", pages[0]);
    }

    [Fact]
    public void Empty_or_null_input_yields_no_pages()
    {
        Assert.Empty(Day28DialogueScript.ToPages("", "X"));
        Assert.Empty(Day28DialogueScript.ToPages(null!, "X"));
    }

    [Fact]
    public void Real_fail_dialogue_parses_to_clean_code_free_pages()
    {
        var pages = Day28DialogueScript.ToPages(Day28CutsceneContent.FailDialogue, "Rodger");
        Assert.NotEmpty(pages);
        Assert.All(pages, p =>
        {
            Assert.DoesNotContain("$", p);   // no pose codes
            Assert.DoesNotContain("#", p);   // no break/markup leftovers
            Assert.DoesNotContain("@", p);   // name substituted
        });
        Assert.Contains(pages, p => p.Contains("Rodger"));
    }

    [Fact]
    public void Real_continue_dialogue_parses_to_clean_code_free_pages()
    {
        var pages = Day28DialogueScript.ToPages(Day28CutsceneContent.ContinueDialogue, "Rodger");
        Assert.NotEmpty(pages);
        Assert.All(pages, p =>
        {
            Assert.DoesNotContain("$", p);
            Assert.DoesNotContain("#", p);
            Assert.DoesNotContain("@", p);
        });
    }
}
