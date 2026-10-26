using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What the player is told, once, after the hand-over: the retired upgrades (highest
/// per-skill tier for each skill, then Junimo Insight when owned), the JP refunded, and whether
/// Insight was among them (that player also hears about Mastery Boost). Persisted in
/// <see cref="MetaState.PendingXpHandoverNotice"/> until a shrine shows it.</summary>
public sealed class XpHandoverNotice
{
    public List<string> Retired { get; set; } = new();
    public long Refund { get; set; }
    public bool HadInsight { get; set; }
}

/// <summary>The hand-over from the retired XP upgrades to Exp Boost and Mastery Boost
/// (Jeff, 2026-10-26, after tanky24u's feedback).
///
/// The 20 per-skill rows (xp_mult_&lt;slug&gt;_1..4) and Junimo Insight (xp_mult_all) left the
/// shop at once. A save that owns any of them keeps them working until the end of the loop
/// (<see cref="XpMultiplierRules"/> still reads them). At the next rewind this removes them and
/// refunds the JP, and leaves a notice for the next shrine to show.
///
/// The refund is the retired catalog base price times the loop's shrine price factor, rounded per
/// row exactly as a purchase is (<see cref="UpgradePricing"/>). MetaState never recorded the price
/// actually paid, so this is the closest honest figure: it matches what was charged unless the
/// difficulty's shrine prices changed between the purchase and the rewind.
///
/// Runs at most once in effect: once the ids are gone there is nothing left to refund.</summary>
public static class XpUpgradeHandover
{
    /// <summary>Retired per-skill prices, indexed [1..4].</summary>
    private static readonly long[] RetiredPerSkillCosts = { 0, 150, 350, 650, 1000 };

    public const long RetiredInsightCost = 3000;

    private static readonly string[] SkillSlugs = { "farming", "fishing", "foraging", "mining", "combat" };

    /// <summary>The 21 retired ids, in the order the notice lists them.</summary>
    public static IReadOnlyList<string> RetiredIds { get; } = BuildRetiredIds();

    private static IReadOnlyList<string> BuildRetiredIds()
    {
        var ids = new List<string>();
        foreach (string slug in SkillSlugs)
            for (int tier = 1; tier <= XpMultiplierRules.LegacyMaxTier; tier++)
                ids.Add(XpMultiplierRules.LegacySkillId(slug, tier));
        ids.Add(XpMultiplierRules.LegacyInsightId);
        return ids;
    }

    private static readonly HashSet<string> RetiredSet = new(RetiredIds, StringComparer.Ordinal);

    public static bool IsRetired(string? id) => id != null && RetiredSet.Contains(id);

    /// <summary>True when this save still owns any retired XP upgrade.</summary>
    public static bool IsOwed(MetaState meta)
        => meta?.OwnedUpgrades != null && meta.OwnedUpgrades.Any(IsRetired);

    /// <summary>What a retired row cost in the catalog. 0 for anything not retired, so a stray
    /// id can never mint points.</summary>
    public static long BaseCostOf(string? id)
    {
        if (!IsRetired(id)) return 0;
        if (string.Equals(id, XpMultiplierRules.LegacyInsightId, StringComparison.Ordinal))
            return RetiredInsightCost;
        int tier = id![id.Length - 1] - '0';
        return RetiredPerSkillCosts[tier];
    }

    /// <summary>The refund for one retired row at this price factor, rounded like a purchase.</summary>
    public static long RefundOf(string id, double priceFactor)
        => UpgradePricing.EffectiveCost(
            new UpgradeDefinition(id, UpgradeCategory.Efficiency, BaseCostOf(id)), priceFactor);

    public sealed class Result
    {
        public List<string> Removed { get; } = new();
        public long Refund { get; set; }
    }

    /// <summary>Remove every retired id this save owns, add the refund to its JP and leave (or
    /// add to) the pending notice. A save with nothing retired is untouched.</summary>
    public static Result Apply(MetaState meta, double priceFactor)
    {
        var result = new Result();
        if (!IsOwed(meta)) return result;

        foreach (string id in RetiredIds)
        {
            if (!meta.OwnedUpgrades.Contains(id, StringComparer.Ordinal)) continue;
            result.Refund += RefundOf(id, priceFactor);
            result.Removed.Add(id);
        }
        meta.OwnedUpgrades.RemoveAll(IsRetired);
        meta.JunimoPoints += result.Refund;

        var notice = meta.PendingXpHandoverNotice ?? new XpHandoverNotice();
        notice.Refund += result.Refund;
        foreach (string id in HighestPerSkill(result.Removed))
            if (!notice.Retired.Contains(id, StringComparer.Ordinal))
                notice.Retired.Add(id);
        if (result.Removed.Contains(XpMultiplierRules.LegacyInsightId, StringComparer.Ordinal))
            notice.HadInsight = true;
        meta.PendingXpHandoverNotice = notice;
        return result;
    }

    /// <summary>Take the pending notice (null when none), clearing it so it shows once.</summary>
    public static XpHandoverNotice? TakeNotice(MetaState meta)
    {
        if (meta == null) return null;
        XpHandoverNotice? notice = meta.PendingXpHandoverNotice;
        meta.PendingXpHandoverNotice = null;
        return notice;
    }

    /// <summary>The highest removed tier for each skill (skill order), then Insight.</summary>
    private static IEnumerable<string> HighestPerSkill(List<string> removed)
    {
        foreach (string slug in SkillSlugs)
            for (int tier = XpMultiplierRules.LegacyMaxTier; tier >= 1; tier--)
            {
                string id = XpMultiplierRules.LegacySkillId(slug, tier);
                if (removed.Contains(id, StringComparer.Ordinal))
                {
                    yield return id;
                    break;
                }
            }
        if (removed.Contains(XpMultiplierRules.LegacyInsightId, StringComparer.Ordinal))
            yield return XpMultiplierRules.LegacyInsightId;
    }

    private const string SkillKeyPrefix = "skill.";

    /// <summary>The player-facing name of a retired id, from the strings the old shop rows used.</summary>
    public static string RetiredName(string id)
    {
        if (string.Equals(id, XpMultiplierRules.LegacyInsightId, StringComparison.Ordinal))
            return Strings.Get("upgrade.xp_mult_all.name");
        int tier = id[id.Length - 1] - '0';
        string slug = id.Substring("xp_mult_".Length, id.Length - "xp_mult_".Length - 2);
        return Strings.Get("upgrade-tpl.xp-mult.name", new Dictionary<string, string>
        {
            ["skill"] = Strings.Get(SkillKeyPrefix + slug),
            ["percent"] = (XpMultiplierRules.LegacyPercentPerTier * tier).ToString(),
        });
    }

    /// <summary>The notice's text, ready to show.</summary>
    public static string NoticeText(XpHandoverNotice notice)
        => Strings.Get(notice.HadInsight ? "notice.xp-handover.insight" : "notice.xp-handover", new Dictionary<string, string>
        {
            ["list"] = string.Join(", ", notice.Retired.Select(RetiredName)),
            ["jp"] = notice.Refund.ToString("N0"),
        });
}
