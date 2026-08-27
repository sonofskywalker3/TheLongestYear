using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class DejaVuLinesTests
{
    private readonly I18nFixture _fixture;
    public DejaVuLinesTests(I18nFixture fixture) => _fixture = fixture;

    private static readonly string[] Villagers =
    {
        "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf", "Elliott", "Emily", "Evelyn",
        "George", "Gus", "Haley", "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah", "Leo", "Lewis",
        "Linus", "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin", "Sam", "Sandy", "Sebastian",
        "Shane", "Vincent", "Willy", "Wizard",
    };

    [Fact]
    public void Villager_pool_is_used_when_present_else_default()
    {
        var keys = _fixture.Map.Keys.ToList();
        Assert.Equal(new[] { "dejavu.pierre.1.1" }, DejaVuLines.KeysFor("Pierre", 1, keys));
        Assert.Equal(new[] { "dejavu.default.2.1", "dejavu.default.2.2", "dejavu.default.2.3" },
            DejaVuLines.KeysFor("SomeModNpc", 2, keys));
    }

    [Fact]
    public void Pick_resolves_text_and_every_villager_has_both_tiers()
    {
        var keys = _fixture.Map.Keys.ToList();
        Assert.Equal("Have you shopped here before? I feel like I know your order.",
            DejaVuLines.Pick("Pierre", 1, keys, _ => 0));
        Assert.Equal("Being with you feels like the island. Safe.", DejaVuLines.Pick("Leo", 2, keys, _ => 0));
        Assert.Equal("Hey... you... Sorry, I swear I knew your name for a second.",
            DejaVuLines.Pick("SomeModNpc", 1, keys, _ => 2));
        foreach (string v in Villagers)
        {
            Assert.Contains($"dejavu.{v.ToLowerInvariant()}.1.1", keys);
            Assert.Contains($"dejavu.{v.ToLowerInvariant()}.2.1", keys);
        }
    }

    [Fact]
    public void Lines_never_explain_the_loop_and_have_no_em_dashes()
    {
        var all = DejaVuLines.AllKeys(_fixture.Map.Keys.ToList()).ToList();
        Assert.Equal(6 + Villagers.Length * 2, all.Count);
        foreach (string key in all)
        {
            string text = _fixture.Map[key].ToLowerInvariant();
            Assert.DoesNotContain("—", text);
            Assert.DoesNotContain("loop", text);
            Assert.DoesNotContain("reset", text);
            Assert.DoesNotContain("junimo", text);
        }
    }
}
