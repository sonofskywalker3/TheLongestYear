using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One route's verdict for the debug readout.</summary>
public sealed record RouteVerdict(ObtainSource Source, bool Counts, int AddedDays, int? LandingDay, string Reason);

/// <summary>Whether an item counts as obtainable for a darkness hit, with every route's reason.</summary>
public sealed record FairnessVerdict(bool Counts, IReadOnlyList<RouteVerdict> Routes, string Summary);

/// <summary>"Start from nothing the day after the hit; can this player get the item by the deadline
/// through what this Darkness level allows?" (spec 2026-09-15 Part B, section 1). Reads the
/// obtainability model through its public API only and prices nothing the game does not price:
/// a missing building adds the game's build days, a missing skill or mine floor adds Jeff's gap
/// table, everything else is met or rules the route out.</summary>
public static class FairnessRule
{
    /// <summary>Tampering only happens in Winter and every unfinished bundle is judged on Winter 28.</summary>
    public const int TamperDeadline = DayTable.Days;

    private const string RecipePrefix = "recipe:";
    private const string CraftingPrefix = "crafting:";
    private const string UnlockShop = "unlock:shop";
    private const string UnlockTv = "unlock:Queen of Sauce";
    private const string MachinePrefix = "machine:";
    private const string BuildingPrefix = "building:";
    private const string AnimalPrefix = "animal:";
    private const string NotSoldSuffix = " (not sold)";
    private const string MailPrefix = "mail:";
    private const string MineFloorPrefix = "mines:floor ";
    private const string SkullCavern = "location:SkullCave";
    private const string Desert = "location:Desert";
    private const string BusMail = "ccVault";
    private const string FriendshipPrefix = "friendship:";
    private const string PondPopulationPrefix = "pond population";
    private const string MiningSkill = "Mining";
    private const int NoDays = 0;
    /// <summary>How many derived steps deep the rule follows a route's inputs (a cheese from a cow
    /// from a barn is two). A chain longer than this rules the route out rather than guessing.</summary>
    private const int MaxInputDepth = 6;

    /// <summary>What a level allows (the columns of the spec's table 1.2).</summary>
    private sealed record Policy(bool AllowChance, bool AllowUnresolved, bool YearTwoTv, bool IgnoreConditions, bool AddDays)
    {
        public static Policy For(DifficultyStep level) => level switch
        {
            DifficultyStep.Easy => new(false, false, false, false, false),
            DifficultyStep.Hard => new(false, false, true, false, true),
            DifficultyStep.Extreme => new(true, true, true, true, true),
            _ => new(false, false, false, false, true),   // Normal
        };
    }

    /// <summary>Reversion's deadline: the end of the current season on Easy, Winter 28 otherwise.</summary>
    public static int ReversionDeadline(int hitDay, DifficultyStep level)
        => level == DifficultyStep.Easy ? Calendar.LastDayOfSeason(hitDay) : DayTable.Days;

    public static bool Counts(string itemId, int hitDay, int deadlineDay, DifficultyStep level, SaveSnapshot save, ObtainabilityModel? model)
        => Judge(itemId, hitDay, deadlineDay, level, save, model).Counts;

    public static FairnessVerdict Judge(string itemId, int hitDay, int deadlineDay, DifficultyStep level, SaveSnapshot save, ObtainabilityModel? model)
    {
        if (save is null) throw new ArgumentNullException(nameof(save));
        if (model is null)
            return new FairnessVerdict(true, Array.Empty<RouteVerdict>(), "no obtainability model: no fairness filter, everything counts");
        var stack = new HashSet<string>(StringComparer.Ordinal) { itemId };
        return Judge(itemId, hitDay, deadlineDay, Policy.For(level), save, model, stack, 0);
    }

