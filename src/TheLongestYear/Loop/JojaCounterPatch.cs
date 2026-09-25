using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;

namespace TheLongestYear.Loop
{
    /// <summary>Morris's offer at the store (spec 2026-09-25-joja-offer-design): Morris's counter
    /// (tile action JoinJoja) asks for the answer or turns the player away, and the cashier (tile
    /// action JojaShop) sells nothing until the player has answered, and nothing ever after a No.
    /// Joja never helps the player anywhere in the mod.</summary>
    internal static class JojaCounterPatch
    {
        private static IMonitor _monitor;
        private static MetaStore _meta;
        private static System.Action _startBadEnding;

        public static void Connect(IMonitor monitor, MetaStore meta, System.Action startBadEnding)
        { _monitor = monitor; _meta = meta; _startBadEnding = startBadEnding; }

        /// <summary>Debug: run the same dialogue-building path as the counter prefix.</summary>
        public static void DebugCounter() => OpenCounter();

        /// <summary>Debug: run the same dialogue-building path as the cashier prefix.</summary>
        public static void DebugCashier() => OpenCashier();

        private static void OpenCounter()
        {
            NPC morris = JojaMart.Morris;
            if (morris == null) return;
            morris.CurrentDialogue.Clear();
            RunState run = _meta.Run;
            switch (JojaOffer.MorrisLine(run, _meta.State, run.RunNumber))
            {
                case JojaMorrisLine.Ask:
                    string q = $"$y '{Strings.Get("joja.morris.ask")}_{Strings.Get("joja.morris.accept")}_ _{Strings.Get("joja.morris.decline")}_ '";
                    var ask = new Dialogue(morris, null, q);
                    ask.answerQuestionBehavior = Answer;
                    morris.setNewDialogue(ask);
                    break;
                case JojaMorrisLine.RefuseAgain:
                    morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.refuse-again")));
                    break;
                default:
                    morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.position-filled")));
                    break;
            }
            Game1.drawDialogue(morris);
        }

        private static void OpenCashier()
        {
            string key = JojaOffer.CashierLine(_meta.State) == JojaCashierLine.Refused
                ? "joja.cashier.refused" : "joja.cashier.undecided";
            Game1.drawObjectDialogue(Strings.Get(key));
        }

        [HarmonyPatch(typeof(JojaMart), nameof(JojaMart.checkAction))]
        internal static class Counter
        {
            private static bool Prefix(JojaMart __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (__instance.doesTileHaveProperty(tileLocation.X, tileLocation.Y, "Action", "Buildings") != "JoinJoja") return true;
                if (JojaMart.Morris == null) return true;
                OpenCounter();
                __result = true;
                return false;
            }
        }

        /// <summary>0 = accept, 1 = decline (the order the $y answers were written in).</summary>
        private static bool Answer(int index)
        {
            NPC morris = JojaMart.Morris;
            if (index == 0)
            {
                _monitor.Log("Joja: the player accepted Morris's offer; bad ending.", LogLevel.Info);
                // Leave the dialogue box first; the ending starts on the next tick.
                DelayedAction.functionAfterDelay(() => _startBadEnding?.Invoke(), 100);
                return true;
            }
            RunState run = _meta.Run;
            JojaOffer.Reject(_meta.State, run.RunNumber);
            _monitor.Log($"Joja: the player turned Morris down in loop {run.RunNumber}; refused service for good.", LogLevel.Info);
            morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.refuse")));
            Game1.drawDialogue(morris);
            return false;
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.performAction),
            new System.Type[] { typeof(string[]), typeof(Farmer), typeof(xTile.Dimensions.Location) })]
        internal static class Cashier
        {
            private static bool Prefix(GameLocation __instance, string[] action, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (__instance is not JojaMart || action == null || action.Length == 0 || action[0] != "JojaShop") return true;
                OpenCashier();
                __result = true;
                return false;
            }
        }
    }
}
