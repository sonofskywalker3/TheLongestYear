// JojaMart tiles, read live 2026-09-25 (tly_newgame standard skipintro, debug warp JojaMart 13 28,
// PrintWindow frame with a 64 px grid anchored on the farmer's feet) and checked against the map
// itself (patch export Maps/JojaMart):
//   - The door: the farmer enters on 13,28 (the map's warp out is 13,30 / 14,30; 13,29 is the doorway).
//   - Morris's counter: the painted Morris behind it is map art, Buildings 21,24 (body) and Front
//     21,23 (head). JoinJoja is 21,25, the counter's front. The counter is a closed ring, cols 19..24,
//     rows 23..25: there is no gap to walk out of, so the scene hides the painted Morris and starts
//     the actor on the floor at the counter's west end, 18,24, as if he has just stepped out.
//   - Row 26 (cols 13..18) and col 18 (rows 24..26) are open floor (the counter's shadow only).
//     Route: down 2 to 18,26, then west 5 to 13,26, two tiles in front of the farmer, facing him.
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;
using xTile.Layers;
using xTile.Tiles;

namespace TheLongestYear.Integration
{
    internal static class JojaEventKeys
    {
        public const string OfferId = "sonofskywalker3.TLY.JojaOffer";
        public const string BadEndingId = "sonofskywalker3.TLY.JojaBadEnding";
    }

    /// <summary>Morris's offer (spec 2026-09-25-joja-offer-design): the scene on the first visit to
    /// JojaMart each loop. Morris steps out from his counter, walks up to the farmer at the door and
    /// makes the offer. Skippable from the second showing on the save.</summary>
    internal static class JojaOfferEvent
    {
        public const string LocationName = "JojaMart";

        internal const int DoorX = 13, DoorY = 28;
        internal const int MorrisStartX = 18, MorrisStartY = 24;
        // Relative advancedMove legs: down 2 to 18,26, then west 5 to 13,26.
        internal const string RouteLegs = "0 2 -5 0";
        private const int FaceUp = 0, FaceDown = 2;
        private const int ViewportRowsAboveDoor = 3;
        private const int WalkTimeoutMs = 6000;

        // The painted Morris behind the counter (map art, not an NPC).
        internal const int PaintedX = 21, PaintedBodyY = 24, PaintedHeadY = 23;
        private const string BodyLayer = "Buildings", HeadLayer = "Front";

        /// <summary>Same sanitising as the ending: a script is '/'-joined and a line is quoted.</summary>
        private static string Sanitise(string value)
            => string.IsNullOrEmpty(value) ? value : value.Replace('"', '\'').Replace('/', ',');

        internal static string Build(bool skippable)
        {
            // Back-to-back lines from one speaker share one box as pages (Jeff, 2026-09-25).
            // Literal keys: I18nGuardTests scans for them.
            string offer = string.Join("#$b#",
                Sanitise(Strings.Get("event.joja-offer.morris-1")),
                Sanitise(Strings.Get("event.joja-offer.morris-2")),
                Sanitise(Strings.Get("event.joja-offer.morris-3")));
            var s = new List<string> { "none", "-1000 -1000", $"farmer {DoorX} {DoorY} {FaceUp}" };
            if (skippable) s.Add("skippable");
            s.AddRange(new[]
            {
                $"addTemporaryActor Morris 16 32 {MorrisStartX} {MorrisStartY} {FaceDown} true Character",
                $"viewport {DoorX} {DoorY - ViewportRowsAboveDoor} clamp",
                "pause 400",
                // Morris comes out from behind the counter to meet the farmer.
                $"advancedMove Morris false {RouteLegs}",
                $"{OpeningEventCommands.WaitWalkName} Morris {WalkTimeoutMs}",
                $"faceDirection Morris {FaceDown}",
                $"speak Morris \"{offer}\"",
                "pause 300",
                "end",
            });
            return string.Join("/", s);
        }

