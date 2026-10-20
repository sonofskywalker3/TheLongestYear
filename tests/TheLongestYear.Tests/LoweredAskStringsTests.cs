using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>A lowered goal names its full ask in brackets, "x33 (44)", with no "was".</summary>
[Collection("i18n")]
public class LoweredAskStringsTests
{
    [Fact]
    public void Full_tag_is_the_full_ask_in_brackets()
        => Assert.Equal(" (44)", Strings.Get("goal.full-tag", new Dictionary<string, string> { ["full"] = "44" }));

    [Fact]
    public void Bundle_page_line_names_both_asks_without_was()
        => Assert.Equal("Asks 33 (44) until the week ends.", Strings.Get("goal.lowered-line",
            new Dictionary<string, string> { ["now"] = "33", ["full"] = "44" }));
}
