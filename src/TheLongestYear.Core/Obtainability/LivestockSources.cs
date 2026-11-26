using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Animal produce, fish pond products and tapper output: sources that come from something
/// living on the farm (spec 2026-09-14-obtainability-phase2, section 1). Split out of
/// <see cref="MadeSources"/>, which keeps machines, recipes and geodes.</summary>
public static class LivestockSources
{
    private const int FriendshipPerPetting = 15;   // FarmAnimal.cs 733
    private const string FishPondBuilding = "Fish Pond";
    private const string ForagingSkill = "Foraging";
    /// <summary>A tree drops its seed (or its SeedDropItems) only for Foraging 1 or more (Tree.cs 665).</summary>
    private const int SeedDropForagingLevel = 1;

    /// <summary>Setup steps for one animal produce: the building (its BuildDays, or 0 when unknown),
    /// the animal itself (bought or hatched today, growing up and, for a hatched one, incubating
    /// first, then producing from the day after), and, past 0 friendship, the days of petting it
    /// takes to reach it (FarmAnimal.cs 733: petting adds 15 a day).</summary>
    public static IReadOnlyList<SetupStep> AnimalSetup(AnimalRow animal, IReadOnlyDictionary<string, int> buildings, int friendship)
    {
        var steps = new List<SetupStep>
        {
            new("building:" + animal.House, buildings.TryGetValue(animal.House, out int buildDays) ? buildDays : 0),
            new("animal:" + animal.AnimalId, Math.Max(1, animal.IncubationDays + animal.DaysToMature)),
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
            bool sold = animal.PurchasePrice > 0 || animal.SoldAsAlternate;
            var requires = new List<string> { "building:" + animal.House };
            if (!sold) requires.Add("animal:" + animal.AnimalId + " (not sold)");
            foreach ((AnimalProduce produce, bool deluxe) in animal.Produce.Select(p => (p, false)).Concat(animal.DeluxeProduce.Select(p => (p, true))))
            {
                ConditionReading reading = ConditionSeasons.Read(produce.Condition, festivals);
                if (reading.Weeks.IsEmpty) continue;
                var extra = new List<string>(requires);
                int friendship = Math.Max(produce.MinimumFriendship, deluxe ? animal.DeluxeMinimumFriendship : 0);
                if (deluxe) extra.Add("deluxe produce");
                // Several produce entries in one list: the animal produces one of them.
                bool picked = (deluxe ? animal.DeluxeProduce.Count : animal.Produce.Count) > 1;
                ObtainConditions conditions = ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = extra, OwnedOnly = !sold }, reading);
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
                if ((winner == null || pond.Precedence < winner.Precedence) && MadeSources.MatchesTags(fish, pond.RequiredTags))
                    winner = pond;
            if (winner == null) continue;
            if (!fishByPond.TryGetValue(winner, out List<ObjInfo>? list)) fishByPond[winner] = list = new List<ObjInfo>();
            list.Add(fish);
        }

        var setup = new[] { new SetupStep("building:" + FishPondBuilding, buildings.TryGetValue(FishPondBuilding, out int buildDays) ? buildDays : 0) };
        foreach ((PondRow pond, List<ObjInfo> fishes) in fishByPond)
        {
            Derived.Input stock = Derived.Sooner(fishes.Select(fish => Derived.Of(snapshot, fish.QualifiedId)));
            foreach (PondProduct product in pond.Products)
            {
                ConditionReading reading = ConditionSeasons.Read(product.Condition, festivals);
                bool luck = product.Chance < 1.0 || reading.Chance || product.IsRandom;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with
                {
                    Requires = new[] { "building:" + FishPondBuilding, $"pond population {product.RequiredPopulation}" },
                }, reading);
                int growth = Math.Max(0, (product.RequiredPopulation - 1) * pond.SpawnTime);
                DayTable gate = ConditionSeasons.Availability(reading, WeekMask.All);
                foreach (ObtainSource s in stock.Emit(SourceKind.FishPond, t => t.Delay(growth).Then(gate),
                    conditions, $"fish pond {pond.Id}", setup, luck))
                    foreach (var emitted in ItemQueries.Emit(product.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Tappers(
        IEnumerable<TapRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (TapRow tap in rows)
            if (TapTemplate(tap, festivals) is ObtainSource template)
                foreach (var emitted in ItemQueries.Emit(tap.ItemId, objects, template))
                    yield return emitted;
    }

    /// <summary>A tap row read as a route from day 1 on a standing tree, or null when it never yields.</summary>
    private static ObtainSource? TapTemplate(TapRow tap, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        ConditionReading reading = ConditionSeasons.Read(tap.Condition, festivals);
        WeekMask seasonMask = tap.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All;
        DayTable table = ConditionSeasons.Availability(reading, seasonMask).Delay(tap.DaysUntilReady);
        if (table.IsEmpty) return null;
        return new ObtainSource(SourceKind.Tapper, table,
            tap.Chance < 1.0 || reading.Chance || tap.IsRandom ? Reliability.Chance : Reliability.Dependable,
            ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "crafting:Tapper", "tree:" + tap.TreeId } }, reading),
            $"tapper on tree {tap.TreeId}, {tap.DaysUntilReady} days");
    }

