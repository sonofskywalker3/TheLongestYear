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
    private const string MixedSeedsId = "(O)770";
    private const string AncientSeedsId = "(O)499";
    private const string MushroomLogTree = "trees:mature trees within 3 tiles";
    private const int FriendshipPerPetting = 15;   // FarmAnimal.cs 733
    private const string FishPondBuilding = "Fish Pond";
    private static readonly string[] MushroomLogItems = { "(O)404", "(O)420", "(O)422", "(O)257", "(O)281" };
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

    public static IEnumerable<(string ItemId, ObtainSource Source)> Machines(
        IEnumerable<MachineRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals, IEnumerable<CropRow> crops)
    {
        List<CropRow> cropList = crops.ToList();
        foreach (MachineRow rule in rows)
        {
            ConditionReading trigger = ConditionSeasons.Read(rule.TriggerCondition, festivals);
            bool noInput = rule.RequiredItemId == null && rule.RequiredTags.Count == 0;
            List<string> inputs = noInput ? new List<string>() : Inputs(rule, objects).ToList();
            if (!noInput && inputs.Count == 0) continue;
            int days = ProcessingDays(rule.MinutesUntilReady, rule.DaysUntilReady);
            bool several = rule.Outputs.Count > 1 && !rule.UseFirstValidOutput;
            string inputText = noInput ? "no input" : rule.RequiredItemId ?? string.Join(" ", rule.RequiredTags);

            // Lazy: a rule whose every output is a seed maker, a mushroom log, a DROP_IN or an unknown
            // output method never asks for this, and folding one table per input is not free.
            var inputCache = new Dictionary<string, Derived.Input>(StringComparer.Ordinal);
            Derived.Input InputOf(string input)
            {
                if (!inputCache.TryGetValue(input, out Derived.Input? read))
                    inputCache[input] = read = Derived.Of(snapshot, input);
                return read;
            }
            var feed = new Lazy<Derived.Input>(() => noInput
                ? Derived.Input.Free
                : Derived.Sooner(inputs.Select(InputOf)));

            // With UseFirstValidOutput the game takes the first output whose condition passes, so a
            // later output only happens in weeks no earlier, surely valid output already covers.
            WeekMask shadowed = WeekMask.None;
            foreach (MachineOutput output in rule.Outputs)
            {
                ConditionReading outCond = ConditionSeasons.Read(output.Condition, festivals);
                WeekMask alsoWithin = trigger.Weeks.Except(shadowed);
                DayTable gate = ConditionSeasons.Availability(outCond, alsoWithin);
                if (rule.UseFirstValidOutput && output.OutputMethod == null && output.Method == OutputMethodKind.None
                    && !outCond.Chance && !string.IsNullOrWhiteSpace(output.ItemId))
                    shadowed |= alsoWithin & outCond.Weeks;
                bool luck = several || output.IsRandom || trigger.Chance || outCond.Chance;
                ObtainConditions conditions = ConditionSeasons.Apply(ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "machine:" + rule.MachineId } }, trigger), outCond);
                string detail = $"{rule.MachineId} from {inputText}";

                if (output.Method == OutputMethodKind.Cask) continue;   // quality only, no item (Cask.cs OutputCask 78-140)

                if (output.Method == OutputMethodKind.SeedMaker)
                {
                    foreach (string input in inputs)
                    {
                        CropRow? crop = cropList.FirstOrDefault(c => c.HarvestId == input);
                        if (crop == null) continue;
                        Derived.Input harvest = InputOf(input);
                        DayTable inputDep = harvest.Dependable.Then(gate).Delay(days);
                        DayTable inputAny = harvest.Any.Then(gate).Delay(days);
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, luck ? DayTable.None : inputDep, inputAny,
                            harvest.Flag(conditions), $"{rule.MachineId} seed maker from {input}"))
                            yield return (crop.SeedId, s);
                    }
                    DayTable chanceBase = feed.Value.Any.Then(gate).Delay(days);
                    ObtainConditions seedMakerConditions = feed.Value.Flag(conditions);
                    foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, DayTable.None, chanceBase, seedMakerConditions, "seed maker 2% mixed seeds"))
                        yield return (MixedSeedsId, s);
                    foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, DayTable.None, chanceBase, seedMakerConditions, "seed maker 0.5% ancient seeds"))
                        yield return (AncientSeedsId, s);
                    continue;
                }

                if (output.Method == OutputMethodKind.MushroomLog)
                {
                    ObtainConditions logConditions = conditions with { Requires = conditions.Requires.Append(MushroomLogTree).ToList() };
                    DayTable table = DayTable.Always.Then(gate).Delay(days);
                    foreach (string mushroom in MushroomLogItems)
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, DayTable.None, table, logConditions, detail))
                            yield return (mushroom, s);
                    continue;
                }

                if (output.Method == OutputMethodKind.Unknown || output.OutputMethod != null)
                {
                    yield return (ItemQueries.UnresolvedPrefix + "machine " + rule.MachineId, new ObtainSource(
                        SourceKind.Other, DayTable.Always, Reliability.Chance, conditions with { Unresolved = true },
                        $"{detail}: output method {output.OutputMethod}"));
                    continue;
                }

                if (output.ItemId == DropIn)
                {
                    foreach (string input in inputs)
                    {
                        Derived.Input dropIn = InputOf(input);
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine,
                            luck ? DayTable.None : dropIn.Dependable.Then(gate).Delay(days),
                            dropIn.Any.Then(gate).Delay(days), dropIn.Flag(conditions), detail))
                            yield return (input, s);
                    }
                    continue;
                }
                if (string.IsNullOrWhiteSpace(output.ItemId)) continue;

                DayTable outDep = luck ? DayTable.None : feed.Value.Dependable.Then(gate).Delay(days);
                DayTable outAny = feed.Value.Any.Then(gate).Delay(days);
                foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, outDep, outAny, feed.Value.Flag(conditions), detail))
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
            Derived.Input needed = Derived.Input.Free;
            foreach (string ingredient in recipe.Ingredients)
                needed = needed.Both(IngredientWeeks(ingredient, objects, snapshot));
            DayTable dep = needed.Dependable, any = needed.Any;
            bool taughtByShop = recipeShopWeeks.TryGetValue(recipe.OutputId, out WeekMask taught);
            if (taughtByShop)
            {
                DayTable taughtTable = DayTable.InWeeks(taught);
                dep = dep.Latest(taughtTable);
                any = any.Latest(taughtTable);
            }
            var outputs = new List<string> { recipe.OutputId };
            if (recipe.AlternateOutputIds != null) outputs.AddRange(recipe.AlternateOutputIds);
            if (outputs.Count > 1) dep = DayTable.None;   // one output picked at random
            SourceKind kind = recipe.IsCooking ? SourceKind.Cooking : SourceKind.Crafting;
            ObtainConditions conditions = needed.Flag(UnlockConditions(recipe, taughtByShop));
            foreach (string output in outputs)
                foreach (ObtainSource s in SourcePair.Of(kind, dep, any, conditions, $"recipe {recipe.Name}"))
                    yield return (output, s);
        }
    }

    /// <summary>Setup steps for one animal produce: the building (its BuildDays, or 0 when unknown),
    /// the animal itself (bought today, produces from tomorrow), and, past 0 friendship, the days of
    /// petting it takes to reach it (FarmAnimal.cs 733: petting adds 15 a day).</summary>
    public static IReadOnlyList<SetupStep> AnimalSetup(AnimalRow animal, IReadOnlyDictionary<string, int> buildings, int friendship)
    {
        var steps = new List<SetupStep>
        {
            new("building:" + animal.House, buildings.TryGetValue(animal.House, out int buildDays) ? buildDays : 0),
            new("animal:" + animal.AnimalId, 1),
        };
        if (friendship > 0)
            steps.Add(new($"friendship:{animal.AnimalId} {friendship}", (int)Math.Ceiling(friendship / (double)FriendshipPerPetting)));
        return steps;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Animals(
        IEnumerable<AnimalRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals, IReadOnlyDictionary<string, int> buildings)
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
                if (deluxe) extra.Add("deluxe produce");
                // Several produce entries in one list: the animal produces one of them.
                bool picked = (deluxe ? animal.DeluxeProduce.Count : animal.Produce.Count) > 1;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with { Requires = extra }, reading);
                DayTable table = ConditionSeasons.Availability(reading, WeekMask.All).Delay(animal.DaysToProduce);
                yield return (produce.ItemId, new ObtainSource(SourceKind.Animal, table,
                    reading.Chance || picked ? Reliability.Chance : Reliability.Dependable, conditions, $"{animal.AnimalId} produce")
                { Setup = AnimalSetup(animal, buildings, friendship) });
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Ponds(
        IEnumerable<PondRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals, IReadOnlyDictionary<string, int> buildings)
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

        var setup = new[] { new SetupStep("building:" + FishPondBuilding, buildings.TryGetValue(FishPondBuilding, out int buildDays) ? buildDays : 0) };
        foreach ((PondRow pond, List<ObjInfo> fishes) in fishByPond)
        {
            Derived.Input stock = Derived.Sooner(fishes.Select(fish => Derived.Of(snapshot, fish.QualifiedId)));
            DayTable fishDep = stock.Dependable, fishAny = stock.Any;
            foreach (PondProduct product in pond.Products)
            {
                ConditionReading reading = ConditionSeasons.Read(product.Condition, festivals);
                bool luck = product.Chance < 1.0 || reading.Chance || product.IsRandom;
                ObtainConditions conditions = stock.Flag(ConditionSeasons.Apply(ObtainConditions.None with
                {
                    Requires = new[] { "building:" + FishPondBuilding, $"pond population {product.RequiredPopulation}" },
                }, reading));
                int growth = Math.Max(0, (product.RequiredPopulation - 1) * pond.SpawnTime);
                DayTable gate = ConditionSeasons.Availability(reading, WeekMask.All);
                DayTable dep = luck ? DayTable.None : fishDep.Delay(growth).Then(gate);
                DayTable any = fishAny.Delay(growth).Then(gate);
                foreach (ObtainSource s in SourcePair.Of(SourceKind.FishPond, dep, any, conditions, $"fish pond {pond.Id}", setup))
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
            WeekMask seasonMask = tap.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All;
            DayTable table = ConditionSeasons.Availability(reading, seasonMask).Delay(tap.DaysUntilReady);
            if (table.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Tapper, table,
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
            Derived.Input stone = Derived.Of(snapshot, geode);
            DayTable table = stone.Any.Then(ConditionSeasons.Availability(reading, WeekMask.All));
            if (table.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Geode, table, Reliability.Chance,
                stone.Flag(ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + geode, GeodeOpener } }, reading)),
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

    /// <summary>One ingredient: an item id, or a negative category any item of that category fills.</summary>
    private static Derived.Input IngredientWeeks(
        string ingredient, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot)
    {
        if (!int.TryParse(ingredient, out int category) || category >= 0)
            return Derived.Of(snapshot, ingredient);
        return Derived.Sooner(objects.Values.Where(o => o.Category == category).Select(o => Derived.Of(snapshot, o.QualifiedId)));
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
