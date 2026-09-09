using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Starts a season-turn scene and reports when it ends. The Day28CutsceneDriver calls
    /// Start on a Continue morning instead of opening its menu; the completion callback is the same
    /// RunController.OnCutsceneEnded. The event adds SeenMail on its last line; a skipped or lost
    /// event never does, so "event gone" also counts as finished (the morning is never stranded).</summary>
    internal sealed class SeasonTurnDriver
    {
        private const int SettleTicks = 30;

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private Action _onComplete;
        private bool _running;
        private int _startedTick;
        private Func<bool> _pendingStart;
        private int _pendingSince;
        private const int PendingTimeoutTicks = 60 * 20;

        public bool Running => _running;

        public SeasonTurnDriver(IMonitor monitor, MetaStore meta) { _monitor = monitor; _meta = meta; }

        public void Attach(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => { _running = false; _onComplete = null; };
        }

        public bool Start(SeasonTurnKind kind, Action onComplete)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.eventUp || loc.currentEvent != null) return false;
            Microsoft.Xna.Framework.Point door = Game1.getFarm().GetMainFarmHouseEntry();
            bool skippable = SeasonTurn.IsSkippable(kind, _meta.State.SeasonTurnsSeen);
            _monitor.Log($"Season turn: starting {kind} (junimos={SeasonTurn.JunimoCount(kind)}, skippable={skippable}, door={door.X},{door.Y}, in {loc.Name}).", LogLevel.Info);
            loc.startEvent(new Event(SeasonTurnEventInjector.Build(kind, door.X, door.Y, skippable), null, SeasonTurnEventKeys.EventId));
            _meta.State.SeasonTurnsSeen.Add(SeasonTurn.SeenName(kind));
            _onComplete = onComplete;
            _running = true;
            _startedTick = Game1.ticks;
            return true;
        }

        /// <summary>Darkness pushback: the porch scene the morning after the board changed. Starts
        /// the moment the wake frame settles (no new-day fade, no farm event, no warp, no menu), and
        /// gives up after a while so a morning is never stranded: then the continuation just runs.
        /// The scene is skippable from its second showing on the save.</summary>
        public void StartTamperWhenSettled(string oldItemName, string newItemName, Action onComplete)
        {
            _pendingSince = Game1.ticks;
            _pendingStart = () =>
            {
                GameLocation loc = Game1.currentLocation;
                if (loc == null || Game1.eventUp || loc.currentEvent != null) return false;
                Microsoft.Xna.Framework.Point door = Game1.getFarm().GetMainFarmHouseEntry();
                bool skippable = _meta.State.SeasonTurnsSeen.Contains(TamperSeenName);
                _monitor.Log($"Darkness: starting the board-changed scene ({oldItemName} -> {newItemName}, skippable={skippable}).", LogLevel.Info);
                loc.startEvent(new Event(SeasonTurnEventInjector.BuildTamper(door.X, door.Y, oldItemName, newItemName, skippable), null, SeasonTurnEventKeys.EventId));
                _meta.State.SeasonTurnsSeen.Add(TamperSeenName);
                _onComplete = onComplete;
                _running = true;
                _startedTick = Game1.ticks;
                return true;
            };
            _pendingOnComplete = onComplete;
        }

        public const string TamperSeenName = "DarknessTamper";
        private Action _pendingOnComplete;

        /// <summary>Debug replay (tly_seasonturn): the scene alone, no continuation.</summary>
        public void StartNow(SeasonTurnKind kind)
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.farmEvent != null || Game1.locationRequest != null)
            {
                _monitor.Log("tly_seasonturn: the game is busy (event, menu or warp up); try again with nothing open.", LogLevel.Warn);
                return;
            }
            if (!Start(kind, () => _monitor.Log("Season turn: replay finished (no continuation).", LogLevel.Info)))
                _monitor.Log("tly_seasonturn: could not start (no location or an event is up).", LogLevel.Warn);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_pendingStart != null && Context.IsWorldReady)
            {
                bool settled = !Game1.newDay && !Game1.eventUp && Game1.farmEvent == null
                    && Game1.locationRequest == null && Game1.activeClickableMenu == null
                    && Game1.currentLocation != null && Game1.player.CanMove;
                bool timedOut = Game1.ticks - _pendingSince > PendingTimeoutTicks;
                if (settled || timedOut)
                {
                    Func<bool> start = _pendingStart;
                    Action pendingCb = _pendingOnComplete;
                    _pendingStart = null;
                    _pendingOnComplete = null;
                    if (timedOut || !start())
                    {
                        _monitor.Log("Darkness: the board-changed scene could not start; continuing the morning.", LogLevel.Warn);
                        pendingCb?.Invoke();
                    }
                }
                return;
            }
            if (!_running || !Context.IsWorldReady) return;
            if (Game1.ticks - _startedTick < SettleTicks) return;
            bool eventGone = !Game1.eventUp && Game1.currentLocation?.currentEvent == null;
            if (!eventGone) return;
            Farmer p = Game1.player;
            bool seen = p.mailReceived.Contains(SeasonTurnEventKeys.SeenMail);
            p.mailReceived.Remove(SeasonTurnEventKeys.SeenMail);   // transient signal, never persisted
            _monitor.Log(seen ? "Season turn: scene finished." : "Season turn: scene ended early (skipped or lost); continuing.",
                seen ? LogLevel.Info : LogLevel.Warn);
            _running = false;
            Action cb = _onComplete;
            _onComplete = null;
            cb?.Invoke();
        }
    }
}
