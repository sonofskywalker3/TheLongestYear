using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What the real save has, read by the glue just before a darkness pick (spec 2026-09-15
/// Part B, section 1.1). The fairness rule checks the model's conditions against this: what the
/// player already has costs nothing, what they lack adds days or rules a route out, by level.</summary>
/// <param name="RecipesKnown">Cooking and crafting recipe names the player knows.</param>
/// <param name="Buildings">Building type names on the farm, plus every type each one upgraded from.</param>
/// <param name="MachinesOwned">Qualified ids of big craftables placed anywhere or held in a chest or the bag.</param>
/// <param name="CraftableMachines">Qualified ids of the items the player's known crafting recipes make.</param>
/// <param name="AnimalsOwned">Animal type names the player owns.</param>
/// <param name="AnimalFriendship">Best friendship per owned animal type.</param>
/// <param name="MailFlags">Mail flags received (ccPantry, ccVault and the rest).</param>
/// <param name="DeepestMineFloor">Deepest regular mine floor reached.</param>
/// <param name="Skills">Level per skill name (Farming, Fishing, Foraging, Mining, Combat, Luck).</param>
public sealed record SaveSnapshot(
    IReadOnlySet<string> RecipesKnown,
    IReadOnlySet<string> Buildings,
    IReadOnlySet<string> MachinesOwned,
    IReadOnlySet<string> CraftableMachines,
    IReadOnlySet<string> AnimalsOwned,
    IReadOnlyDictionary<string, int> AnimalFriendship,
    IReadOnlySet<string> MailFlags,
    int DeepestMineFloor,
    IReadOnlyDictionary<string, int> Skills)
{
    public static readonly SaveSnapshot Empty = new(
        new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
        new HashSet<string>(), new Dictionary<string, int>(), new HashSet<string>(), 0, new Dictionary<string, int>());

    public int SkillLevel(string skill) => Skills.TryGetValue(skill, out int level) ? level : 0;
}
