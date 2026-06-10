using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Per-loop mushrooms-vs-bats re-choice, without replaying the Demetrius cutscene.
    ///
    /// Event-hygiene pass (2026-06-10): the Demetrius cave scene (event 65) now plays ONCE per
    /// playthrough — from loop 2 on it stays in the eventsSeen reseed like every other watched
    /// scene. The mechanic it carried (choosing the cave's gift) still has to re-offer each loop
    /// because <see cref="FarmerReset"/> clears <c>caveChoice</c>; this prompt is the low-friction
    /// replacement: walk into the farm cave with no gift chosen this loop and a one-line question
    /// dialogue offers the choice. Applies exactly what vanilla's event answer does
    /// (Event.hostActionChooseCave, decompile Event.cs:12819): mushrooms → caveChoice 2 +
    /// FarmCave.setUpMushroomHouse(); bats → caveChoice 1.
    ///
    /// Gates:
    ///   - TLY-active saves only (dormant rule), main player only.
    ///   - caveChoice still unchosen (0) — once picked, the prompt never reappears this loop.
    ///   - Event 65 already seen (this run's eventsSeen — reseeded on loop 2+): the first-ever
    ///     reveal stays Demetrius' scene; we never preempt it on loop 1.
    ///   - "Decide later" is always offered; re-entering the cave re-asks.
    /// </summary>
    internal sealed class CaveChoicePrompt
    {
        private const string DemetriusCaveEventId = "65";
        private const string AnswerMushrooms = "tly_cave_mushrooms";
        private const string AnswerBats = "tly_cave_bats";
        private const string AnswerLater = "tly_cave_later";

        private readonly IMonitor _monitor;

        public CaveChoicePrompt(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            helper.Events.Player.Warped += OnWarped;
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!RunActivation.IsActive) return;
            if (!Context.IsMainPlayer || !e.IsLocalPlayer) return;
            if (e.NewLocation is not FarmCave cave) return;
            if (Game1.MasterPlayer.caveChoice.Value != 0) return;
            if (!Game1.player.eventsSeen.Contains(DemetriusCaveEventId)) return;
            if (Game1.eventUp || Game1.farmEvent != null || Game1.activeClickableMenu != null) return;

            cave.createQuestionDialogue(
                "The cave waits, familiar and patient. What should it nurture this year?",
                new[]
                {
                    new Response(AnswerMushrooms, "Mushrooms"),
                    new Response(AnswerBats, "Fruit bats"),
                    new Response(AnswerLater, "(Decide later)"),
                },
                OnAnswer);
        }

        private void OnAnswer(Farmer who, string answerKey)
        {
            switch (answerKey)
            {
                case AnswerMushrooms:
                    Game1.MasterPlayer.caveChoice.Value = 2;
                    (Game1.getLocationFromName("FarmCave") as FarmCave)?.setUpMushroomHouse();
                    _monitor.Log("Cave re-choice: mushrooms (caveChoice=2, mushroom boxes placed).", LogLevel.Info);
                    break;
                case AnswerBats:
                    Game1.MasterPlayer.caveChoice.Value = 1;
                    _monitor.Log("Cave re-choice: fruit bats (caveChoice=1).", LogLevel.Info);
                    break;
                default:
                    _monitor.Log("Cave re-choice deferred (re-enter the cave to choose).", LogLevel.Trace);
                    break;
            }
        }
    }
}
