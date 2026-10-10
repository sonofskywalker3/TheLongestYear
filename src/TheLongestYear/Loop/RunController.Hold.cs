using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    internal sealed partial class RunController
    {
        /// <summary>Post-win choice dialog: after the shrine menu closes, ask the player
        /// whether to start a new loop (triggers PerformReset) or keep playing this run
        /// indefinitely (sets VictoryAcknowledged + falls through to the normal day-start
        /// flow with no reset). Uses vanilla's <c>createQuestionDialogue</c> so the prompt
        /// renders identically to other in-world Y/N choices the player has seen.</summary>
        private void ShowKeepPlayingChoice()
        {
            var responses = new[]
            {
                new StardewValley.Response("newLoop",     Strings.Get("dialog.win.new-loop")),
                new StardewValley.Response("keepPlaying", Strings.Get("dialog.win.keep-playing"))
            };
            string loopLine = WinSummary.LoopLine(Run.RunNumber);
            string prompt = Strings.Get("dialog.win.prompt",
                new Dictionary<string, string> { ["loopline"] = loopLine });

            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                // Defensive: no location to host the dialogue. Default to keep-playing
                // (the safer choice — never destroys the won run's state silently).
                _monitor.Log("Post-win choice: no currentLocation available, defaulting to 'Keep playing'.", LogLevel.Warn);
                ApplyKeepPlaying();
                return;
            }

            loc.createQuestionDialogue(prompt, responses, (Farmer who, string key) =>
            {
                if (key == "newLoop")
                {
                    _monitor.Log("Post-win choice: 'Start a new loop' — triggering reset.", LogLevel.Info);
                    // Inside the answer callback the DialogueBox is still the active menu, so the
                    // books would be refused and the reset would wipe recipes and herd animals
                    // unbanked. The shrine already ran before this question; defer the rest a tick.
                    DeferContinue(ContinueAfterResetSpend);
                }
                else
                {
                    _monitor.Log("Post-win choice: 'Keep playing this run' — VictoryAcknowledged set.", LogLevel.Info);
                    ApplyKeepPlaying();
                }
            });
        }

        /// <summary>"Keep playing" branch of the post-win choice. Marks VictoryAcknowledged
        /// (suppresses the popup on subsequent Winter 28 wins) and runs the normal day-start
        /// flow so the player lands on Spring 1 Year 2 with the planning hub.</summary>
        private void ApplyKeepPlaying()
        {
            _store.State.VictoryAcknowledged = true;
            _store.Save();   // persist immediately — no save-scum revert
            DoDayStartSeasonAndHub();
        }

        /// <summary>Fail-night "hold the town's wishes" choice (spec 2026-08-24). Asked BEFORE the
        /// shrine so the player can't accidentally spend the JP they meant for the hold. Either
        /// answer runs BundleHold.Apply then continues into the shrine -> reset chain. If the
        /// dialogue is clobbered, the watchdog treats it as reshuffle (today's behaviour). A
        /// NotEnoughJp re-ask is deferred a tick (via _holdReaskPending, drained by
        /// TickShrineWatchdog) instead of being called from inside this callback, because
        /// GameLocation.answerDialogue nulls afterQuestion right after this callback returns and
        /// would wipe the nested dialogue's own callback before the player could answer it.</summary>
        private void ShowHoldChoice()
        {
            MetaState meta = _store.State;
            long cost = BundleHold.NextCost(meta, _config.BundleHoldCosts, meta.EffectiveDifficulty(_config).HoldPriceFactor);
            string keepLabel = cost == 0
                ? Strings.Get("dialog.hold.keep-free")
                : Strings.Get("dialog.hold.keep", new Dictionary<string, string> { ["cost"] = cost.ToString() });
            var responses = new[]
            {
                new StardewValley.Response("keep",      keepLabel),
                new StardewValley.Response("reshuffle", Strings.Get("dialog.hold.reshuffle"))
            };

            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                _monitor.Log("Hold choice: no currentLocation available, defaulting to reshuffle.", LogLevel.Warn);
                ApplyHoldChoice(keep: false);
                return;
            }

            loc.createQuestionDialogue(Strings.Get("dialog.hold.prompt"), responses, (Farmer who, string key) =>
            {
                _menuWatch = null;
                if (key == "keep")
                {
                    BundleHold.HoldResult result = BundleHold.Apply(meta, keep: true, _config.BundleHoldCosts, meta.EffectiveDifficulty(_config).HoldPriceFactor);
                    if (result == BundleHold.HoldResult.NotEnoughJp)
                    {
                        Game1.playSound("cancel");
                        Game1.addHUDMessage(new HUDMessage(Strings.Get("dialog.hold.not-enough-jp",
                            new Dictionary<string, string> { ["cost"] = cost.ToString(), ["have"] = meta.JunimoPoints.ToString() }), HUDMessage.error_type));
                        _holdReaskPending = true;   // re-ask next tick; see ShowHoldChoice's doc comment
                        return;
                    }
                    _monitor.Log($"Hold choice: KEEP (cost {cost} JP, consecutive holds now {meta.ConsecutiveHolds}, seed loop {meta.BundleSeedLoop}).", LogLevel.Info);
                    Game1.playSound("junimoMeep1");
                    AfterHoldChoice(held: true);
                    return;
                }
                ApplyHoldChoice(keep: false);
            });

            // Watchdog: if something replaces the question box before an answer, default to reshuffle.
            if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box)
                _menuWatch = (box, () => ApplyHoldChoice(keep: false));
        }

        private void ApplyHoldChoice(bool keep)
        {
            BundleHold.HoldResult result = BundleHold.Apply(_store.State, keep, _config.BundleHoldCosts, _store.State.EffectiveDifficulty(_config).HoldPriceFactor);
            _monitor.Log($"Hold choice: {result} (seed loop {_store.State.BundleSeedLoop}).", LogLevel.Info);
            AfterHoldChoice(held: keep);
        }

        /// <summary>The board's fate is decided: open the upgrade menu, then reset. Deferred a tick
        /// because this runs inside the hold question's answer callback (see
        /// <see cref="DeferShrineThenContinue"/>). Season pity used to offer a second question
        /// here; it was removed (Jeff, 2026-09-24: players adjust the difficulty themselves).</summary>
        private void AfterHoldChoice(bool held)
        {
            DeferShrineThenContinue(ContinueAfterResetSpend);
        }

        /// <summary>Queue the shrine open for the next tick instead of opening it now.
        /// MUST be used by every caller that runs inside a question’s answer callback (the hold
        /// choice): vanilla calls answerDialogue -> our callback and only
        /// then tryOutro()s the DialogueBox (GameLocation.cs answerDialogue, DialogueBox
        /// receiveLeftClick), so the box is still Game1.activeClickableMenu while we run.
        /// MenuLauncher.CanOpen refuses to open over it, TryOpenShrineThenContinue would take its
        /// fall-through and run the continuation with no shop shown, and even if it did open, the
        /// outro would tear the shrine down. Nexus bug 1123181, regression from 0.12.17 (the hold
        /// prompt put a question in front of a shrine open that used to be called from
        /// OnCutsceneEnded directly). Drained by <see cref="TickShrineWatchdog"/>.</summary>
        private void DeferShrineThenContinue(System.Action onContinue)
        {
            _shrineOpenPending = onContinue;
        }

        /// <summary>Same deferral as <see cref="DeferShrineThenContinue"/> for a continuation that
        /// does not reopen the shrine (the post-win "Start a new loop" answer, whose shrine already
        /// ran before the question). Drained by <see cref="TickShrineWatchdog"/> once no menu is up.</summary>
        private void DeferContinue(System.Action onContinue)
        {
            _continuePending = onContinue;
        }

        /// <summary>Continuation queued by <see cref="DeferContinue"/>.</summary>
        private System.Action _continuePending;

        /// <summary>Continuation owed a shrine open, queued by <see cref="DeferShrineThenContinue"/>
        /// and drained by <see cref="TickShrineWatchdog"/> once no menu is up.</summary>
        private System.Action _shrineOpenPending;

        /// <summary>Try to open the Junimo Shrine menu; on close, run <paramref name="onContinue"/>.
        /// If the menu can't open (cutscene blocking, already-open menu), run onContinue
        /// immediately so the gameplay path never gets stranded waiting for a missed popup.</summary>
        private void TryOpenShrineThenContinue(System.Action onContinue)
        {
            _launcher?.OpenShrineShop();
            if (Game1.activeClickableMenu is TheLongestYear.UI.JunimoShrineMenu shrine)
            {
                _menuWatch = (shrine, onContinue);
                shrine.exitFunction = () =>
                {
                    _menuWatch = null;
                    onContinue();
                };
                return;
            }
            // Menu did not open (blocked or torn down): run the continuation so the night is
            // never stranded, but say so loudly - a silently skipped shrine is Nexus bug 1123181.
            string blockingMenu = Game1.activeClickableMenu?.GetType().Name ?? "none";
            _monitor.Log(
                "Junimo Shrine could not open before the reset; continuing without it (JP stays banked). " +
                $"activeClickableMenu={blockingMenu}, eventUp={Game1.eventUp}.",
                LogLevel.Warn);
            onContinue();
        }
    }
}
