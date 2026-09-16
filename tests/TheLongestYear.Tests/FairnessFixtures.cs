using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Tests;

/// <summary>The hand-made model, routes and saves the fairness rule tests share.</summary>
internal static class FairnessFixtures
{
    internal const string Item = "(O)999";
    internal const string Ingredient = "(O)888";
    internal const string Other = "(O)777";
    internal const int Hit = 60;          // Fall 4
    internal const int Deadline = 112;    // Winter 28

    internal static ObtainabilityModel Model(params ObtainSource[] sources)
        => new(new Dictionary<string, IReadOnlyList<ObtainSource>> { [Item] = sources });

    /// <summary>A model of several items, for the routes that are made from another item.</summary>
    internal static ObtainabilityModel ModelOf(params (string Id, ObtainSource[] Sources)[] items)
        => new(items.ToDictionary(i => i.Id, i => (IReadOnlyList<ObtainSource>)i.Sources));

    internal static ObtainSource Route(
        SourceKind kind = SourceKind.Forage, Reliability reliability = Reliability.Dependable,
        Func<int, bool>? available = null, string[]? requires = null, string? skill = null, int skillLevel = 0,
        bool yearTwo = false, bool island = false, bool unresolved = false, SetupStep[]? setup = null,
        string[][]? inputs = null)
        => new(kind, DayTable.Available(available ?? (_ => true)), reliability,
            ObtainConditions.None with
            {
                Requires = requires ?? Array.Empty<string>(), Skill = skill, SkillLevel = skillLevel,
                YearTwo = yearTwo, GingerIsland = island, Unresolved = unresolved,
            }, "test")
        {
            Setup = setup ?? Array.Empty<SetupStep>(),
            Inputs = inputs ?? Array.Empty<IReadOnlyList<string>>(),
        };

    internal static SaveSnapshot Save(
        string[]? recipes = null, string[]? buildings = null, string[]? machines = null, string[]? craftable = null,
        string[]? animals = null, string[]? mail = null, int floor = 0, int mining = 0, int fishing = 0,
        Dictionary<string, int>? friendship = null)
        => new(
            new HashSet<string>(recipes ?? Array.Empty<string>()), new HashSet<string>(buildings ?? Array.Empty<string>()),
            new HashSet<string>(machines ?? Array.Empty<string>()), new HashSet<string>(craftable ?? Array.Empty<string>()),
            new HashSet<string>(animals ?? Array.Empty<string>()), friendship ?? new Dictionary<string, int>(),
            new HashSet<string>(mail ?? Array.Empty<string>()), floor,
            new Dictionary<string, int> { ["Mining"] = mining, ["Fishing"] = fishing });
}
