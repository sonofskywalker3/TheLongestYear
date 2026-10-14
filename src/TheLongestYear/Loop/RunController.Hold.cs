using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;
using TheLongestYear.Core.Day28;
using TheLongestYear.Core.Ending;

namespace TheLongestYear.Loop
{
    internal sealed partial class RunController
    {
        /// <summary>Year One Ending (spec 2026-09-06 section 5): after the event and the shrine spend.
        /// Loop again resets right here; Keep playing marks the win, hands vanilla its post-completion
        /// world, arms the Spring 1 year-2 wall, and lets the Junimos say what comes next.</summary>
        public void OnEndingFinished() => TryOpenShrineThenContinue(ShowEndingChoice);

        /// <summary>The Year 2 wall: one response, Loop again. ESC also selects the last (only)
        /// response, so there is no way past it on this version; quitting re-shows it next load.</summary>
        public void ShowYear2Wall()
        {
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                // No location on this frame means the wall cannot be drawn, but the day still has to
                // start: returning here left the player on a Spring 1 with no season roll and no
                // planning hub. Year2WallArmed is untouched, so the wall re-asks next morning.
                _monitor.Log("Year 2 wall: no location yet; will retry next morning.", LogLevel.Warn);
                DoDayStartSeasonAndHub();
                return;
            }
            var responses = new[] { new StardewValley.Response("loop", Strings.Get("dialog.year2wall.loop")) };
            loc.createQuestionDialogue(Strings.Get("dialog.year2wall.prompt"), responses, (Farmer who, string key) =>
            {
                _monitor.Log("Year 2 wall: Loop again.", LogLevel.Info);
                _store.State.VictoryAcknowledged = false;
                _store.State.Year2WallArmed = false;
                _store.Save();
                FinalizeReset("year 2 wall");
            });
        }

        public void DebugShowYear2Wall() => ShowYear2Wall();

        /// <summary>The ending's continuation choice: start a new loop (PerformReset) or keep playing
        /// this year. Uses vanilla's <c>createQuestionDialogue</c> so the prompt renders identically to
        /// other in-world Y/N choices the player has seen.</summary>
        private void ShowEndingChoice()
        {
            var responses = new[]
            {
                new StardewValley.Response("newLoop",     Strings.Get("dialog.ending.new-loop")),
                new StardewValley.Response("keepPlaying", Strings.Get("dialog.ending.keep-playing")),
            };
            string prompt = Strings.Get("dialog.ending.prompt",
                new Dictionary<string, string> { ["loopline"] = WinSummary.LoopLine(Run.RunNumber) });
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                // Defensive: no location to host the dialogue. Default to keep-playing
                // (the safer choice, it never destroys the won run's state silently).
                _monitor.Log("Ending choice: no currentLocation, defaulting to Keep playing.", LogLevel.Warn);
                ApplyKeepPlaying();
                return;
            }
            loc.createQuestionDialogue(prompt, responses, (Farmer who, string key) =>
            {
                if (key == "newLoop")
                {
                    _monitor.Log("Ending choice: Loop again.", LogLevel.Info);
                    // Inside the answer callback the DialogueBox is still the active menu, so the
                    // books would be refused and the reset would wipe recipes and herd animals
                    // unbanked. The shrine already ran before this question; defer the rest a tick.
                    DeferContinue(ContinueAfterResetSpend);
                }
                else
                {
                    _monitor.Log("Ending choice: Keep playing.", LogLevel.Info);
                    ApplyKeepPlaying();
                }
            });
        }

        /// <summary>"Keep playing" branch of the ending choice. Marks VictoryAcknowledged (no further
        /// win nights), arms the Spring 1 year-2 wall, gives vanilla its post-completion world via the
        /// CC-completed event flag, then runs the normal day-start flow once the Junimo lines close.</summary>
        private void ApplyKeepPlaying()
        {
            _store.State.VictoryAcknowledged = true;
            _store.State.Year2WallArmed = true;
            // Vanilla's post-completion world for THIS year only (spec section 3). PostCompletionEvents
            // keeps the id out of SeenEventsEver and out of the next loop's re-seed.
            if (!Game1.player.eventsSeen.Contains(PostCompletionEvents.CeremonyEventId))
                Game1.player.eventsSeen.Add(PostCompletionEvents.CeremonyEventId);
            _store.Save();   // persist immediately, no save-scum revert
            // One box only: the second line (the Spring 1 promise) was cut, the Junimos now
            // close the hall scene with it (Jeff, 2026-09-10).
            var lines = new List<string> { Strings.Get("dialog.ending.keep-1") };
            Game1.afterDialogues = () => { Game1.afterDialogues = null; DoDayStartSeasonAndHub(); };
            // drawObjectDialogue, not a raw DialogueBox assignment: vanilla sets dialogueUp, blocks
            // movement, and emergency-shuts-down whatever menu was up before the box opens.
            Game1.drawObjectDialogue(lines);
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
