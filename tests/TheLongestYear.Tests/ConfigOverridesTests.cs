using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ConfigOverridesTests
{
    private static readonly IReadOnlyDictionary<string, string> SeasonDefaults = new Dictionary<string, string>
    {
        ["(O)24"] = "Spring",
        ["(O)254"] = "summer",
        ["(O)999"] = "NotASeason",
    };

    [Fact]
    public void Enum_defaults_parse_case_insensitively_and_bad_defaults_drop_silently()
    {
        var invalid = new List<string>();
        var merged = ConfigOverrides.MergeEnum<Season>(SeasonDefaults, null, (k, v) => invalid.Add(k));

        Assert.Equal(Season.Spring, merged["(O)24"]);
        Assert.Equal(Season.Summer, merged["(O)254"]);
        Assert.False(merged.ContainsKey("(O)999"));
        Assert.Empty(invalid);
    }

    [Fact]
    public void Enum_user_value_overrides_the_default_and_adds_new_keys()
    {
        var user = new Dictionary<string, string> { ["(O)24"] = "fall", ["(O)88"] = "Winter" };
        var merged = ConfigOverrides.MergeEnum<Season>(SeasonDefaults, user, (k, v) => { });

        Assert.Equal(Season.Fall, merged["(O)24"]);
        Assert.Equal(Season.Winter, merged["(O)88"]);
        Assert.Equal(Season.Summer, merged["(O)254"]);
    }

    [Fact]
    public void Enum_invalid_user_value_is_reported_and_keeps_the_default()
    {
        var invalid = new List<(string Key, string Value)>();
        var user = new Dictionary<string, string> { ["(O)24"] = "Monsoon" };
        var merged = ConfigOverrides.MergeEnum<Season>(SeasonDefaults, user, (k, v) => invalid.Add((k, v)));

        Assert.Equal(Season.Spring, merged["(O)24"]);
        Assert.Equal(new[] { ("(O)24", "Monsoon") }, invalid);
    }

    [Fact]
    public void Enum_merge_works_for_themes_too()
    {
        var defaults = new Dictionary<string, string>();
        var user = new Dictionary<string, string> { ["(O)24"] = Theme.Farming.ToString().ToLowerInvariant() };
        var merged = ConfigOverrides.MergeEnum<Theme>(defaults, user, (k, v) => { });
        Assert.Equal(Theme.Farming, merged["(O)24"]);
    }

    private static readonly IReadOnlyDictionary<string, int[]> QuotaDefaults = new Dictionary<string, int[]>
    {
        ["Pantry"] = new[] { 1, 2, 3, 4 },
    };

    [Fact]
    public void Quotas_copy_the_defaults_so_callers_cannot_edit_them()
    {
        var merged = ConfigOverrides.MergeQuotas(QuotaDefaults, null, Calendar.MonthsPerYear, (k, v, p) => { });

        Assert.Equal(new[] { 1, 2, 3, 4 }, merged["Pantry"]);
        merged["Pantry"][0] = 99;
        Assert.Equal(1, QuotaDefaults["Pantry"][0]);
    }

    [Fact]
    public void Quota_user_array_overrides_the_default_and_is_copied()
    {
        int[] mine = { 0, 1, 2, 6 };
        var user = new Dictionary<string, int[]> { ["Pantry"] = mine };
        var merged = ConfigOverrides.MergeQuotas(QuotaDefaults, user, Calendar.MonthsPerYear, (k, v, p) => { });

        Assert.Equal(mine, merged["Pantry"]);
        Assert.NotSame(mine, merged["Pantry"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Quota_of_the_wrong_length_is_reported_and_skipped(int length)
    {
        var problems = new List<ConfigOverrides.QuotaProblem>();
        var user = new Dictionary<string, int[]> { ["Pantry"] = new int[length] };
        var merged = ConfigOverrides.MergeQuotas(QuotaDefaults, user, Calendar.MonthsPerYear, (k, v, p) => problems.Add(p));

        Assert.Equal(new[] { 1, 2, 3, 4 }, merged["Pantry"]);
        Assert.Equal(new[] { ConfigOverrides.QuotaProblem.WrongLength }, problems);
    }

    [Fact]
    public void Null_quota_is_reported_as_wrong_length()
    {
        var problems = new List<ConfigOverrides.QuotaProblem>();
        var user = new Dictionary<string, int[]> { ["Crafts"] = null! };
        var merged = ConfigOverrides.MergeQuotas(QuotaDefaults, user, Calendar.MonthsPerYear, (k, v, p) => problems.Add(p));

        Assert.False(merged.ContainsKey("Crafts"));
        Assert.Equal(new[] { ConfigOverrides.QuotaProblem.WrongLength }, problems);
    }

    [Fact]
    public void Quota_with_a_negative_count_is_reported_and_skipped()
    {
        var problems = new List<(string Key, ConfigOverrides.QuotaProblem Problem)>();
        var user = new Dictionary<string, int[]> { ["Pantry"] = new[] { 1, -1, 3, 4 } };
        var merged = ConfigOverrides.MergeQuotas(QuotaDefaults, user, Calendar.MonthsPerYear, (k, v, p) => problems.Add((k, p)));

        Assert.Equal(new[] { 1, 2, 3, 4 }, merged["Pantry"]);
        Assert.Equal(new[] { ("Pantry", ConfigOverrides.QuotaProblem.Negative) }, problems);
    }
}
