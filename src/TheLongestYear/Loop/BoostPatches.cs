using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>Fast Friends (spec 2.8): friendship gains x1.5 while active. Runs before vanilla's
    /// own Book_Friendship 1.1, so the two compound (accepted: the book is a permanent the player
    /// also paid for).</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.changeFriendship))]
    internal static class FastFriendsPatch
    {
        private const double Factor = 1.5;

        // ReSharper disable once InconsistentNaming
        private static void Prefix(ref int amount)
        {
            if (amount <= 0) return;
            if (BoostEffectsService.FastFriendsActive?.Invoke() != true) return;
            amount = (int)System.Math.Ceiling(amount * Factor);
        }
    }

    /// <summary>Second Wind (spec 2.5): the sleep that ends the day it was bought on costs no
    /// stamina and leaves no Exhausted status. Farmer.dayupdate(int timeWentToSleep) holds the
    /// penalty block (Farmer.cs:3520-3545): clear exhausted first, and make both bed-time reads
    /// say 2400 so the late-sleep deduction and the 2700 halving never fire. Nothing else in
    /// dayupdate reads those two values before the block. Never a refill after the fact.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.dayupdate))]
    internal static class SecondWindPatch
    {
        private const int Midnight = 2400;

        // ReSharper disable once InconsistentNaming
        private static void Prefix(Farmer __instance, ref int timeWentToSleep)
        {
            if (BoostEffectsService.SecondWindTonight?.Invoke() != true) return;
            __instance.exhausted.Value = false;
            __instance.timeWentToBed.Value = 0;
            if (timeWentToSleep > Midnight) timeWentToSleep = Midnight;
            PatchLog.Info("Second Wind: late-sleep penalty and Exhausted skipped for tonight.");
        }
    }
}