    private static FairnessVerdict Judge(
        string itemId, int hitDay, int deadlineDay, Policy policy, SaveSnapshot save, ObtainabilityModel model,
        HashSet<string> stack, int depth)
    {
        int startDay = hitDay + 1;
        var routes = new List<RouteVerdict>();
        foreach (ObtainSource source in model.Sources(itemId))
            routes.Add(JudgeRoute(source, hitDay, startDay, deadlineDay, policy, save, model, stack, depth));
        bool counts = routes.Any(r => r.Counts);
        string summary = routes.Count == 0
            ? "no source in the obtainability model"
            : counts ? $"counts ({routes.Count(r => r.Counts)} of {routes.Count} route(s))" : $"does not count (0 of {routes.Count} route(s))";
        return new FairnessVerdict(counts, routes, summary);
    }

    private static RouteVerdict JudgeRoute(
        ObtainSource source, int hitDay, int startDay, int deadlineDay, Policy policy, SaveSnapshot save,
        ObtainabilityModel model, HashSet<string> stack, int depth)
    {
        ObtainConditions c = source.Conditions;
        if (c.GingerIsland) return Out(source, "Ginger Island route");
        if (c.YearTwo)
        {
            bool tv = source.Kind == SourceKind.Cooking && c.Requires.Any(r => r.StartsWith(UnlockTv, StringComparison.Ordinal));
            if (!tv) return Out(source, "year 2 route");
            if (!policy.YearTwoTv) return Out(source, "year 2 Queen of Sauce episode (Hard and Extreme only)");
        }
        if (source.Reliability == Reliability.Chance && !policy.AllowChance) return Out(source, "chance route");
        if (c.Unresolved && !policy.AllowUnresolved) return Out(source, "unresolved route (a guess)");
        int? landing = source.Lands.Lands(startDay);
        if (landing is null) return Out(source, $"never lands from day {startDay}");

        int added = NoDays;
        if (!policy.IgnoreConditions)
        {
            string? blocked = Conditions(source, policy, save, ref added);
            if (blocked != null) return Out(source, blocked);
        }
        // A derived route carries the items it is made from, and the table says only WHEN they land,
        // not whether this player can get them: a Cheese Press with no cow is not a cheese route.
        // Extreme ignores conditions but not inputs, because an item made from nothing obtainable is
        // not obtainable at any level.
        string? missing = MissingInput(source, hitDay, deadlineDay, policy, save, model, stack, depth);
        if (missing != null) return Out(source, missing);
        // Setup days are added after the landing rather than shifting the start; an accepted approximation.
        int lands = landing.Value + added;
        bool counts = lands <= deadlineDay;
        string reason = counts
            ? (added > NoDays ? $"counts, lands day {lands} (+{added} day(s) of setup)" : $"counts, lands day {lands}")
            : $"lands day {lands}, after the deadline (day {deadlineDay})" + (added > NoDays ? $" with +{added} day(s) of setup" : "");
        return new RouteVerdict(source, counts, added, lands, reason);
    }

    /// <summary>Judges the route's input groups by the same rule: a group counts when ANY of its
    /// members counts, and every group must count. Returns the reason the route is out, or null when
    /// the route needs no item or every group is served. An id already being judged further up the
    /// chain does not count, so a cycle (X made from Y, Y made from X) terminates.</summary>
    private static string? MissingInput(
        ObtainSource source, int hitDay, int deadlineDay, Policy policy, SaveSnapshot save,
        ObtainabilityModel model, HashSet<string> stack, int depth)
    {
        if (source.Inputs.Count == 0) return null;
        if (depth >= MaxInputDepth) return $"input chain deeper than {MaxInputDepth} steps, not judged";
        foreach (IReadOnlyList<string> group in source.Inputs)
        {
            if (group.Count == 0) continue;
            bool served = false;
            foreach (string id in group)
            {
                if (!stack.Add(id)) continue;   // already on the stack: not a way in
                try
                {
                    if (Judge(id, hitDay, deadlineDay, policy, save, model, stack, depth + 1).Counts)
                    {
                        served = true;
                        break;
                    }
                }
                finally { stack.Remove(id); }
            }
            if (!served) return $"needs {string.Join(" or ", group)}, none obtainable";
        }
        return null;
    }

