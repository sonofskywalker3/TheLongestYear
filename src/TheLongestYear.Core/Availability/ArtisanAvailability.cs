using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for an artisan good (Data/Machines OutputRules + the machine's
/// own recipe in Data/CraftingRecipes): the cheapest qualifying input (recursively derived
/// through the composer's resolver), plus the machine's unlock effort, plus a step for how long
/// it runs. Minimum over every rule that outputs the item. The week is the later of the machine's
/// skill-level week (AvailabilityWeeks.MachineLevelWeek) and the input's own week, so Melon
/// Wine is not before Melon; an input nothing placed leaves the good unplaced.</summary>
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
    /// <summary>Skill level stood in for a quest, friendship or purchase unlock: the last thing a
    /// first-year player gets.</summary>
    public const int QuestUnlockLevel = 10;

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf,
        Func<string, int?>? weekOf = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (RawMachineRule rule in data.MachineRules)
        {
            if (!rule.OutputItemIds.Contains(qualifiedId)) continue;
            (int inputEffort, string inputNote, int? inputWeek, bool hasInput) = InputEffort(rule, data, effortOf, weekOf);
            string? unlock = data.MachineUnlocks.TryGetValue(rule.MachineItemId, out string? u) ? u : null;
            int machine = MachineUnlockEffort(unlock);
            int time = TimeStep(rule.MinutesUntilReady, rule.DaysUntilReady);
            int effort = inputEffort + machine + time;
            int machineWeek = AvailabilityWeeks.MachineLevelWeek(MachineUnlockLevel(unlock));
            int? week = hasInput && inputWeek == null ? null : Math.Max(machineWeek, inputWeek ?? 1);
            bool better = best == null
                || (week ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"artisan, {rule.MachineItemId}: input {inputNote} ({inputEffort}) + machine {machine} + time {time}, "
                    + $"week {(week?.ToString() ?? "unknown")}, effort {effort}",
                    week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value));
        }
        return best;
    }

    private static (int Effort, string Note, int? Week, bool HasInput) InputEffort(
        RawMachineRule rule, EffortData data, Func<string, int?> effortOf, Func<string, int?> weekOf)
    {
        if (!string.IsNullOrEmpty(rule.RequiredItemId))
        {
            string id = BundleParsing.NormalizeItemId(rule.RequiredItemId);
            return (effortOf(id) ?? UnresolvedInputEffort, id, weekOf(id), true);
        }
        if (rule.RequiredTags.Count > 0)
        {
            int? cheapest = null;
            int? earliest = null;
            string cheapestId = "";
            foreach (string id in ContextTagMatcher.IdsMatchingAll(data.Objects, rule.RequiredTags))
            {
                int? e = effortOf(id);
                if (e != null && (cheapest == null || e < cheapest)) { cheapest = e; cheapestId = id; }
                int? w = weekOf(id);
                if (w != null && (earliest == null || w < earliest)) earliest = w;
            }
            string tags = string.Join("+", rule.RequiredTags);
            return cheapest == null
                ? (UnresolvedInputEffort, $"tags {tags} (no member derived)", earliest, true)
                : (cheapest.Value, $"cheapest {cheapestId} of {tags}", earliest, true);
        }
        return (NoInputEffort, "none", null, false);
    }

    /// <summary>1 for a machine unlocked by default or a skill level under 4, 2 for levels 4 to 7,
    /// 3 for level 8 and up or a friendship, quest or purchase unlock ("null" in the recipe data).</summary>
    public static int MachineUnlockEffort(string? unlockCondition)
    {
        int level = MachineUnlockLevel(unlockCondition);
        if (level == QuestUnlockLevel) return QuestUnlockEffort;
        if (level == 0) return DefaultUnlockEffort;
        return level < MidSkillLevel ? DefaultUnlockEffort : level < HighSkillLevel ? 2 : QuestUnlockEffort;
    }

    /// <summary>Skill level a machine recipe needs; 0 for a default recipe, 10 for anything a
    /// quest, friendship or purchase gates.</summary>
    public static int MachineUnlockLevel(string? unlockCondition)
    {
        string text = (unlockCondition ?? "").Trim();
        if (text.Equals("default", StringComparison.OrdinalIgnoreCase)) return 0;
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            || text.Equals("none", StringComparison.OrdinalIgnoreCase))
            return QuestUnlockLevel;
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase)) return QuestUnlockLevel;
        if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase)) tokens = tokens.Skip(1).ToArray();
        return tokens.Length >= 2 && int.TryParse(tokens[^1], out int level) ? level : QuestUnlockLevel;
    }

    public static int TimeStep(int minutesUntilReady, int daysUntilReady)
    {
        int minutes = daysUntilReady >= 0 ? daysUntilReady * MinutesPerDay : minutesUntilReady;
        if (minutes < MinutesPerDay) return 0;
        return minutes < LongDays * MinutesPerDay ? 1 : 2;
    }
}
