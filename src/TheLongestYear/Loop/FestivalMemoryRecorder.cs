using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Records what the player did at each festival this loop into RunState.FestivalLog
    /// (deja-vu phase 2, spec "Vanilla hooks (recording)"). Each result hook then hands the result to
    /// <see cref="FestivalMemoryInjector"/> for the post-result line. Decompile references are to
    /// decompiled-pc Event.cs.</summary>
    internal static class FestivalMemoryRecorder
    {
        private const string GoldenPumpkinId = "(O)373";

        private static void Log(string text) => FestivalMemoryContext.Monitor?.Log(text, LogLevel.Trace);

        /// <summary>Festival free roam opens (Event.cs 10947): attendance, then the festival-day lines.</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.setUpPlayerControlSequence))]
        internal static class OpenPatch
        {
            private static void Postfix(Event __instance, string id)
            {
                try
                {
                    if (id == null || !FestivalIds.ByControlSequence.TryGetValue(id, out string festival)) return;
                    if (FestivalMemoryContext.FestivalOf(__instance) != festival) return;
                    if (!FestivalMemoryContext.CanRecord) return;
                    FestivalMemoryStore.RecordAttendance(FestivalMemoryContext.Run, festival);
                    FestivalMemoryInjector.OnFestivalOpen(__instance, festival);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory open hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The egg hunt winner is named (Event.cs 12824). Single player wins at 9 eggs.</summary>
        [HarmonyPatch(typeof(Event), "eggHuntWinner")]
        internal static class EggHuntPatch
        {
            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.EggFestival || !FestivalMemoryContext.CanRecord) return;
                    int eggs = Game1.player.festivalScore;
                    string outcome = FestivalOutcomes.ClassifyEggHunt(eggs);
                    FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.EggFestival, outcome, score: eggs);
                    Log($"Festival memory recorded: egg hunt {outcome} ({eggs} eggs).");
                    FestivalMemoryInjector.AfterScriptedResult(__instance, FestivalIds.EggFestival, outcome);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory egg hunt hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The Flower Dance starts (Event.cs 11148): the partner, or none.</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.setUpFestivalMainEvent))]
        internal static class DancePatch
        {
            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.FlowerDance || !FestivalMemoryContext.CanRecord) return;
                    NPC partner = Game1.player.dancePartner.Value as NPC;
                    RunState run = FestivalMemoryContext.Run;
                    if (partner == null)
                    {
                        FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.NoPartner);
                        Log("Festival memory recorded: Flower Dance, no partner.");
                        return;
                    }
                    FestivalMemoryStore.RecordOutcome(run, FestivalIds.FlowerDance, FestivalOutcome.Danced, npc: partner.Name);
                    int total = FamiliarityRollup.AddBonus(FestivalMemoryContext.Meta, partner.Name, FamiliarityRollup.DancePartnerPoints);
                    Log($"Festival memory recorded: danced with {partner.Name} (familiarity now {total}).");
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory dance hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The Governor tastes the soup (Event.cs 12772): the method writes
        /// "switchEvent governorReaction&lt;N&gt;" into the next command, N 0 to 6.</summary>
        [HarmonyPatch(typeof(Event), "governorTaste")]
        internal static class LuauPatch
        {
            private const string ReactionPrefix = "switchEvent governorReaction";

            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.Luau || !FestivalMemoryContext.CanRecord) return;
                    int next = __instance.CurrentCommand + 1;
                    if (__instance.eventCommands == null || next >= __instance.eventCommands.Length) return;
                    string command = __instance.eventCommands[next];
                    if (command == null || !command.StartsWith(ReactionPrefix, StringComparison.Ordinal)
                        || !int.TryParse(command.Substring(ReactionPrefix.Length), out int level)) return;
                    string outcome = FestivalOutcomes.ClassifyLuau(level);
                    Item ingredient = Game1.player.team.luauIngredients.FirstOrDefault(i => i != null);
                    FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.Luau, outcome,
                        ingredient?.QualifiedItemId ?? "", ingredient?.Quality ?? 0);
                    Log($"Festival memory recorded: Luau {outcome} (level {level}, {ingredient?.QualifiedItemId ?? "no ingredient"}).");
                    FestivalMemoryInjector.AfterLuau(__instance, outcome);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory Luau hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The grange is judged (Event.cs 11410). Vanilla treats an empty grangeDisplay as
        /// skipped; a display opened and emptied again holds nine nulls, which counts the same.</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.interpretGrangeResults))]
        internal static class GrangePatch
        {
            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.Fair || !FestivalMemoryContext.CanRecord) return;
                    var display = Game1.player.team.grangeDisplay;
                    bool empty = display.Count == 0 || display.All(i => i == null);
                    int score = __instance.grangeScore;
                    string outcome = FestivalOutcomes.ClassifyGrange(score, empty);
                    FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.Fair, outcome, score: score);
                    Log($"Festival memory recorded: grange {outcome} (score {score}).");
                    FestivalMemoryInjector.AfterGrange(__instance, outcome);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory grange hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The ice fishing winner is named (Event.cs 12889). A win is 5 fish.</summary>
        [HarmonyPatch(typeof(Event), "iceFishingWinner")]
        internal static class IceFishingPatch
        {
            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.IceFestival || !FestivalMemoryContext.CanRecord) return;
                    int fish = Game1.player.festivalScore;
                    string outcome = FestivalOutcomes.ClassifyIceFishing(fish);
                    FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.IceFestival, outcome, score: fish);
                    Log($"Festival memory recorded: ice fishing {outcome} ({fish} fish).");
                    FestivalMemoryInjector.AfterScriptedResult(__instance, FestivalIds.IceFestival, outcome);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory ice fishing hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The secret gift (Event.cs 12476). The method nulls secretSantaRecipient once the gift
        /// is given, so the prefix keeps the name.</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.chooseSecretSantaGift))]
        internal static class SecretGiftPatch
        {
            private static void Prefix(Event __instance, out string __state) => __state = __instance?.secretSantaRecipient?.Name;

            private static void Postfix(Event __instance, Item i, string __state)
            {
                try
                {
                    if (__state == null || i is not StardewValley.Object || __instance.secretSantaRecipient != null) return;
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.WinterStar || !FestivalMemoryContext.CanRecord) return;
                    NPC recipient = __instance.getActorByName(__state);
                    if (recipient == null) return;
                    string outcome = FestivalOutcomes.ClassifyGift(recipient.getGiftTasteForThisItem(i));
                    FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.WinterStar, outcome,
                        i.QualifiedItemId, i.Quality, __state);
                    int total = FamiliarityRollup.AddBonus(FestivalMemoryContext.Meta, __state, FamiliarityRollup.WinterStarGiftPoints);
                    Log($"Festival memory recorded: secret gift {i.QualifiedItemId} to {__state}, {outcome} (familiarity now {total}).");
                    FestivalMemoryInjector.AfterSecretGift(__instance, recipient);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory secret gift hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>The golden pumpkin from the Spirit's Eve maze chest (Event.cs 11028), seen as it
        /// lands in the inventory so the chest needs no patch.</summary>
        public static void OnInventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            try
            {
                if (!e.IsLocalPlayer || !FestivalMemoryContext.CanRecord) return;
                if (FestivalMemoryContext.FestivalOf(Game1.CurrentEvent) != FestivalIds.SpiritsEve) return;
                if (!e.Added.Any(i => i?.QualifiedItemId == GoldenPumpkinId)) return;
                FestivalMemoryStore.RecordOutcome(FestivalMemoryContext.Run, FestivalIds.SpiritsEve, FestivalOutcome.Pumpkin);
                Log("Festival memory recorded: golden pumpkin found.");
            }
            catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory pumpkin hook failed: {ex}", LogLevel.Error); }
        }
    }
}
