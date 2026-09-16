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
}
