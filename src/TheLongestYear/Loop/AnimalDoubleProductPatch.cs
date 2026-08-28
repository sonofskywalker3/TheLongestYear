using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Kitchen bonus (animal_double_product): 20% chance an animal gives a second product
    /// that day. Two shapes, because the game has two produce paths (FarmAnimal.dayUpdate,
    /// decompile FarmAnimal.cs:929):
    ///  - animals with currentProduce (cows, goats, sheep, pigs): when dayUpdate sets a NEW
    ///    currentProduce and the roll passes, the animal id and produce id are recorded in
    ///    RunState.DoubleProduceToday; when the pail, shears or truffle dig clears currentProduce,
    ///    the record puts it back once (MilkPail.DoFunction, Shears.DoFunction, FarmAnimal.DigUpProduce).
    ///  - overnight droppers (chickens, ducks, rabbits, dinosaurs): the produce object lands in
    ///    the coop during dayUpdate; a second copy is spawned beside it.
    /// Records are cleared on DayEnding, before the night's dayUpdate writes new ones, and on a
    /// run reset.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.dayUpdate))]
    internal static class AnimalDoubleProductPatch
    {
        public const string BonusId = "animal_double_product";
        private const double Chance = 0.20;
        private static Func<RunState> _run;

        public static void Connect(Func<RunState> run) => _run = run;

        internal sealed class State
        {
            public string ProduceBefore;
            public HashSet<Vector2> Tiles;
            public GameLocation Indoors;
        }

        private static void Prefix(FarmAnimal __instance, out State __state)
        {
            GameLocation indoors = null;
            try { indoors = __instance?.home?.GetIndoors(); }
            catch (Exception) { indoors = null; }
            __state = new State
            {
                ProduceBefore = __instance?.currentProduce?.Value,
                Indoors = indoors,
                Tiles = indoors?.objects != null ? new HashSet<Vector2>(indoors.objects.Keys) : null,
            };
        }

        private static void Postfix(FarmAnimal __instance, State __state)
        {
            if (!ActiveEffectsProvider.ActiveBonus(BonusId) || __instance == null || __state == null) return;
            RunState run = _run?.Invoke();
            if (run == null) return;
            try
            {
                string produce = __instance.currentProduce?.Value;
                if (produce != null && produce != __state.ProduceBefore)
                {
                    if (Game1.random.NextDouble() >= Chance) return;
                    run.RecordDoubleProduce(__instance.myID.Value, produce);
                    PatchLog.Info($"{BonusId}: {__instance.displayName} will give a second {produce} today.");
                    return;
                }
                if (__state.Indoors?.objects == null || __state.Tiles == null) return;
                foreach (Vector2 tile in __state.Indoors.objects.Keys)
                {
                    if (__state.Tiles.Contains(tile)) continue;
                    StardewValley.Object dropped = __state.Indoors.objects[tile];
                    if (dropped == null) continue;
                    if (Game1.random.NextDouble() >= Chance) return;
                    var copy = (StardewValley.Object)dropped.getOne();
                    Utility.spawnObjectAround(__instance.Tile, copy, __state.Indoors);
                    PatchLog.Info($"{BonusId}: {__instance.displayName} left a second {dropped.Name}.");
                    return;
                }
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"{BonusId}: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Shared by the three collect postfixes: if this animal is owed a second
        /// product and its currentProduce was just cleared, put it back once.</summary>
        internal static void RestoreIfRecorded(FarmAnimal animal)
        {
            if (animal == null || animal.currentProduce?.Value != null) return;
            RunState run = _run?.Invoke();
            if (run == null || !run.TryTakeDoubleProduce(animal.myID.Value, out string produce)) return;
            animal.currentProduce.Value = produce;
            animal.ReloadTextureIfNeeded();
            PatchLog.Info($"{BonusId}: {animal.displayName} has a second {produce} ready.");
        }
    }

    [HarmonyPatch(typeof(StardewValley.Tools.MilkPail), nameof(StardewValley.Tools.MilkPail.DoFunction))]
    internal static class MilkPailDoublePatch
    {
        private static void Postfix(StardewValley.Tools.MilkPail __instance)
            => AnimalDoubleProductPatch.RestoreIfRecorded(__instance?.animal);
    }

    [HarmonyPatch(typeof(StardewValley.Tools.Shears), nameof(StardewValley.Tools.Shears.DoFunction))]
    internal static class ShearsDoublePatch
    {
        private static void Postfix(StardewValley.Tools.Shears __instance)
            => AnimalDoubleProductPatch.RestoreIfRecorded(__instance?.animal);
    }

    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.DigUpProduce))]
    internal static class DigUpDoublePatch
    {
        private static void Postfix(FarmAnimal __instance)
            => AnimalDoubleProductPatch.RestoreIfRecorded(__instance);
    }
}
