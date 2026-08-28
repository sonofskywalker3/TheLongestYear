using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for an artisan good (Data/Machines OutputRules + the machine's own recipe in
/// Data/CraftingRecipes): the cheapest qualifying input (recursively derived through the
/// composer's resolver), plus the machine's unlock effort, plus a step for how long it runs.
/// Minimum over every rule that outputs the item.</summary>
public static class ArtisanAvailability
{
    private const int MinutesPerDay = 1440;
    /// <summary>A run of this many days or more is the long step (Wine's 6.9 days, Casks, Aged Roe).</summary>
    private const int LongDays = 4;
    private const int DefaultUnlockEffort = 1;
    private const int MidSkillLevel = 4;
    private const int HighSkillLevel = 8;
    private const int QuestUnlockEffort = 3;
    private const int NoInputEffort = 0;
    private const int UnresolvedInputEffort = ItemAvailabilityModel.UnrecognisedEffort;
    private const string SkillPrefix = "s";
    private const string FriendshipPrefix = "f";

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        ItemEffort? best = null;
        foreach (RawMachineRule rule in data.MachineRules)
        {
            if (!rule.OutputItemIds.Contains(qualifiedId)) continue;
            (int inputEffort, string inputNote) = InputEffort(rule, data, effortOf);
            int machine = MachineUnlockEffort(
                data.MachineUnlocks.TryGetValue(rule.MachineItemId, out string? unlock) ? unlock : null);
            int time = TimeStep(rule.MinutesUntilReady, rule.DaysUntilReady);
            int effort = inputEffort + machine + time;
            if (best == null || effort < best.Effort)
                best = new ItemEffort(effort,
                    $"artisan, {rule.MachineItemId}: input {inputNote} ({inputEffort}) + machine {machine} + time {time}, effort {effort}");
        }
        return best;
    }

    private static (int Effort, string Note) InputEffort(RawMachineRule rule, EffortData data, Func<string, int?> effortOf)
    {
        if (!string.IsNullOrEmpty(rule.RequiredItemId))
        {
            string id = BundleParsing.NormalizeItemId(rule.RequiredItemId);
            return (effortOf(id) ?? UnresolvedInputEffort, id);
        }
        if (rule.RequiredTags.Count > 0)
        {
            int? cheapest = null;
            string cheapestId = "";
            foreach (string id in ContextTagMatcher.IdsMatchingAll(data.Objects, rule.RequiredTags))
            {
                int? e = effortOf(id);
                if (e != null && (cheapest == null || e < cheapest)) { cheapest = e; cheapestId = id; }
            }
            string tags = string.Join("+", rule.RequiredTags);
            return cheapest == null
                ? (UnresolvedInputEffort, $"tags {tags} (no member derived)")
                : (cheapest.Value, $"cheapest {cheapestId} of {tags}");
        }
        return (NoInputEffort, "none");
    }

    /// <summary>1 for a machine unlocked by default or a skill level under 4, 2 for levels 4 to 7,
    /// 3 for level 8 and up or a friendship, quest or purchase unlock ("null" in the recipe data).</summary>
    public static int MachineUnlockEffort(string? unlockCondition)
    {
        string text = (unlockCondition ?? "").Trim();
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            || text.Equals("none", StringComparison.OrdinalIgnoreCase))
            return QuestUnlockEffort;
        if (text.Equals("default", StringComparison.OrdinalIgnoreCase))
            return DefaultUnlockEffort;

        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase))
            return QuestUnlockEffort;
        if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase))
            tokens = tokens.Skip(1).ToArray();
        if (tokens.Length >= 2 && int.TryParse(tokens[^1], out int level))
            return level < MidSkillLevel ? DefaultUnlockEffort : level < HighSkillLevel ? 2 : QuestUnlockEffort;
        return QuestUnlockEffort;
    }

    public static int TimeStep(int minutesUntilReady, int daysUntilReady)
    {
        int minutes = daysUntilReady >= 0 ? daysUntilReady * MinutesPerDay : minutesUntilReady;
        if (minutes < MinutesPerDay) return 0;
        return minutes < LongDays * MinutesPerDay ? 1 : 2;
    }
}