    /// <summary>Checks every condition against the save. Returns the reason the route is out, or null
    /// with <paramref name="added"/> holding the days the lacking setup costs.</summary>
    private static string? Conditions(ObtainSource source, Policy policy, SaveSnapshot save, ref int added)
    {
        ObtainConditions c = source.Conditions;
        IReadOnlyList<string> requires = c.Requires;
        var handledAnimals = new HashSet<string>(StringComparer.Ordinal);

        if (c.Skill != null && save.SkillLevel(c.Skill) < c.SkillLevel)
        {
            if (!policy.AddDays) return $"needs {c.Skill} {c.SkillLevel}, has {save.SkillLevel(c.Skill)}";
            added += SkillGapDays(save.SkillLevel(c.Skill), c.SkillLevel);
        }

        foreach (string r in requires)
        {
            if (r.StartsWith(RecipePrefix, StringComparison.Ordinal))
            {
                string name = r.Substring(RecipePrefix.Length);
                if (save.RecipesKnown.Contains(name)) continue;
                // A shop sale or a TV episode is a wait the game itself prices (the table already
                // holds the Sunday); any other unlock is a judgement, so a missing recipe rules out.
                bool priced = requires.Any(u => u == UnlockShop || u.StartsWith(UnlockTv, StringComparison.Ordinal));
                bool skillTaught = c.Skill != null;
                if (priced || skillTaught) continue;
                return $"recipe {name} not known";
            }
            if (r.StartsWith(CraftingPrefix, StringComparison.Ordinal))
            {
                // A crab pot or a tapper the player cannot craft is a missing crafting recipe, which
                // rules the route out on Easy, Normal and Hard alike (spec 1.2's recipe row). The
                // snapshot's RecipesKnown holds crafting recipe names as well as cooking ones.
                string name = r.Substring(CraftingPrefix.Length);
                if (save.RecipesKnown.Contains(name)) continue;
                return $"crafting recipe {name} not known";
            }
            if (r.StartsWith(MachinePrefix, StringComparison.Ordinal))
            {
                string id = r.Substring(MachinePrefix.Length);
                if (save.MachinesOwned.Contains(id)) continue;
                if (policy.AddDays && save.CraftableMachines.Contains(id)) { added += SabotageTuning.MachineCraftDays; continue; }
                return $"machine {id} not owned" + (policy.AddDays ? " and not craftable" : "");
            }
            if (r.StartsWith(BuildingPrefix, StringComparison.Ordinal))
            {
                string name = r.Substring(BuildingPrefix.Length);
                if (save.Buildings.Contains(name)) continue;
                if (!policy.AddDays) return $"building {name} not on the farm";
                SetupStep? step = source.Setup.FirstOrDefault(s => s.Name == r);
                if (step is null) return $"building {name} not on the farm and no build time recorded";
                added += step.Days;
                continue;
            }
            if (r.StartsWith(AnimalPrefix, StringComparison.Ordinal) && r.EndsWith(NotSoldSuffix, StringComparison.Ordinal))
            {
                // A not-sold animal is a game rule, not a wait: the model only writes this when the
                // animal cannot be bought, so it rules the route out regardless of level.
                string name = r.Substring(AnimalPrefix.Length, r.Length - AnimalPrefix.Length - NotSoldSuffix.Length);
                handledAnimals.Add(name);
                if (save.AnimalsOwned.Contains(name)) continue;
                return $"animal {name} not owned and not sold";
            }
            if (r.StartsWith(MailPrefix, StringComparison.Ordinal))
            {
                string flag = r.Substring(MailPrefix.Length);
                if (save.MailFlags.Contains(flag)) continue;
                return $"needs {flag}";
            }
            if (r.StartsWith(MineFloorPrefix, StringComparison.Ordinal))
            {
                if (!int.TryParse(r.Substring(MineFloorPrefix.Length), out int floor)) continue;
                if (save.DeepestMineFloor >= floor) continue;
                if (!policy.AddDays) return $"mine floor {floor} not reached (deepest {save.DeepestMineFloor})";
                added += MineGapDays(save.DeepestMineFloor, floor);
                continue;
            }
            if (r == SkullCavern)
            {
                if (!save.MailFlags.Contains(BusMail)) return "Skull Cavern: the desert is not open";
                int mining = save.SkillLevel(MiningSkill);
                if (mining >= SabotageTuning.StaircaseMiningLevel) continue;
                if (!policy.AddDays) return $"Skull Cavern: Staircases need Mining {SabotageTuning.StaircaseMiningLevel}, has {mining}";
                added += SkillGapDays(mining, SabotageTuning.StaircaseMiningLevel);
                continue;
            }
            if (r == Desert)
            {
                if (!save.MailFlags.Contains(BusMail)) return "the desert is not open";
                continue;
            }
            if (r.StartsWith(PondPopulationPrefix, StringComparison.Ordinal))
            {
                // The model records the pond building and the population it needs, but never the fish
                // that has to be in it, and the save snapshot cannot see a pond's contents. So a pond
                // route is unprovable below Extreme and rules out (spec 1.2's "pond" row).
                return "fish pond contents are not modelled";
            }
            // item:, guild:, tapper on tree, other location: and unlock: notes count as met.
            // "skill:<Name> N" strings from MineSources are not parsed: every such source is Chance today.
        }

        // Animal and friendship setup: the game prices these off Setup, not Requires, because a
        // purchasable animal is never named in Requires at all (spec 2026-09-15 fix round 1).
        // building:, sapling and tea bush steps are skipped here: buildings are already priced off
        // Requires above, and sapling/tea bush days are already inside the landing table.
        foreach (SetupStep step in source.Setup)
        {
            if (step.Name.StartsWith(AnimalPrefix, StringComparison.Ordinal))
            {
                string name = step.Name.Substring(AnimalPrefix.Length);
                if (handledAnimals.Contains(name)) continue;
                if (save.AnimalsOwned.Contains(name)) continue;
                if (!policy.AddDays) return $"animal {name} not owned";
                added += step.Days;
                continue;
            }
            if (!step.Name.StartsWith(FriendshipPrefix, StringComparison.Ordinal)) continue;
            // The animal id can contain spaces ("White Chicken"), so the count is the LAST token.
            int lastSpace = step.Name.LastIndexOf(' ');
            if (lastSpace < FriendshipPrefix.Length || !int.TryParse(step.Name.Substring(lastSpace + 1), out int needed)) continue;
            string animal = step.Name.Substring(FriendshipPrefix.Length, lastSpace - FriendshipPrefix.Length);
            int have = save.AnimalFriendship.TryGetValue(animal, out int f) ? f : 0;
            if (have >= needed) continue;
            if (!policy.AddDays) return $"{animal} friendship {needed} needed, has {have}";
            added += step.Days;
        }
        return null;
    }

