using System;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus 1122619 (2026-08-24): BundleOptionPatch located the vanilla dropdown's
/// apply-callback by counting non-label options, but AGO's header rows use the DEFAULT
/// style, so the index was off by one — the wrong callback was replaced, vanilla's
/// 2-entry capture stayed live, and picking Remixed (index 2) threw on OK. The fix finds
/// the callback by what its closure actually captured; these tests cover that helper.</summary>
public class DelegateClosuresTests
{
    private sealed class Widget { public int SelectedOption; }

    [Fact]
    public void References_TrueWhenClosureCapturesTheNeedle()
    {
        var widget = new Widget();
        Action action = () => widget.SelectedOption = 2;
        Assert.True(DelegateClosures.References(action, widget));
    }

    [Fact]
    public void References_FalseForOtherCaptures_StaticLambdas_AndNull()
    {
        var widget = new Widget();
        var other = new Widget();
        Action captured = () => other.SelectedOption = 1;
        Action bare = static () => Console.Write("");
        Assert.False(DelegateClosures.References(captured, widget));
        Assert.False(DelegateClosures.References(bare, widget));
        Assert.False(DelegateClosures.References(null, widget));
    }

    [Fact]
    public void References_FindsNeedleOneLevelDeep_NestedClosureObjects()
    {
        var widget = new Widget();
        Action inner = () => widget.SelectedOption = 3;
        // Outer lambda captures the INNER DELEGATE, whose Target holds the widget.
        Action outer = () => inner();
        Assert.True(DelegateClosures.References(outer, widget));
    }
}
