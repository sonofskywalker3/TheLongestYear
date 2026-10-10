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
        /// <summary>Debug: fire the SAME fail-reset flow a day-28 gate miss triggers
        /// (<see cref="OnDayStarted"/> line 175) — open the JP-spend shrine, then on close run
        /// <see cref="ContinueAfterResetSpend"/> (PerformReset + persist). Lets a playtest exercise
        /// the exact spend-at-shrine → reset → reload path without grinding to day 28. Unlike
        /// <c>tly_reset</c> (which resets raw, no shrine), this reproduces the real loop-boundary
        /// purchase flow that the JP-refund bug lived in.</summary>
        public void DebugForceFailReset()
        {
            _monitor.Log("tly_failreset: queuing the day-28 FAIL cutscene (Junimo → shrine → reset).", LogLevel.Info);
            // Set the pending branch so the Day28CutsceneDriver plays the real bedtime scene this
            // tick (the driver polls UpdateTicked, not just DayStarted), exercising the full
            // cutscene → shop → reset → forced-save path from anywhere. tly_reset stays raw.
            _pendingCutscene = Day28Branch.Fail;
        }

        /// <summary>Debug: queue the CONTINUE (gate-passed) day-28 cutscene so a playtest can watch
        /// the "great job, next season" branch without reaching a real passing day-28. Sets the
        /// pending branch; the Day28CutsceneDriver plays the scene this tick and OnCutsceneEnded
        /// rolls into DoDayStartSeasonAndHub (no shop, no reset).</summary>
        public void DebugForceContinueCutscene()
        {
            _monitor.Log("tly_day28continue: queuing the day-28 CONTINUE cutscene (Junimo → next season).", LogLevel.Info);
            _pendingCutscene = Day28Branch.Continue;
        }

        /// <summary>Debug: queue the WIN screen so a playtest can watch the win → shrine →
        /// keep-playing flow without grinding to a real Winter-28 win. Sets the pending branch;
        /// the Day28CutsceneDriver opens VictoryMenu this tick and OnCutsceneEnded opens the JP
        /// shrine then the keep-playing choice. Bypasses the VictoryAcknowledged "first win only"
        /// gate (that lives in OnDayEnding), so it is re-runnable from any loaded save.</summary>
        public void DebugForceWin()
        {
            _monitor.Log("tly_win: queuing the WIN screen (Junimos → shrine → keep-playing choice).", LogLevel.Info);
            _pendingCutscene = Day28Branch.Win;
        }

        /// <summary>Debug: jump the in-game date to <paramref name="day"/> of the current season so a
        /// playtest can sleep straight into the day-28 gate (and exercise the REAL sleep → morning
        /// cutscene timing) without grinding a whole month. Sets both the game date and the run's
        /// day so OnDayEnding's gate evaluates for the right day, plus the load-menu display field.</summary>
        public void DebugSetDay(int day)
        {
            Run.DayOfMonth = day;
            Game1.dayOfMonth = day;
            // No netWorldState.Date write here: Date is a computed `=> WorldDate.Now()` that builds
            // a fresh WorldDate from the Game1 statics, so assigning to it hits a throwaway object
            // (audit 2026-08-26). The line above is what actually moves the date; netWorldState's
            // own copy is synced by vanilla's UpdateFromGame1 on the next save.
            if (Game1.player != null)
                Game1.player.dayOfMonthForSaveGame = day;
            _monitor.Log(
                $"tly_setday: date set to {Run.Season} {day}. Sleep to trigger the day-{day} gate.",
                LogLevel.Info);
        }

        /// <summary>Simulate a CC donation: fill the first open slot on the board that wants the id,
        /// then record it. The board is written first because the ledger mirrors the board.</summary>
        public void Donate(string itemId)
        {
            var slot = TheLongestYear.Integration.CcSlotWriter.FirstOpenSlotFor(itemId);
            if (slot == null)
            {
                _monitor.Log($"No open slot wants '{itemId}'. Nothing donated.", LogLevel.Warn);
                return;
            }
            if (!TheLongestYear.Integration.CcSlotWriter.TryFill(slot.Value.BundleIndex, slot.Value.IngredientIndex))
            {
                _monitor.Log($"Could not fill bundle {slot.Value.BundleIndex} slot {slot.Value.IngredientIndex} for '{itemId}'.", LogLevel.Warn);
                return;
            }
            Run.RecordDonation(slot.Value.BundleIndex, slot.Value.IngredientIndex, BundleParsing.NormalizeItemId(itemId));
            _monitor.Log($"Donated '{itemId}' into bundle {slot.Value.BundleIndex} slot {slot.Value.IngredientIndex}. Ledger {Run.DonatedSlots.Count} slot(s).", LogLevel.Info);
        }

        public void PrintRunState()
        {
            _monitor.Log(
                $"Run {Run.RunNumber}: {Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}). " +
                $"Selection={Run.CurrentSelection?.ToString() ?? "none"}, " +
                $"selectedThisMonth=[{string.Join(",", Run.SelectedThemesThisMonth)}], " +
                $"slots filled={Run.DonatedSlots.Count}, JP banked={_store.State.JunimoPoints}, " +
                $"yearTwoSeedsWeek={Run.YearTwoSeedsWeek}, sneakPeekSeason={Run.SneakPeekSeason}.",
                LogLevel.Info);
            PrintCommunityCenterCompletion();
        }

        /// <summary>Everything vanilla's Willy back-room letter trigger reads (Nexus bug 1130863):
        /// <c>Mail_Willy_BackRoomUnlocked</c> fires on DayEnding when
        /// <c>Game1.MasterPlayer.hasCompletedCommunityCenter()</c> is true, which this mod patches
        /// to also require every room complete on the live board. Prints both halves so a
        /// player's paste shows which one said no.</summary>
        private void PrintCommunityCenterCompletion()
        {
            Farmer p = Game1.MasterPlayer ?? Game1.player;
            if (p == null) return;
            string[] rooms = { "Pantry", "CraftsRoom", "FishTank", "BoilerRoom", "Vault" };
            var roomBits = new List<string>();
            foreach (string room in rooms)
                roomBits.Add($"{room}={(Integration.RunReachEvaluator.RoomComplete(room) ? "done" : "OPEN")}");
            string[] mails = { "ccPantry", "ccCraftsRoom", "ccFishTank", "ccBoilerRoom", "ccVault", "ccBulletin",
                               "ccIsComplete", "willyBackRoomInvitation", "willyBackRoom", "JojaMember" };
            var mailBits = new List<string>();
            foreach (string m in mails)
                if (p.mailReceived.Contains(m)) mailBits.Add(m);
            string areas = "?";
            if (Game1.getLocationFromName("CommunityCenter") is StardewValley.Locations.CommunityCenter cc)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < cc.areasComplete.Count; i++) sb.Append(cc.areasComplete[i] ? 'T' : 'F');
                areas = sb.ToString();
            }
            bool willyFired = p.triggerActionsRun.Contains("Mail_Willy_BackRoomUnlocked");
            _monitor.Log(
                $"CC completion: hasCompletedCommunityCenter={p.hasCompletedCommunityCenter()}, " +
                $"board rooms [{string.Join(", ", roomBits)}], areasComplete={areas}, " +
                $"mail=[{string.Join(", ", mailBits)}], willyTriggerRan={willyFired}, " +
                $"victoryAcknowledged={_store.State.VictoryAcknowledged}, year={Game1.year}.",
                LogLevel.Info);
        }
    }
}
