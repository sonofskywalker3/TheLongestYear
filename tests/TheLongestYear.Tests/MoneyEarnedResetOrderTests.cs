using System;
using System.IO;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The rewind zeroes money earned, and the starting gold must not count as this year's
/// earnings. The game's Money setter adds any rise in gold to money earned, so the zeroing has to
/// run after FarmerReset sets the starting gold and after every restore step that might hand out
/// gold. The reset sequence lives in the game-side WorldResetService, which these tests cannot
/// load, so this guards the order in its source.</summary>
public class MoneyEarnedResetOrderTests
{
    private static string PerformResetBody()
    {
        string path = Path.Combine(I18nFixture.RepoRoot, "src", "TheLongestYear", "Loop", "WorldResetService.cs");
        string src = File.ReadAllText(path);
        int start = src.IndexOf("public void PerformReset()", StringComparison.Ordinal);
        Assert.True(start >= 0, "PerformReset not found");
        int end = src.IndexOf("In-place reset: complete.", start, StringComparison.Ordinal);
        Assert.True(end > start, "PerformReset's closing log line not found");
        return src.Substring(start, end - start);
    }

    [Fact]
    public void Money_earned_is_zeroed_after_the_starting_gold_is_set()
    {
        string body = PerformResetBody();
        int farmer = body.IndexOf("ResetFarmer(ctx);", StringComparison.Ordinal);
        int zero = body.IndexOf("_farmerReset.ResetMoneyEarned", StringComparison.Ordinal);

        Assert.True(farmer >= 0, "ResetFarmer (which sets the starting gold) not found in PerformReset");
        Assert.True(zero >= 0, "ResetMoneyEarned not found in PerformReset");
        Assert.True(zero > farmer, "money earned is zeroed before the starting gold is set");
    }

    [Fact]
    public void Money_earned_is_the_last_restore_step()
    {
        string body = PerformResetBody();
        int zero = body.IndexOf("_farmerReset.ResetMoneyEarned", StringComparison.Ordinal);
        string after = body.Substring(zero + "_farmerReset.ResetMoneyEarned".Length);

        Assert.DoesNotContain("Restore", after);
        Assert.DoesNotContain("Reapply", after);
    }

    [Fact]
    public void The_starting_gold_is_set_in_FarmerReset_Apply()
    {
        string path = Path.Combine(I18nFixture.RepoRoot, "src", "TheLongestYear", "Loop", "FarmerReset.cs");
        string src = File.ReadAllText(path);

        Assert.Contains("p.Money = baseline.StartingGold;", src);
    }
}
