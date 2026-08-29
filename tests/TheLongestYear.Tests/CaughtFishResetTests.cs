using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CaughtFishResetTests
{
    [Fact]
    public void Only_catch_limited_fish_are_cleared()
    {
        var limited = new[] { "(O)163", "(O)682" };
        var caught = new[] { "(O)163", "(O)128", "(O)682" };
        Assert.Equal(new[] { "(O)163", "(O)682" }, CaughtFishReset.IdsToClear(limited, caught).OrderBy(x => x));
    }

    [Fact]
    public void No_catch_limited_ids_caught_returns_empty()
    {
        var limited = new[] { "(O)163", "(O)682" };
        var caught = new[] { "(O)128" };
        Assert.Empty(CaughtFishReset.IdsToClear(limited, caught));
    }

    [Fact]
    public void Empty_caught_returns_empty()
    {
        var limited = new[] { "(O)163" };
        Assert.Empty(CaughtFishReset.IdsToClear(limited, System.Array.Empty<string>()));
    }
}
