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
            bool rewound = _meta.State.CompletedResets > 0;
            _monitor.Log($"Season turn: starting {kind} (junimos={SeasonTurn.JunimoCount(kind)}, skippable={skippable}, rewound={rewound}, door={door.X},{door.Y}, in {loc.Name}).", LogLevel.Info);
            loc.startEvent(new Event(SeasonTurnEventInjector.Build(kind, door.X, door.Y, skippable, rewound), null, SeasonTurnEventKeys.EventId));
            _meta.State.SeasonTurnsSeen.Add(SeasonTurn.SeenName(kind));
            _onComplete = onComplete;
            _running = true;
            _startedTick = Game1.ticks;
            return true;
        }

        /// <summary>Darkness pushback: the Junimos' "tainted" scene after the board changed, started
        /// NOW where the farmer stands on the Farm (Jeff, 2026-10-07: never on waking, never a warp to
        /// the doorstep; <see cref="TheLongestYear.Core.Sabotage.TamperPorchRule"/> says when). False
        /// when it cannot start here (not on the Farm, an event or another scene already up), and then
        /// nothing has changed and the caller keeps the report for the next Farm entry. The scene is
        /// skippable from its second showing on the save.</summary>
        public bool StartTamperHere(string oldItemName, string newItemName, bool newIsPlural, Action onComplete)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc is not Farm || _running || Game1.eventUp || loc.currentEvent != null) return false;
            Microsoft.Xna.Framework.Point at = Game1.player.TilePoint;
            int facing = Game1.player.FacingDirection;
            bool skippable = _meta.State.SeasonTurnsSeen.Contains(TamperSeenName);
            _monitor.Log($"Darkness: starting the board-changed scene where the farmer stands on the Farm, ({at.X},{at.Y}) facing {facing} ({oldItemName} -> {newItemName}, skippable={skippable}).", LogLevel.Info);
            loc.startEvent(new Event(SeasonTurnEventInjector.BuildTamper(at.X, at.Y, facing, oldItemName, newItemName, newIsPlural, skippable), null, SeasonTurnEventKeys.EventId));
            _meta.State.SeasonTurnsSeen.Add(TamperSeenName);
            _onComplete = onComplete;
            _running = true;
            _startedTick = Game1.ticks;
            return true;
        }

        public const string TamperSeenName = "DarknessTamper";

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
