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
        /// can play, so every strike lands at once exactly as it did before the split.</summary>
        public Func<PendingStrike, bool> SceneCanPlay { get; set; } = _ => false;

        /// <summary>Land a waiting strike now. Only for a strike with no scene by design: its kind
        /// already played this loop, or its target is somewhere the scene cannot show.</summary>
        private bool ApplyPendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            Pending = null;
            if (p.Applied) return false;
            _monitor.Log($"Darkness: applying tonight's {p.Event} with no scene by design ({why}) at tick {Game1.ticks}.", LogLevel.Trace);
            p.Apply();
            return true;
        }

        /// <summary>Drop a waiting strike whose scene cannot have tonight's overnight slot (Jeff,
        /// 2026-10-07: "we don't delay scenes without delaying the effect of them"). Neither its
        /// effect nor its scene happens tonight, and since nothing was recorded the night is as if
        /// no strike happened: the roll and the every-loop guarantee bring it back later.</summary>
        public bool PostponePendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            if (p.Committed)
                return SettlePendingIfAny(why);
            Pending = null;
            if (_guaranteedTamperTonight && p.Event == DarknessEvent.Tampering)
                Run.GuaranteedTamperPostponed = true;
            _monitor.Log($"Darkness: tonight's {p.Event} is postponed ({why}): no effect and no scene tonight; the night counts as no strike, so the roll and the guarantee bring it back.", LogLevel.Info);
            return true;
        }

        /// <summary>The net under the save, the morning and the next night pass: a strike still
        /// waiting lands only if its scene had the slot (<see cref="StrikeSlot.LandsAtNet"/>), and is
        /// postponed otherwise (a collision nobody caught, or the first night of a save, when
        /// vanilla runs no <c>pickFarmEvent</c>). Never applies a strike whose scene never had the
        /// slot.</summary>
        public bool SettlePendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            if (!StrikeSlot.LandsAtNet(p.Committed)) return PostponePendingIfAny(why);
            Pending = null;
            if (p.Applied) return false;
            _monitor.Log($"Darkness: tonight's {p.Event} lands now ({why}): its scene had the overnight slot but did not reach its beat, at tick {Game1.ticks}.", LogLevel.Trace);
            p.Apply();
            return true;
        }

        /// <summary>Tonight's scene has taken the overnight slot: the strike is committed.</summary>
        public void CommitPendingScene()
        {
            PendingStrike p = Pending;
            if (p == null || p.Committed) return;
            p.Commit();
            _monitor.Log($"Darkness: tonight's {p.Event} is committed: its scene has the overnight slot.", LogLevel.Trace);
        }

        /// <summary>A strike is committed to tonight: its scene took the slot, or it is landing with no
        /// scene by design. Only now does the run record it (the week's chance, the front's cap or
        /// spacing, the every-loop guarantee), so a postponed strike spends nothing. The guaranteed
        /// Winter tamper counts as done here too.</summary>
        private void OnStrikeCommitted(PendingStrike strike, int week, CoreSeason season, int dayOfYear)
        {
            StrikeLedger.Record(Run, strike.Event, week, season, dayOfYear);
            if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
            {
                Run.GuaranteedTamperDone = true;
                Run.GuaranteedTamperPostponed = false;
                _monitor.Log($"Darkness: the guaranteed Winter tamper struck on Winter {Run.DayOfMonth}.", LogLevel.Info);
            }
        }

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
            if (strike.Landed)
            {
                if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
                    Meta.FirstWinterTamperSeen = true;
                return;
            }
            (Run.StruckEvents ??= new()).Remove(strike.Event.ToString());
            _monitor.Log($"Darkness: tonight's {strike.Event} found nothing to do when it came to it, so the kind is still owed this loop.", LogLevel.Info);
            if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
            {
                Run.GuaranteedTamperDone = false;
                _monitor.Log("Darkness: the guaranteed Winter tamper did not land, so it retries tomorrow.", LogLevel.Info);
            }
        }
    }
}
