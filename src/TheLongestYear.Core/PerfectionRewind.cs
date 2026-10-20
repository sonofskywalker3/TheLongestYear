using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Everything that counts toward Perfection resets every loop (Jeff, 2026-10-20). This is
/// the audit table of every term <c>Utility.percentGameComplete</c> (Utility.cs:3065) adds, the
/// records it reads and how the rewind treats each one, plus the clear rules FarmerReset applies
/// to the three farmer collections that used to survive the rewind (fish caught, dishes cooked,
/// the Monster Slayer flag).</summary>
public static class PerfectionRewind
{
    public enum Handling
    {
        /// <summary>The rewind empties it and nothing puts it back.</summary>
        Cleared,
        /// <summary>The rewind empties it; only what the player bought at the upgrade menu returns.</summary>
        ClearedExceptBoughtKeep,
        /// <summary>Survives the rewind. No input may have this.</summary>
        Persists,
    }

    public sealed record Input(string Name, int Weight, IReadOnlyList<string> Records, Handling Handling, string Where);

    /// <summary>In the order percentGameComplete adds them; weights total 100.</summary>
    public static readonly IReadOnlyList<Input> Inputs = new[]
    {
        new Input("Shipped", 15, new[] { "basicShipped" }, Handling.Cleared, "FarmerReset"),
        new Input("Obelisks", 4, new[] { "farm buildings" }, Handling.Cleared, "new farm on loadForNewGame; obelisks are not keepable"),
        new Input("Gold Clock", 10, new[] { "farm buildings" }, Handling.Cleared, "new farm on loadForNewGame; the clock is not keepable"),
        new Input("Monster Slayer", 10, new[] { "hasCompletedAllMonsterSlayerQuests", "specificMonstersKilled" }, Handling.Cleared, "FarmerReset"),
        new Input("Great friends", 11, new[] { "friendshipData" }, Handling.Cleared, "FarmerReset"),
        new Input("Farmer level", 5, new[] { "skill levels" }, Handling.ClearedExceptBoughtKeep, "FarmerReset, kept skill levels re-granted"),
        new Input("Stardrops", 10, new[] { "mailReceived CF_* / gotMaxStamina" }, Handling.ClearedExceptBoughtKeep, "FarmerReset, kept Stardrop flags re-added"),
        new Input("Cooking", 10, new[] { "cookingRecipes", "recipesCooked" }, Handling.Cleared, "FarmerReset; banked cookbook recipes return known but never cooked"),
        new Input("Crafting", 10, new[] { "craftingRecipes" }, Handling.Cleared, "FarmerReset; banked and kept recipes return with a count of 0"),
        new Input("Fish caught", 10, new[] { "fishCaught" }, Handling.Cleared, "FarmerReset"),
        new Input("Golden walnuts", 5, new[] { "GoldenWalnutsFound" }, Handling.Cleared, "WorldResetService.NetWorldState"),
    };

    /// <summary>The whole fish collection goes, not only the catch-limited legendaries: the
    /// collection tab and the Perfection fish share start from nothing each year, and a legendary
    /// can be caught again because its CatchLimit check reads this same record.</summary>
    public static IReadOnlyList<string> FishIdsToClear(IEnumerable<string> caughtIds) => caughtIds.ToList();

    /// <summary>Every dish cooked in an earlier year is forgotten, so a banked recipe counts toward
    /// Perfection only once it is cooked again this year.</summary>
    public static IReadOnlyList<string> CookedIdsToClear(IEnumerable<string> cookedIds) => cookedIds.ToList();

    /// <summary>Vanilla recomputes the flag from the kill counts only on a save load or a kill that
    /// finishes a goal, so the rewind sets it itself. With the counts empty no goal can be done.</summary>
    public static bool SlayerDoneAfterRewind(bool killCountsCleared, bool wasDone) => !killCountsCleared && wasDone;
}