        /// <summary>Hides the painted Morris for the scene and puts him back afterwards. Map edits
        /// are never saved, so a crash mid-scene costs nothing.</summary>
        internal sealed class PaintedMorris
        {
            private Tile _body, _head;
            private GameLocation _in;

            public bool Hidden => _in != null;

            public void Hide(GameLocation loc)
            {
                if (Hidden) return;
                Layer body = loc.map?.GetLayer(BodyLayer), head = loc.map?.GetLayer(HeadLayer);
                if (body == null || head == null) return;
                _body = body.Tiles[PaintedX, PaintedBodyY];
                _head = head.Tiles[PaintedX, PaintedHeadY];
                body.Tiles[PaintedX, PaintedBodyY] = null;
                head.Tiles[PaintedX, PaintedHeadY] = null;
                _in = loc;
            }

            public void Restore()
            {
                if (!Hidden) return;
                Layer body = _in.map?.GetLayer(BodyLayer), head = _in.map?.GetLayer(HeadLayer);
                if (body != null) body.Tiles[PaintedX, PaintedBodyY] = _body;
                if (head != null) head.Tiles[PaintedX, PaintedHeadY] = _head;
                _in = null;
                _body = _head = null;
            }
        }
    }

    /// <summary>Starts Morris's offer on the first visit to JojaMart each loop: on the warp in, and
    /// then every half second while the farmer stands there, so a scene blocked by a menu or a fade
    /// starts once the way is clear.</summary>
    internal sealed class JojaOfferDriver
    {
        private const int PollTicks = 30;

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly JojaOfferEvent.PaintedMorris _painted = new();

        public JojaOfferDriver(IMonitor monitor, MetaStore meta) { _monitor = monitor; _meta = meta; }

        public void Attach(IModHelper helper)
        {
            helper.Events.Player.Warped += OnWarped;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => _painted.Restore();
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer && e.NewLocation?.Name == JojaOfferEvent.LocationName) TryStart();
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (_painted.Hidden && !Game1.eventUp && Game1.currentLocation?.currentEvent == null)
                _painted.Restore();   // the scene ended, was skipped, or was lost
            if (!e.IsMultipleOf(PollTicks)) return;
            if (Game1.currentLocation?.Name == JojaOfferEvent.LocationName) TryStart();
        }

        private static bool Busy()
            => Game1.eventUp || Game1.activeClickableMenu != null || Game1.isFestival() || !Context.CanPlayerMove;

        private void TryStart()
        {
            if (!RunActivation.IsActive || _meta == null) return;
            if (!JojaOffer.ShouldPlayScene(_meta.Run, _meta.State, busy: Busy())) return;
            Start();
        }

        private void Start()
        {
            RunState run = _meta.Run;
            MetaState meta = _meta.State;
            GameLocation loc = Game1.currentLocation;
            bool skip = JojaOffer.SceneSkippable(meta);
            loc.startEvent(new Event(JojaOfferEvent.Build(skip), null, JojaEventKeys.OfferId));
            if (loc.currentEvent?.id != JojaEventKeys.OfferId)
            {
                _monitor.Log("Joja: the offer scene did not start (an event was ending); trying again shortly.", LogLevel.Trace);
                return;
            }
            _painted.Hide(loc);
            JojaOffer.MarkSceneSeen(run, meta, Calendar.DayOfYear((int)run.Season, run.DayOfMonth));
            _monitor.Log($"Joja: offer scene (skippable={skip}).", LogLevel.Info);
        }

        /// <summary>Debug (tly_joja scene): forget today's scene and play it now if in JojaMart.</summary>
        public void DebugReplay()
        {
            _meta.Run.JojaSceneSeenDay = -1;
            if (Game1.currentLocation?.Name != JojaOfferEvent.LocationName)
            {
                _monitor.Log("tly_joja scene: JojaSceneSeenDay reset; walk into JojaMart to see it.", LogLevel.Info);
                return;
            }
            if (Busy())
            {
                _monitor.Log("tly_joja scene: JojaSceneSeenDay reset; the game is busy, it starts once the way is clear.", LogLevel.Info);
                return;
            }
            Start();
        }
    }
}