    /// <summary>Wild tree drops (Data/WildTrees ShakeItems, SeedDropItems, ChopItems; Tree.cs shake and
    /// chop), read like the tappers: the tree stands where the game puts it, its own season and
    /// condition decide the days. A seed drop comes only on days the tree rolls a seed and only from
    /// Foraging 1 (Tree.cs 665), so it is luck.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> TreeDrops(
        IEnumerable<TreeDropRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (TreeDropRow drop in rows)
            if (DropTemplate(drop, festivals) is ObtainSource template)
                foreach (var emitted in ItemQueries.Emit(drop.ItemId, objects, template))
                    yield return emitted;
    }

    /// <summary>A shake, seed or chop row read as a route from day 1 on a standing tree, or null when it
    /// never yields.</summary>
    private static ObtainSource? DropTemplate(TreeDropRow drop, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        ConditionReading reading = ConditionSeasons.Read(drop.Condition, festivals);
        WeekMask seasonMask = drop.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All;
        DayTable table = ConditionSeasons.Availability(reading, seasonMask);
        if (table.IsEmpty) return null;
        bool seed = drop.Kind == TreeDropKind.Seed;
        ObtainConditions conditions = ObtainConditions.None with { Requires = new[] { "tree:" + drop.TreeId } };
        if (seed) conditions = conditions with { Skill = ForagingSkill, SkillLevel = SeedDropForagingLevel };
        string how = drop.Kind switch
        {
            TreeDropKind.Shake => "shaken from",
            TreeDropKind.Seed => "seed drop from",
            _ => "chopped from",
        };
        return new ObtainSource(SourceKind.Forage, table,
            seed || drop.Chance < 1.0 || reading.Chance || drop.IsRandom ? Reliability.Chance : Reliability.Dependable,
            ConditionSeasons.Apply(conditions, reading), $"{how} tree {drop.TreeId}");
    }

    /// <summary>The ids of the trees a player has only by planting their seed (<see cref="PlantedTrees.IsPlantedOnly"/>):
    /// their drops and taps are left out of the direct routes and come from <see cref="PlantedTreeDrops"/>.</summary>
    public static IReadOnlySet<string> PlantedOnlyTreeIds(IEnumerable<WildTreeRow> trees)
        => new HashSet<string>((trees ?? Enumerable.Empty<WildTreeRow>()).Where(PlantedTrees.IsPlantedOnly).Select(t => t.TreeId), StringComparer.Ordinal);

    /// <summary>Drops and taps of a tree that stands nowhere until a player plants its seed (Cornucopia's
    /// herb trees and Bamboo, the Mystic Tree): the seed's own table, then the growth nights to a grown
    /// tree (<see cref="PlantedTrees.GrowTable"/>), then the row's own days, exactly as a standing tree's.
    /// A tree whose seed has no route gives nothing; a chance row or a chance seed stays luck.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> PlantedTreeDrops(
        IEnumerable<TreeDropRow> drops, IEnumerable<TapRow> taps, IEnumerable<WildTreeRow> trees, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var dropRows = drops.ToList();
        var tapRows = taps.ToList();
        foreach (WildTreeRow tree in trees.Where(PlantedTrees.IsPlantedOnly))
        {
            if (PlantedTrees.GrowthNights(tree.GrowthChance) is not int nights) continue;
            string seedId = BundleParsing.NormalizeItemId(tree.SeedItemId!);
            Derived.Input planted = Derived.Of(snapshot, seedId);
            if (planted.IsEmpty) continue;
            DayTable grown = PlantedTrees.GrowTable(nights, tree.GrowsInWinter);
            var setup = new[] { new SetupStep("tree", nights) };
            var templates = dropRows.Where(d => d.TreeId == tree.TreeId).Select(d => (d.ItemId, Template: DropTemplate(d, festivals)))
                .Concat(tapRows.Where(t => t.TreeId == tree.TreeId).Select(t => (t.ItemId, Template: TapTemplate(t, festivals))));
            foreach ((string itemId, ObtainSource? template) in templates)
            {
                if (template == null) continue;
                ObtainConditions conditions = template.Conditions with
                {
                    Requires = new[] { "item:" + seedId }.Concat(template.Conditions.Requires).ToArray(),
                };
                DayTable rowDays = template.Lands;
                foreach (ObtainSource source in planted.Emit(template.Kind, t => t.Then(grown).Then(rowDays), conditions,
                    $"{template.Detail}, grown from {seedId}", setup, template.Reliability == Reliability.Chance))
                    foreach (var emitted in ItemQueries.Emit(itemId, objects, source))
                        yield return emitted;
            }
        }
    }
}
