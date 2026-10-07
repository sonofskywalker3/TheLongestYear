using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The pending strike: when it lands, is committed or is postponed.</summary>
    internal sealed partial class SabotageService
    {
        /// <summary>Tonight's strike, picked but not yet applied, waiting for its scene. Null when
        /// none is waiting.</summary>
        public PendingStrike Pending { get; private set; }

        /// <summary>Set by ModEntry once the overnight scenes exist (Task 6). Until then no scene
        /// can play, so a strike whose scene is due waits (it never lands without it).</summary>
        public Func<PendingStrike, bool> SceneCanPlay { get; set; } = _ => false;

        /// <summary>Can this kind's scene stage tonight at all (review I1)? Set by ModEntry. Asked
        /// only while the kind's scene is due; until it is set every kind may act and the pick-time
        /// <see cref="SceneCanPlay"/> still guards the strike.</summary>
        public Func<DarknessEvent, bool> SceneCanStage { get; set; } = _ => true;

        /// <summary><see cref="SceneCanStage"/>, with a throwing check counted as "cannot".</summary>
        private bool SceneStagesTonight(DarknessEvent e)
        {
            try
            {
                return SceneCanStage(e);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Darkness: the staging check for {e}'s scene threw, so {e} cannot act tonight. {ex}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>Land a waiting strike now. Only for a strike with no scene by design: its kind's
        /// scene already played this loop.</summary>
        private bool ApplyPendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            Pending = null;
            if (p.Applied) return false;
            _monitor.Log($"Darkness: applying tonight's {p.Event} with no scene by design ({why}) at tick {Game1.ticks}.", LogLevel.Trace);
            p.LandNow();
            return true;
        }

        /// <summary>Drop a waiting strike whose scene cannot have tonight's overnight slot (Jeff,
        /// 2026-10-07: "we don't delay scenes without delaying the effect of them"). Neither its
        /// effect nor its scene happens tonight, and since nothing was recorded the night is as if
        /// no strike happened. Only a slot collision (<see cref="PostponeCause.SlotTaken"/>) queues it
        /// for the next free night (<see cref="StrikeQueue"/>), or carries the guaranteed Winter
        /// tamper; a staging failure would fail the same way again, so it is dropped (review I1).</summary>
        public bool PostponePendingIfAny(string why, PostponeCause cause)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            if (!p.Postpone())
            {
                // Its scene staged, so tonight is spent on it: the net lands it. Already applied or
                // postponed: just let it go.
                if (p.Committed) return SettlePendingIfAny(why);
                Pending = null;
                return false;
            }
            Pending = null;
            // A collision queues it for the next free night (designer, 2026-10-07); the guaranteed
            // Winter tamper keeps its own carry instead. A staging failure queues nothing.
            bool queue = StrikeQueue.Queues(cause);
            if (queue && _guaranteedTamperTonight && p.Event == DarknessEvent.Tampering)
                GuaranteedTamper.OnPostponed(Run);
            else if (queue)
                StrikeQueue.Enqueue(Run, p.Event);
            _monitor.Log($"Darkness: tonight's {p.Event} is postponed ({why}): no effect and no scene tonight; the night counts as no strike, and {(queue ? "it is queued for the next free night" : "it is not queued (its scene could not stage)")}.", LogLevel.Info);
            return true;
        }

        /// <summary>The net under the save, the morning and the next night pass: a strike still
        /// waiting lands only if its scene actually staged (<see cref="StrikeLifecycle.AtNet"/>), and is
        /// postponed otherwise: a collision nobody caught, a scene that took the slot but could not
        /// stage or threw in setUp, another mod replacing the event after our postfix, or the first
        /// night of a save, when vanilla runs no <c>pickFarmEvent</c>. Never applies a strike whose
        /// scene never staged.</summary>
        public bool SettlePendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            switch (p.AtNet())
            {
                case StrikeNetAction.Postpone:
                    return PostponePendingIfAny(why, PostponeCause.NeverStaged);
                case StrikeNetAction.Land:
                    Pending = null;
                    _monitor.Log($"Darkness: tonight's {p.Event} lands now ({why}): its scene staged but did not reach its beat, at tick {Game1.ticks}.", LogLevel.Trace);
                    p.Apply();
                    return true;
                default:
                    Pending = null;
                    return false;
            }
        }

        /// <summary>A strike is committed to tonight: its scene has staged, or it is landing with no
        /// scene by design. Only now does the run record it (the week's chance, the front's cap or
        /// spacing, the every-loop guarantee), so a postponed strike spends nothing. The guaranteed
        /// Winter tamper counts as done here too.</summary>
        private void OnStrikeCommitted(PendingStrike strike, int week, CoreSeason season, int dayOfYear)
        {
            StrikeLedger.Record(Run, strike.Event, week, season, dayOfYear);
            _monitor.Log($"Darkness: tonight's {strike.Event} is committed and recorded.", LogLevel.Trace);
            if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
            {
                GuaranteedTamper.OnCommitted(Run);
                // Review M3: a tamper queued from an earlier collision is paid by this one too.
                StrikeQueue.OnCommitted(Run, DarknessEvent.Tampering);
                _monitor.Log($"Darkness: the guaranteed Winter tamper struck on Winter {Run.DayOfMonth}.", LogLevel.Info);
            }
            else if (StrikeQueue.OnCommitted(Run, strike.Event))
                _monitor.Log($"Darkness: the postponed {strike.Event} has struck{(_queuedTonight ? "" : " (picked tonight by an arm or the roll)")}, so it leaves the queue.", LogLevel.Info);
        }

        /// <summary>Did tonight's strike come from the queue of postponed strikes? Cleared at the top
        /// of every night pass, so it only ever describes tonight.</summary>
        private bool _queuedTonight;

        /// <summary>Was the guaranteed Winter tamper the strike picked tonight? Cleared at the top of
        /// every night pass, so it only ever describes tonight.</summary>
        private bool _guaranteedTamperTonight;

        /// <summary>Told once a strike has run, whatever came of it, from every apply path: the
        /// immediate one, the nets, and a scene calling Apply itself. A strike that found nothing to
        /// do when it came to it does not count toward the every-loop guarantee, so its name comes
        /// back off the run's list and the kind is still owed. The week's chance drop and the cap
        /// slot stay spent: the night is over either way. (A postponed strike never gets here: it
        /// was never committed, so nothing was recorded.)
        ///
        /// The guaranteed Winter tamper marks the save's first Winter as reached only here, once it
        /// has landed. Marking it at the pick would send a failed apply's retry to the seeded
        /// random week-1 night instead of tomorrow.</summary>
        private void OnStrikeApplied(PendingStrike strike)
        {
            bool guaranteed = _guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering;
            if (guaranteed) GuaranteedTamper.OnApplied(Run, strike.Landed);
            if (strike.Landed)
            {
                if (guaranteed) Meta.FirstWinterTamperSeen = true;
                return;
            }
            (Run.StruckEvents ??= new()).Remove(strike.Event.ToString());
            _monitor.Log($"Darkness: tonight's {strike.Event} found nothing to do when it came to it, so the kind is still owed this loop.", LogLevel.Info);
            if (guaranteed)
                _monitor.Log("Darkness: the guaranteed Winter tamper did not land, so it retries tomorrow.", LogLevel.Info);
        }
    }
}
