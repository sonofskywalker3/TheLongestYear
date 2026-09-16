using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Machines, cooking and crafting, and geode contents (animals, fish ponds and tappers are in
/// <see cref="LivestockSources"/>). Each
/// landing day is the latest of the inputs' landing days from that start, plus the processing time
/// (spec 2026-09-14-obtainability-phase2, section 1, "Machines and recipes").</summary>
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
    private static readonly string[] MushroomLogItems = { "(O)404", "(O)420", "(O)422", "(O)257", "(O)281" };
    /// <summary>Recipe ingredient code for "any wild seed packet" (CraftingRecipe.cs 16, matched at 209):
    /// Spring, Summer, Fall or Winter Seeds.</summary>
    private const string AnyWildSeed = "-777";
    private static readonly string[] WildSeedIds = { "(O)495", "(O)496", "(O)497", "(O)498" };
    private static readonly string[] KnownFromStartWords = { "default", "" };
    private static readonly string[] TaughtElsewhereWords = { "none", "null" };
    /// <summary>A cooking recipe unlocked by farmhouse level ("l 0", "l 100") has no automatic unlock
    /// either: the Queen of Sauce or an event teaches it.</summary>
    private const string FarmhouseUnlockPrefix = "l";
    private const int EpisodesInYearOne = Calendar.WeeksPerYear;   // episode k airs in week k
    private const int NoEpisode = 0;
    private static readonly string[] Skills = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };

    /// <summary>Code-only default geode contents (Utility.cs getTreasureFromGeode 6397-6647).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> DefaultGeodeTable = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["(O)535"] = new[] { "(O)390", "(O)330", "(O)86", "(O)378", "(O)380", "(O)382" },
        ["(O)536"] = new[] { "(O)390", "(O)330", "(O)84", "(O)378", "(O)380", "(O)382", "(O)384" },
        ["(O)749"] = new[] { "(O)390", "(O)330", "(O)82", "(O)84", "(O)86", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" },
    };
    private static readonly string[] OtherGeodeDefault = { "(O)390", "(O)330", "(O)82", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" };

    /// <summary>The three geode types the mines hand out by the handful (Geode, Frozen Geode, Magma
    /// Geode: about 2.2% of stones on their floors, MineShaft.cs 3642-3650). A named mineral from one
    /// of them is about a 3.1% to 3.8% roll per crack (Utility.cs getTreasureFromGeode 6586-6647: half
    /// the cracks take the common branch, the rest pick one of 13 to 16 minerals), and a mining day
    /// cracks 6 or 7 of them, so it is about 20% a day and crosses 90% after
    /// <see cref="CrackDays"/> days. Omni Geode (1 of 44, 1.1% a crack) and Artifact Trove (one trove
    /// per omni geode traded, not a repeatable try) are not in this set and stay Chance
    /// (ruling 2026-09-16).</summary>
    private static readonly HashSet<string> RepeatableGeodeIds = new(StringComparer.Ordinal)
        { "(O)535", "(O)536", "(O)537" };

    /// <summary>Days of cracking after which a named mineral from a repeatable geode has landed with
    /// about 90% probability (1 - 0.8^11); the dependable half lands this many days after the geode.</summary>
    public const int CrackDays = 11;

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
                        foreach (ObtainSource s in InputOf(input).Emit(SourceKind.Machine, t => t.Then(gate).Delay(days),
                            conditions, $"{rule.MachineId} seed maker from {input}", luck: luck))
                            yield return (crop.SeedId, s);
                    }
                    foreach (ObtainSource s in feed.Value.Emit(SourceKind.Machine, t => t.Then(gate).Delay(days),
                        conditions, "seed maker 2% mixed seeds", luck: true))
                        yield return (MixedSeedsId, s);
                    foreach (ObtainSource s in feed.Value.Emit(SourceKind.Machine, t => t.Then(gate).Delay(days),
                        conditions, "seed maker 0.5% ancient seeds", luck: true))
                        yield return (AncientSeedsId, s);
                    continue;
                }

                if (output.Method == OutputMethodKind.MushroomLog)
                {
                    ObtainConditions logConditions = conditions with { Requires = conditions.Requires.Append(MushroomLogTree).ToList() };
                    DayTable table = DayTable.Always.Then(gate).Delay(days);
                    foreach (string mushroom in MushroomLogItems)
                        // The log takes no input, so the shared "from <input>" detail would read "from
                        // no input"; name the mushroom this source is for instead.
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, DayTable.None, table, logConditions,
                            $"{rule.MachineId} gives {mushroom}"))
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
                        foreach (ObtainSource s in InputOf(input).Emit(SourceKind.Machine, t => t.Then(gate).Delay(days),
                            conditions, detail, luck: luck))
                            yield return (input, s);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(output.ItemId)) continue;

                foreach (ObtainSource s in feed.Value.Emit(SourceKind.Machine, t => t.Then(gate).Delay(days),
                    conditions, detail, luck: luck))
                    foreach (var emitted in ItemQueries.Emit(output.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Recipes(
        IEnumerable<RecipeRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, WeekMask> recipeShopWeeks, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, int>? cookingChannel = null)
    {
        foreach (RecipeRow recipe in rows)
        {
            Derived.Input needed = Derived.Input.Free;
            foreach (string ingredient in recipe.Ingredients)
                needed = needed.Both(IngredientWeeks(ingredient, objects, snapshot));
            bool taughtByShop = recipeShopWeeks.TryGetValue(recipe.OutputId, out WeekMask taught);
            DayTable? taughtTable = taughtByShop ? DayTable.InWeeks(taught) : null;
            var outputs = new List<string> { recipe.OutputId };
            if (recipe.AlternateOutputIds != null) outputs.AddRange(recipe.AlternateOutputIds);
            bool oneAtRandom = outputs.Count > 1;   // one output picked at random
            SourceKind kind = recipe.IsCooking ? SourceKind.Cooking : SourceKind.Crafting;
            int episode = TvEpisode(recipe, cookingChannel);
            // A recipe the TV teaches and nothing else does: the TV week is the real answer, so it
            // replaces the "taught some other way" GUESS rather than sitting beside it. Only that
            // guess is replaced: a shop that teaches the same recipe is already a resolved week, and
            // a skill or friendship unlock is a route of its own, so both keep their source and gain
            // the TV one beside it (either route works, and the model takes the earlier).
            bool replacesTheGuess = episode != NoEpisode && TaughtElsewhere(recipe) && !taughtByShop;
            if (!replacesTheGuess)
            {
                ObtainConditions conditions = UnlockConditions(recipe, taughtByShop);
                foreach (string output in outputs)
                    foreach (ObtainSource s in needed.Emit(kind, t => taughtTable == null ? t : t.Latest(taughtTable),
                        conditions, $"recipe {recipe.Name}", luck: oneAtRandom))
                        yield return (output, s);
            }
            if (episode == NoEpisode) continue;
            // Episode k airs on the Sunday that is day 7k of year 1 (TV.cs getWeeklyRecipe 518:
            // whichWeek = DaysPlayed % 224 / 7); episodes past 16 air in year 2, which the default
            // filters exclude. A Wednesday rerun only repeats an EARLIER episode, so it adds nothing.
            bool yearTwo = episode > EpisodesInYearOne;
            int airDay = Calendar.DaysPerWeek * episode;
            DayTable tvTable = yearTwo ? DayTable.Always : DayTable.Available(day => day >= airDay);
            ObtainConditions tvConditions = ObtainConditions.None with
            {
                Requires = new[] { "recipe:" + recipe.Name, $"unlock:Queen of Sauce episode {episode} (Sunday of week {episode})" },
                YearTwo = yearTwo,
            };
            foreach (string output in outputs)
                foreach (ObtainSource s in needed.Emit(kind, t => t.Latest(tvTable), tvConditions,
                    $"recipe {recipe.Name} taught by the Queen of Sauce", luck: oneAtRandom))
                    yield return (output, s);
        }
    }

    /// <summary>The Queen of Sauce episode that teaches this cooking recipe, or <see cref="NoEpisode"/>.</summary>
    private static int TvEpisode(RecipeRow recipe, IReadOnlyDictionary<string, int>? cookingChannel)
        => recipe.IsCooking && cookingChannel != null && cookingChannel.TryGetValue(recipe.Name, out int episode)
            ? episode
            : NoEpisode;

    /// <summary>Recipes whose Data/CraftingRecipes unlock is "null" but which an event or letter
    /// teaches on a friendship level (Jeff's ruling 2026-09-16). Tea Sapling: the Sunroom opens at
    /// Caroline 2 hearts (GameLocation.cs 8863-8871) and its event mails CarolineTea. Wild Bait: Linus's
    /// event 26 needs 4 hearts and adds the recipe (Event.cs 10463-10465).</summary>
    private static readonly IReadOnlyDictionary<string, string> EventTaughtUnlocks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Tea Sapling"] = "f Caroline 2",
        ["Wild Bait"] = "f Linus 4",
    };

    /// <summary>True when nothing in the recipe row itself unlocks it: a shop, a letter, a friend, an
    /// event or the TV teaches it.</summary>
    private static bool TaughtElsewhere(RecipeRow recipe)
    {
        if (EventTaughtUnlocks.ContainsKey(recipe.Name)) return false;
        string[] tokens = recipe.Unlock.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;
        string first = tokens[0].ToLowerInvariant();
        return TaughtElsewhereWords.Contains(first) || first == FarmhouseUnlockPrefix;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Geodes(
        IEnumerable<GeodeDropRow> rows, IReadOnlyCollection<string> geodesUsingDefaultTable,
        IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        // The geode's own mineral list (Data/Objects GeodeDrops) is the repeatable part; the shared
        // common branch (stone, clay, ore, coal, the floor's crystal) is added from the code table
        // below with Repeatable false, so it stays a plain roll.
        var drops = rows.Select(r => (r.GeodeId, r.ItemId, r.Condition, Repeatable: RepeatableGeodeIds.Contains(r.GeodeId))).ToList();
        foreach (string geode in geodesUsingDefaultTable)
            foreach (string id in DefaultGeodeTable.TryGetValue(geode, out string[]? table) ? table : OtherGeodeDefault)
                drops.Add((geode, id, null, false));
        foreach ((string geode, string item, string? condition, bool repeatable) in drops.Distinct())
        {
            ConditionReading reading = ConditionSeasons.Read(condition, festivals);
            Derived.Input stone = Derived.Of(snapshot, geode);
            DayTable open = ConditionSeasons.Availability(reading, WeekMask.All);
            ObtainConditions conditions = ConditionSeasons.Apply(
                ObtainConditions.None with { Requires = new[] { "item:" + geode, GeodeOpener } }, reading);
            // Every crack is a roll, so the lucky half lands the day the geode does.
            foreach (ObtainSource template in stone.Emit(SourceKind.Geode, t => t.Then(open), conditions,
                $"opened from {geode}", luck: true))
                foreach (var emitted in ItemQueries.Emit(item, objects, template))
                    yield return emitted;
            // A named mineral from a repeatable geode is also dependable, CrackDays after the geode's
            // own dependable landing (ruling 2026-09-16). A row with a condition of its own (the
            // Prismatic Shard after 16 cracks) is not a plain pick from the list and stays a roll.
            if (!repeatable || condition != null) continue;
            foreach (ObtainSource template in stone.Emit(SourceKind.Geode, t => t.Then(open).Delay(CrackDays), conditions,
                $"opened from {geode}, cracked over {CrackDays} days"))
                if (template.Reliability == Reliability.Dependable)
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
        if (ingredient == AnyWildSeed)
            return Derived.Sooner(WildSeedIds.Select(id => Derived.Of(snapshot, id)));
        if (!int.TryParse(ingredient, out int category) || category >= 0)
            return Derived.Of(snapshot, ingredient);
        return Derived.Sooner(objects.Values.Where(o => o.Category == category).Select(o => Derived.Of(snapshot, o.QualifiedId)));
    }

    internal static bool MatchesTags(ObjInfo item, IReadOnlyList<string> tags)
        => tags.Count > 0 && tags.All(tag => tag.StartsWith(NotTag, StringComparison.Ordinal)
            ? !item.ContextTags.Contains(tag.Substring(1))
            : item.ContextTags.Contains(tag));

    private static ObtainConditions UnlockConditions(RecipeRow recipe, bool taughtByShop)
    {
        if (EventTaughtUnlocks.TryGetValue(recipe.Name, out string? taughtBy))
            return ObtainConditions.None with { Requires = new[] { "recipe:" + recipe.Name, "unlock:" + taughtBy } };
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
