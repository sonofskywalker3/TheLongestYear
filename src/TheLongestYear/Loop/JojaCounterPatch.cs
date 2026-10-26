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
    /// Joja never helps the player anywhere in the mod. Once this loop's year is won
    /// (JojaOffer.YearWon) the offer is over and both fall through to vanilla.</summary>
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
            JojaMorrisLine line = JojaOffer.MorrisLine(run, _meta.State, run.RunNumber);
            switch (line)
            {
                case JojaMorrisLine.Ask:
                    morris.setNewDialogue(Question(morris, Strings.Get("joja.morris.ask"), Answer));
                    break;
                case JojaMorrisLine.RefuseAfterLetter:
                    // The fourth decision letter turned the player away; he never said it to their face.
                    morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.refuse-letter")));
                    break;
                case JojaMorrisLine.Reconsider:
                    // After a No in this loop he asks once more each visit; Yes is still the bad ending.
                    morris.setNewDialogue(Question(morris, Strings.Get("joja.morris.reconsider"), AnswerReconsider));
                    break;
                default:
                    morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.position-filled")));
                    break;
            }
            JojaOffer.MarkMorrisLineShown(_meta.State, line);
            Game1.drawDialogue(morris);
        }

        private static void OpenCashier()
        {
            // Extracted to a paren-free ternary condition so the literal keys are visible to
            // I18nGuardTests' Strings.Get(...) scanner (its regex excludes parens from the
            // condition, and JojaOffer.CashierLine(...) is a call).
            JojaCashierLine line = JojaOffer.CashierLine(_meta.State);
            Game1.drawObjectDialogue(Strings.Get(line == JojaCashierLine.Refused
                ? "joja.cashier.refused" : "joja.cashier.undecided"));
        }

        [HarmonyPatch(typeof(JojaMart), nameof(JojaMart.checkAction))]
        internal static class Counter
        {
            private static bool Prefix(JojaMart __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (JojaOffer.YearWon(_meta.Run, _meta.State)) return true;   // the offer is over: vanilla
                if (__instance.doesTileHaveProperty(tileLocation.X, tileLocation.Y, "Action", "Buildings") != "JoinJoja") return true;
                if (JojaMart.Morris == null) return true;
                OpenCounter();
                __result = true;
                return false;
            }
        }

        private const int AcceptIndex = 0;

        /// <summary>Morris's Yes/No question: accept first, decline second (see <see cref="AcceptIndex"/>).
        /// The empty quick replies leave the answer to <paramref name="answer"/>.</summary>
        private static Dialogue Question(NPC morris, string question, Dialogue.onAnswerQuestion answer)
        {
            string q = $"$y '{question}_{Strings.Get("joja.morris.accept")}_ _{Strings.Get("joja.morris.decline")}_ '";
            return new Dialogue(morris, null, q) { answerQuestionBehavior = answer };
        }

        /// <summary>The first ask: a No turns the player away for this loop.</summary>
        private static bool Answer(int index)
        {
            if (index == AcceptIndex) return Accept();
            RunState run = _meta.Run;
            JojaOffer.Reject(_meta.State, run.RunNumber);
            _monitor.Log($"Joja: the player turned Morris down in loop {run.RunNumber}; refused service for good.", LogLevel.Info);
            return Say(Strings.Get("joja.morris.refuse"));
        }

        /// <summary>Asked again after a No: Yes is the bad ending, No keeps the ban.</summary>
        private static bool AnswerReconsider(int index)
        {
            if (index == AcceptIndex) return Accept();
            _monitor.Log("Joja: the player turned Morris down again.", LogLevel.Info);
            return Say(Strings.Get("joja.morris.refuse-final"));
        }

        private static bool Say(string line)
        {
            NPC morris = JojaMart.Morris;
            morris.setNewDialogue(new Dialogue(morris, null, line));
            Game1.drawDialogue(morris);
            return false;
        }

        private static bool Accept()
        {
            if (JojaOffer.YearWon(_meta.Run, _meta.State))
            {
                // The year was won while the question was open: the offer is over, no bad ending.
                _monitor.Log("Joja: a Yes after the year was won; the offer is over, no bad ending.", LogLevel.Info);
                return true;
            }
            _monitor.Log("Joja: the player accepted Morris's offer; bad ending.", LogLevel.Info);
            // Leave the dialogue box first; the ending starts on the next tick.
            DelayedAction.functionAfterDelay(() => _startBadEnding?.Invoke(), 100);
            return true;
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.performAction),
            new System.Type[] { typeof(string[]), typeof(Farmer), typeof(xTile.Dimensions.Location) })]
        internal static class Cashier
        {
            private static bool Prefix(GameLocation __instance, string[] action, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (JojaOffer.YearWon(_meta.Run, _meta.State)) return true;   // the offer is over: vanilla
                if (__instance is not JojaMart || action == null || action.Length == 0 || action[0] != "JojaShop") return true;
                OpenCashier();
                __result = true;
                return false;
            }
        }
    }
}
