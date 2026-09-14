using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Machines, cooking and crafting, animals, fish ponds, tappers and geode contents. See the
/// week-in-isolation rule in the plan's Task 7.</summary>
public static class MadeSources
{
    private const int MinutesPerDay = 1440;
    private const string NotTag = "!";
    private const string DropIn = "DROP_IN";
    private const string SkillUnlockPrefix = "s";
    private const string GeodeOpener = "shop:Blacksmith";
    private static readonly string[] KnownFromStartWords = { "default", "" };
    private static readonly string[] TaughtElsewhereWords = { "none", "null" };
    private static readonly string[] Skills = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };

    /// <summary>Code-only default geode contents (Utility.cs getTreasureFromGeode 6397-6647).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> DefaultGeodeTable = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["(O)535"] = new[] { "(O)390", "(O)330", "(O)86", "(O)378", "(O)380", "(O)382" },
        ["(O)536"] = new[] { "(O)390", "(O)330", "(O)84", "(O)378", "(O)380", "(O)382", "(O)384" },
        ["(O)749"] = new[] { "(O)390", "(O)330", "(O)82", "(O)84", "(O)86", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" },
    };
    private static readonly string[] OtherGeodeDefault = { "(O)390", "(O)330", "(O)82", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" };

    public static int ProcessingDays(int minutes, int days)
        => days >= 0 ? days : (int)Math.Ceiling(Math.Max(0, minutes) / (double)MinutesPerDay);

    /// <summary>Weeks an output can come out, when the input can be obtained on any day of
    /// <paramref name="inputWeeks"/> and takes <paramref name="days"/> to process; past Winter 28 is gone.</summary>
    public static WeekMask ShiftByDays(WeekMask inputWeeks, int days)
    {
        if (days <= 0) return inputWeeks;
        WeekMask result = WeekMask.None;
        for (int day = 1; day + days <= Calendar.DaysPerYear; day++)
            if (inputWeeks.Contains(WeekMask.WeekOfDay(day)))
                result |= WeekMask.Of(WeekMask.WeekOfDay(day + days));
        return result;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Machines(
        IEnumerable<MachineRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (MachineRow rule in rows)
        {
            ConditionReading trigger = ConditionSeasons.Read(rule.TriggerCondition, festivals);
            bool noInput = rule.RequiredItemId == null && rule.RequiredTags.Count == 0;
            List<string> inputs = noInput ? new List<string>() : Inputs(rule, objects).ToList();
            if (!noInput && inputs.Count == 0) continue;
            int days = ProcessingDays(rule.MinutesUntilReady, rule.DaysUntilReady);
            bool several = rule.Outputs.Count > 1 && !rule.UseFirstValidOutput;
            string inputText = noInput ? "no input" : rule.RequiredItemId ?? string.Join(" ", rule.RequiredTags);

            // With UseFirstValidOutput the game takes the first output whose condition passes, so a
            // later output only happens in weeks no earlier, surely valid output already covers.
            WeekMask shadowed = WeekMask.None;
            foreach (MachineOutput output in rule.Outputs)
            {
                ConditionReading outCond = ConditionSeasons.Read(output.Condition, festivals);
                WeekMask gate = (trigger.Weeks & outCond.Weeks).Except(shadowed);
                if (rule.UseFirstValidOutput && output.OutputMethod == null && !outCond.Chance && !string.IsNullOrWhiteSpace(output.ItemId))
                    shadowed |= gate;
                bool luck = several || output.IsRandom || trigger.Chance || outCond.Chance;
                ObtainConditions conditions = ConditionSeasons.Apply(ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "machine:" + rule.MachineId } }, trigger), outCond);
                string detail = $"{rule.MachineId} from {inputText}";

                if (output.OutputMethod != null)
                {
                    yield return (ItemQueries.UnresolvedPrefix + "machine " + rule.MachineId, new ObtainSource(
                        SourceKind.Other, WeekMask.All, Reliability.Chance, conditions with { Unresolved = true },
                        $"{detail}: output method {output.OutputMethod}"));
                    continue;
                }

                if (output.ItemId == DropIn)
                {
                    foreach (string input in inputs)
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine,
                            luck ? WeekMask.None : ShiftByDays(snapshot.Weeks(input, ObtainFilter.DependableOnly), days) & gate,
                            ShiftByDays(snapshot.Weeks(input, ObtainFilter.Any), days) & gate, conditions, detail))
                            yield return (input, s);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(output.ItemId)) continue;

                WeekMask dep = noInput ? WeekMask.All : WeekMask.None, any = dep;
                foreach (string input in inputs)
                {
                    dep |= snapshot.Weeks(input, ObtainFilter.DependableOnly);
                    any |= snapshot.Weeks(input, ObtainFilter.Any);
                }
                dep = luck ? WeekMask.None : ShiftByDays(dep, days) & gate;
                any = ShiftByDays(any, days) & gate;
                foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, dep, any, conditions, detail))
                    foreach (var emitted in ItemQueries.Emit(output.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Recipes(
        IEnumerable<RecipeRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, WeekMask> recipeShopWeeks, ObtainabilityModel snapshot)
    {
        foreach (RecipeRow recipe in rows)
        {
            WeekMask dep = WeekMask.All, any = WeekMask.All;
            foreach (string ingredient in recipe.Ingredients)
            {
                (WeekMask d, WeekMask a) = IngredientWeeks(ingredient, objects, snapshot);
                dep &= d;
                any &= a;
            }
            bool taughtByShop = recipeShopWeeks.TryGetValue(recipe.OutputId, out WeekMask taught);
            if (taughtByShop)
            {
                dep &= taught.FromWeekOnward();
                any &= taught.FromWeekOnward();
            }
            var outputs = new List<string> { recipe.OutputId };
            if (recipe.AlternateOutputIds != null) outputs.AddRange(recipe.AlternateOutputIds);
            if (outputs.Count > 1) dep = WeekMask.None;   // one output picked at random
            SourceKind kind = recipe.IsCooking ? SourceKind.Cooking : SourceKind.Crafting;
            ObtainConditions conditions = UnlockConditions(recipe, taughtByShop);
            foreach (string output in outputs)
                foreach (ObtainSource s in SourcePair.Of(kind, dep, any, conditions, $"recipe {recipe.Name}"))
                    yield return (output, s);
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Animals(
        IEnumerable<AnimalRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (AnimalRow animal in rows)
        {
            var requires = new List<string> { "building:" + animal.House };
            if (animal.PurchasePrice <= 0) requires.Add("animal:" + animal.AnimalId + " (not sold)");
            foreach ((AnimalProduce produce, bool deluxe) in animal.Produce.Select(p => (p, false)).Concat(animal.DeluxeProduce.Select(p => (p, true))))
            {
                ConditionReading reading = ConditionSeasons.Read(produce.Condition, festivals);
                if (reading.Weeks.IsEmpty) continue;
                var extra = new List<string>(requires);
                int friendship = Math.Max(produce.MinimumFriendship, deluxe ? animal.DeluxeMinimumFriendship : 0);
                if (friendship > 0) extra.Add($"friendship:{animal.AnimalId} {friendship}");
                if (deluxe) extra.Add("deluxe produce");
                // Several produce entries in one list: the animal produces one of them.
                bool picked = (deluxe ? animal.DeluxeProduce.Count : animal.Produce.Count) > 1;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with { Requires = extra }, reading);
                yield return (produce.ItemId, new ObtainSource(SourceKind.Animal, reading.Weeks,
                    reading.Chance || picked ? Reliability.Chance : Reliability.Dependable, conditions, $"{animal.AnimalId} produce"));
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Ponds(
        IEnumerable<PondRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        List<PondRow> ordered = rows.ToList();
        var fishByPond = new Dictionary<PondRow, List<ObjInfo>>();
        foreach (ObjInfo fish in objects.Values)
        {
            PondRow? winner = null;
            foreach (PondRow pond in ordered)
                if ((winner == null || pond.Precedence < winner.Precedence) && MatchesTags(fish, pond.RequiredTags))
                    winner = pond;
            if (winner == null) continue;
            if (!fishByPond.TryGetValue(winner, out List<ObjInfo>? list)) fishByPond[winner] = list = new List<ObjInfo>();
            list.Add(fish);
        }

        foreach ((PondRow pond, List<ObjInfo> fishes) in fishByPond)
        {
            WeekMask dep = WeekMask.None, any = WeekMask.None;
            foreach (ObjInfo fish in fishes)
            {
                dep |= snapshot.Weeks(fish.QualifiedId, ObtainFilter.DependableOnly);
                any |= snapshot.Weeks(fish.QualifiedId, ObtainFilter.Any);
            }
            dep = dep.FromWeekOnward();
            any = any.FromWeekOnward();
            foreach (PondProduct product in pond.Products)
            {
                ConditionReading reading = ConditionSeasons.Read(product.Condition, festivals);
                bool luck = product.Chance < 1.0 || reading.Chance || product.IsRandom;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with
                {
                    Requires = new[] { "building:Fish Pond", $"pond population {product.RequiredPopulation}" },
                }, reading);
                foreach (ObtainSource s in SourcePair.Of(SourceKind.FishPond,
                    luck ? WeekMask.None : dep & reading.Weeks, any & reading.Weeks, conditions, $"fish pond {pond.Id}"))
                    foreach (var emitted in ItemQueries.Emit(product.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Tappers(
        IEnumerable<TapRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (TapRow tap in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(tap.Condition, festivals);
            WeekMask weeks = reading.Weeks & (tap.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All);
            if (weeks.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Tapper, weeks,
                tap.Chance < 1.0 || reading.Chance || tap.IsRandom ? Reliability.Chance : Reliability.Dependable,
                ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "crafting:Tapper", "tree:" + tap.TreeId } }, reading),
                $"tapper on tree {tap.TreeId}, {tap.DaysUntilReady} days");
            foreach (var emitted in ItemQueries.Emit(tap.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Geodes(
        IEnumerable<GeodeDropRow> rows, IReadOnlyCollection<string> geodesUsingDefaultTable,
        IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var drops = rows.Select(r => (r.GeodeId, r.ItemId, r.Condition)).ToList();
        foreach (string geode in geodesUsingDefaultTable)
            foreach (string id in DefaultGeodeTable.TryGetValue(geode, out string[]? table) ? table : OtherGeodeDefault)
                drops.Add((geode, id, null));
        foreach ((string geode, string item, string? condition) in drops.Distinct())
        {
            ConditionReading reading = ConditionSeasons.Read(condition, festivals);
            WeekMask weeks = snapshot.Weeks(geode, ObtainFilter.Any) & reading.Weeks;
            if (weeks.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Geode, weeks, Reliability.Chance,
                ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + geode, GeodeOpener } }, reading),
                $"opened from {geode}");
            foreach (var emitted in ItemQueries.Emit(item, objects, template))
                yield return emitted;
        }
    }

    private static IEnumerable<string> Inputs(MachineRow rule, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        if (rule.RequiredItemId != null && rule.RequiredTags.Count == 0)
            return new[] { BundleParsing.NormalizeItemId(rule.RequiredItemId) };
        string? id = rule.RequiredItemId == null ? null : BundleParsing.NormalizeItemId(rule.RequiredItemId);
        return objects.Values
            .Where(o => (id == null || o.QualifiedId == id) && MatchesTags(o, rule.RequiredTags))
            .Select(o => o.QualifiedId);
    }

    private static (WeekMask Dependable, WeekMask Any) IngredientWeeks(
        string ingredient, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot)
    {
        if (!int.TryParse(ingredient, out int category) || category >= 0)
            return (snapshot.Weeks(ingredient, ObtainFilter.DependableOnly), snapshot.Weeks(ingredient, ObtainFilter.Any));
        WeekMask dep = WeekMask.None, any = WeekMask.None;
        foreach (ObjInfo o in objects.Values.Where(o => o.Category == category))
        {
            dep |= snapshot.Weeks(o.QualifiedId, ObtainFilter.DependableOnly);
            any |= snapshot.Weeks(o.QualifiedId, ObtainFilter.Any);
        }
        return (dep, any);
    }

    private static bool MatchesTags(ObjInfo item, IReadOnlyList<string> tags)
        => tags.Count > 0 && tags.All(tag => tag.StartsWith(NotTag, StringComparison.Ordinal)
            ? !item.ContextTags.Contains(tag.Substring(1))
            : item.ContextTags.Contains(tag));

    private static ObtainConditions UnlockConditions(RecipeRow recipe, bool taughtByShop)
    {
        string[] tokens = recipe.Unlock.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requires = new List<string> { "recipe:" + recipe.Name };
        string first = tokens.Length == 0 ? "" : tokens[0].ToLowerInvariant();
        if (KnownFromStartWords.Contains(first))
            return ObtainConditions.None with { Requires = requires };
        if (TaughtElsewhereWords.Contains(first))
        {
            // No automatic unlock: a shop, a letter, a friend or an event teaches it. A shop the model
            // read is enough; otherwise the week the player learns it is unknown.
            requires.Add(taughtByShop ? "unlock:shop" : "unlock:none (taught some other way)");
            return ObtainConditions.None with { Requires = requires, Unresolved = !taughtByShop };
        }
        int start = tokens[0] == SkillUnlockPrefix ? 1 : 0;
        if (tokens.Length > start + 1 && Skills.Contains(tokens[start]) && int.TryParse(tokens[start + 1], out int level))
            return ObtainConditions.None with { Skill = tokens[start], SkillLevel = level, Requires = requires };
        // "f Robin 7" (friendship), "l 4" (farmhouse level) and anything else stay as a note.
        requires.Add("unlock:" + recipe.Unlock);
        return ObtainConditions.None with { Requires = requires };
    }
}