    /// <summary>Jeff's skill table: the target level's days minus the current level's days.</summary>
    public static int SkillGapDays(int currentLevel, int targetLevel)
    {
        int[] table = SabotageTuning.SkillDaysToLevel;
        int Days(int level) => table[Math.Clamp(level, 0, table.Length - 1)];
        return Math.Max(0, Days(targetLevel) - Days(currentLevel));
    }

    /// <summary>One day per ten floors below the deepest reached, rounded up.</summary>
    public static int MineGapDays(int deepest, int target)
        => target <= deepest ? 0 : (target - deepest + SabotageTuning.MineFloorsPerDay - 1) / SabotageTuning.MineFloorsPerDay;

    private static RouteVerdict Out(ObtainSource source, string reason) => new(source, false, NoDays, null, reason);

    /// <summary>The readout for tly_sabotage fair: the verdict, then one line per route.</summary>
    public static string Explain(FairnessVerdict verdict)
    {
        if (verdict is null) throw new ArgumentNullException(nameof(verdict));
        var sb = new StringBuilder(verdict.Summary);
        foreach (RouteVerdict r in verdict.Routes)
        {
            sb.AppendLine().Append("  - ").Append(r.Counts ? "counts: " : "out: ").Append(r.Reason)
              .Append(" | ").Append(ObtainabilityText.SourceLine(r.Source));
            if (r.Source.Inputs.Count > 0)
                sb.Append(" | made from ")
                  .Append(string.Join(" and ", r.Source.Inputs.Select(g => string.Join(" or ", g))));
        }
        return sb.ToString();
    }
}
