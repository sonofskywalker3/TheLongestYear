using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace TheLongestYear.Integration
{
    /// <summary>The six hall Junimo colours, in the order the ending seats them (vanilla's own room
    /// colours: Junimo.cs whichArea 0..5, plus the purple of the sixth). Index 0 is the lead speaker;
    /// JunimoPortrait tints "Portraits/Junimo&lt;i&gt;" with the same entry so the face in the dialogue
    /// box matches the sprite on the floor.</summary>
    internal static class JunimoPalette
    {
        public static readonly Color[] Colours =
        {
            Color.LimeGreen,
            Color.Orange,
            Color.Turquoise,
            Color.Gold,
            new Color(160, 20, 220),
            Color.Salmon,
        };

        public static Color Get(int index)
            => index >= 0 && index < Colours.Length ? Colours[index] : Colours[0];
    }

    /// <summary>Custom event commands the Year One Ending needs beyond vanilla's set (registered through
    /// Event.RegisterCommand, like GrandpaCandleCommand). Each one never throws: a failure is logged and
    /// the command is skipped so the ending keeps playing.
    ///
    /// <c>tlyChangeLocation &lt;location&gt; &lt;x&gt; &lt;y&gt;</c>: vanilla's changeLocation warps the
    /// farmer to the SAME tile coordinates they had in the old map and only then lets the script warp
    /// them again, so the first frame in the new map is centred on the wrong spot (live 2026-09-07: one
    /// frame of Town up and to the right of the hall), and its fade starts by dropping the old map's
    /// objects and actors. This one fades to black first with the world intact, then warps under
    /// black to the target tile through the same private Event.changeLocation the vanilla command
    /// uses, and lets the game fade back in.
    ///
    /// <c>tlyRefurbishHall</c>: swaps the Town map's Community Center exterior to the restored art
    /// (Town.refurbishCommunityCenter). Vanilla only does that from resetLocalState when the vanilla
    /// completion mail is present; the ending plays on the morning the hall is whole, so the building
    /// behind Lewis must already look it.
    ///
    /// <c>tlyJunimo &lt;name&gt; &lt;x&gt; &lt;y&gt; &lt;colourIndex&gt;</c>: a real Junimo (StardewValley.Characters.Junimo)
    /// as an event actor, so it is drawn at Junimo scale, tinted, and animates its idle bob with the
    /// occasional hop. addTemporaryActor with the Junimo sheet gave a grey, still, oversized NPC (live
    /// 2026-09-07). The display name is always "Junimo"; the internal name is what jump/speak address.</summary>
    internal static class EndingEventCommands
    {
        public const string ChangeLocationName = "tlyChangeLocation";
        public const string RefurbishHallName = "tlyRefurbishHall";
        public const string JunimoName = "tlyJunimo";
        public const string PanToName = "tlyPanTo";
        public const string FadeTreesName = "tlyFadeTrees";
        public const string FadeInName = "tlyFadeIn";
        public const string FadeOutName = "tlyFadeOut";
        public const string SayName = "tlySay";
        // How long the overlay stays black after the event ends before it lifts, so the hand-off
        // to the continuation (viewport back to the player, the shrine menu) happens unseen.
        private const float HoldAfterEndMs = 1500f, LiftAfterEndMs = 700f;
        private const string JunimoDisplayName = "Junimo";

        private static readonly MethodInfo EventChangeLocation = typeof(Event).GetMethod(
            "changeLocation", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(string), typeof(int), typeof(int), typeof(Action) }, null);
        private static readonly MethodInfo TownRefurbish = typeof(Town).GetMethod(
            "refurbishCommunityCenter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo JunimoColour = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PendingMoves = typeof(Event).GetField(
            "actorPositionsAfterMove", BindingFlags.Instance | BindingFlags.NonPublic);

        // tlyChangeLocation state. WarpFadeDone is past the 1.1 the warp fade completes at, so the
        // game performs the pending warp on its very next fade update. After the load the mod's own
        // black overlay (_black, drawn in Display.Rendered) stays up so the script can place every
        // actor unseen; tlyFadeIn lifts it. The game's own fade-in is cancelled at the load.
        private static bool _changing;
        private static float _black;
        private static bool _fadingIn, _fadingOut, _saying;
        private static float _fadeElapsed, _fadeDuration;
        private static float _afterEnd = -1f;   // ms since the event ended while black, -1 = not tracking
        private const float WarpFadeDone = 1.15f;
        private const float FadeSpeed = 0.02f;

        // tlyFadeTrees state: trees inside this tile rectangle are held translucent every tick
        // while the ending event is up (Tree.alpha climbs back to 1 on its own each frame).
        private static Rectangle? _fadeTrees;
        private const float TreeAlpha = 0.3f;

        // tlyPanTo state: the command is called every tick until it advances the script.
        private static bool _panning;
        private static Vector2 _panFrom, _panTo;
        private static float _panElapsed, _panDuration;

        /// <summary>Camera centre (pixels) that shows tile (x, y) as near the middle as the map allows.</summary>
        private static Vector2 ClampedCentre(int x, int y)
        {
            var loc = Game1.currentLocation;
            float w = Game1.viewport.Width, h = Game1.viewport.Height;
            float mapW = loc.Map.DisplayWidth, mapH = loc.Map.DisplayHeight;
            float cx = x * 64f + 32f, cy = y * 64f + 32f;
            cx = mapW >= w ? MathHelper.Clamp(cx, w / 2f, mapW - w / 2f) : mapW / 2f;
            cy = mapH >= h ? MathHelper.Clamp(cy, h / 2f, mapH - h / 2f) : mapH / 2f;
            return new Vector2(cx, cy);
        }

        private static void SetCentre(Vector2 c)
        {
            Game1.viewport.X = (int)System.Math.Round(c.X - Game1.viewport.Width / 2f);
            Game1.viewport.Y = (int)System.Math.Round(c.Y - Game1.viewport.Height / 2f);
        }

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += (_, _) => HoldTreesTranslucent();
            helper.Events.Display.Rendered += (_, e) => DrawBlack(e.SpriteBatch);

            // tlyFadeOut [ms]: take the overlay to black, eased, and leave it there. Used before
            // "end": the vanilla globalFade cleared itself when the event ended, which showed the
            // shrine for a blink before the continuation moved the camera (2026-09-07). After the
            // event ends the overlay holds a moment, then lifts on its own (see DrawBlack).
            Event.RegisterCommand(FadeOutName, (evt, args, context) =>
            {
                if (!_fadingOut)
                {
                    ArgUtility.TryGetOptionalInt(args, 1, out int ms, out _, 1500);
                    _fadeDuration = System.Math.Max(1, ms);
                    _fadeElapsed = 0f;
                    _fadingOut = true;
                    return;
                }
                _fadeElapsed += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                float t = MathHelper.Clamp(_fadeElapsed / _fadeDuration, 0f, 1f);
                _black = t * t * (3f - 2f * t);
                if (t < 1f) return;
                _black = 1f;
                _fadingOut = false;
                _afterEnd = 0f;
                evt.CurrentCommand++;
            });

            // tlySay <Name> "<text>": the line in the plain (short) dialogue box, the speaker's name
            // leading it, pages split on #$b#. The portrait box covers half the scene at 1080p
            // (Jeff, 2026-09-07). The game advances the event when the box closes, like "message".
            Event.RegisterCommand(SayName, (evt, args, context) =>
            {
                // Called every tick: open the box once, then wait for it to close, then advance.
                if (_saying)
                {
                    if (Game1.activeClickableMenu is EndingSpeechBox) return;
                    _saying = false;
                    evt.CurrentCommand++;
                    return;
                }
                if (Game1.activeClickableMenu != null || Game1.dialogueUp) return;
                if (!ArgUtility.TryGet(args, 1, out string name, out string error) || !ArgUtility.TryGet(args, 2, out string text, out error))
                {
                    monitor.Log($"{SayName}: {error}; skipping.", LogLevel.Warn);
                    evt.CurrentCommand++;
                    return;
                }
                var pages = new List<string>();
                foreach (string raw in text.Split(new[] { "#$b#" }, StringSplitOptions.None))
                {
                    string page = System.Text.RegularExpressions.Regex.Replace(raw, @"\$[a-z0-9]+", "").Replace("@", Game1.player.Name).Trim();
                    if (page.Length > 0) pages.Add(page);
                }
                if (pages.Count == 0) { evt.CurrentCommand++; return; }
                NPC actor = evt.getActorByName(name, out _) ?? Game1.getCharacterFromName(name);
                Microsoft.Xna.Framework.Graphics.Texture2D portrait = null;
                try { portrait = actor?.Portrait; }
                catch (Exception) { portrait = null; }
                if (portrait == null)
                {
                    // A real Junimo actor never loads a portrait itself; the mod serves Portraits/Junimo<i>.
                    try { portrait = Game1.content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("Portraits/" + name); }
                    catch (Exception ex) { monitor.Log($"{SayName}: no portrait for {name} ({ex.GetType().Name}); the box shows the name only.", LogLevel.Trace); }
                }
                string display = actor?.displayName ?? name;
                Game1.activeClickableMenu = new EndingSpeechBox(portrait, display, pages);
                _saying = true;
            });

            // tlyFadeIn [ms]: lift the black overlay tlyChangeLocation left up, eased, then continue.
            Event.RegisterCommand(FadeInName, (evt, args, context) =>
            {
                if (!_fadingIn)
                {
                    ArgUtility.TryGetOptionalInt(args, 1, out int ms, out _, 1200);
                    _fadeDuration = System.Math.Max(1, ms);
                    _fadeElapsed = 0f;
                    _fadingIn = true;
                    return;
                }
                _fadeElapsed += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                float t = MathHelper.Clamp(_fadeElapsed / _fadeDuration, 0f, 1f);
                _black = 1f - t * t * (3f - 2f * t);
                if (t < 1f) return;
                _black = 0f;
                _fadingIn = false;
                evt.CurrentCommand++;
            });

            // tlyFadeTrees <x> <y>: from here to the end of the event, every tree whose tile is
            // within 4 tiles of (x, y) draws translucent, so the shrine is never hidden behind a
            // canopy (the Standard farm's big tree sat square in front of it, 2026-09-07).
            Event.RegisterCommand(FadeTreesName, (evt, args, context) =>
            {
                if (ArgUtility.TryGetInt(args, 1, out int x, out string error) && ArgUtility.TryGetInt(args, 2, out int y, out error))
                    _fadeTrees = new Rectangle(x - 4, y - 4, 9, 9);
                else
                    monitor.Log($"{FadeTreesName}: {error}; skipping.", LogLevel.Warn);
                evt.CurrentCommand++;
            });

            Event.RegisterCommand(PanToName, (evt, args, context) =>
            {
                try
                {
                    if (!_panning)
                    {
                        if (!ArgUtility.TryGetInt(args, 1, out int x, out string error)
                            || !ArgUtility.TryGetInt(args, 2, out int y, out error)
                            || !ArgUtility.TryGetOptionalInt(args, 3, out int ms, out error, 4000))
                        {
                            monitor.Log($"{PanToName}: {error}; skipping.", LogLevel.Warn);
                            evt.CurrentCommand++;
                            return;
                        }
                        Game1.viewportFreeze = true;
                        _panFrom = new Vector2(Game1.viewport.X + Game1.viewport.Width / 2f, Game1.viewport.Y + Game1.viewport.Height / 2f);
                        _panTo = ClampedCentre(x, y);
                        _panDuration = System.Math.Max(1, ms);
                        _panElapsed = 0f;
                        _panning = true;
                        return;
                    }
                    _panElapsed += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                    float t = MathHelper.Clamp(_panElapsed / _panDuration, 0f, 1f);
                    float eased = t * t * (3f - 2f * t);   // smoothstep: slow out, slow in
                    SetCentre(Vector2.Lerp(_panFrom, _panTo, eased));
                    if (t < 1f) return;
                }
                catch (Exception ex)
                {
                    monitor.Log($"{PanToName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
                }
                _panning = false;
                evt.CurrentCommand++;
            });

            Event.RegisterCommand(ChangeLocationName, (evt, args, context) =>
            {
                if (_changing) return;   // waiting on the fade or the load; called every tick meanwhile
                if (!ArgUtility.TryGet(args, 1, out string location, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int y, out error)
                    || EventChangeLocation == null)
                {
                    monitor.Log($"{ChangeLocationName}: {error ?? "Event.changeLocation not found"}; skipping.", LogLevel.Warn);
                    evt.CurrentCommand++;
                    return;
                }
                _changing = true;
                // Fade the whole screen to black first, with the world intact (the warp's own fade
                // drops the old location's objects and actors the moment it starts: everything but
                // the farmer and the farmhouse popped out, 2026-09-07). At the black frame, inside
                // the same update, hand the warp to the game and mark its fade already complete, so
                // the load happens under black and the only thing the player sees is the fade back
                // in on the new scene. Actors keep moving during the fade, so an exit can overlap it.
                Game1.nonWarpFade = false;
                Game1.globalFadeToBlack(() =>
                {
                    try
                    {
                        Action onComplete = () =>
                        {
                            Game1.currentLocation.ResetForEvent(evt);
                            // Cancel the game's own fade-in; the overlay holds black until tlyFadeIn.
                            Game1.fadeToBlack = false;
                            Game1.fadeToBlackAlpha = 0f;
                            _black = 1f;
                            _changing = false;
                            evt.CurrentCommand++;
                        };
                        // The old scene's actors do not belong in the new one (the Town crowd showed
                        // up inside the hall for a frame, and Lewis was still standing on the farm,
                        // 2026-09-07). Under black nobody sees them go.
                        evt.actors.Clear();
                        // A "move ... true" left running (Morris's exit) stays in the event's
                        // pending-move table, and with its actor gone it can never resolve. The
                        // event then re-issues every later move command each tick until that table
                        // empties: the farmer walked one tile, then another, into the farmhouse and
                        // against its wall (2026-09-07). Clear the table with the actors.
                        if (PendingMoves?.GetValue(evt) is System.Collections.IDictionary pending)
                            pending.Clear();
                        _black = 1f;
                        EventChangeLocation.Invoke(evt, new object[] { location, x, y, onComplete });
                        Game1.fadeToBlackAlpha = WarpFadeDone;
                    }
                    catch (Exception ex)
                    {
                        monitor.Log($"{ChangeLocationName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
                        Game1.globalFadeToClear();
                        _changing = false;
                        evt.CurrentCommand++;
                    }
                }, FadeSpeed);
            });

            Event.RegisterCommand(RefurbishHallName, (evt, args, context) =>
            {
                try
                {
                    if (Game1.currentLocation is Town town && TownRefurbish != null)
                        TownRefurbish.Invoke(town, null);
                    else
                        monitor.Log($"{RefurbishHallName}: not in Town (or Town.refurbishCommunityCenter not found); skipping.", LogLevel.Warn);
                }
                catch (Exception ex)
                {
                    monitor.Log($"{RefurbishHallName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });

            Event.RegisterCommand(JunimoName, (evt, args, context) =>
            {
                if (!ArgUtility.TryGet(args, 1, out string name, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int y, out error)
                    || !ArgUtility.TryGetOptionalInt(args, 4, out int colour, out error, 0))
                {
                    monitor.Log($"{JunimoName}: {error}; skipping.", LogLevel.Warn);
                    evt.CurrentCommand++;
                    return;
                }
                try
                {
                    var junimo = new Junimo(new Vector2(x * 64f, y * 64f), -1, temporary: true);
                    junimo.Name = name;
                    junimo.displayName = JunimoDisplayName;
                    junimo.EventActor = true;
                    junimo.currentLocation = Game1.currentLocation;
                    if (JunimoColour?.GetValue(junimo) is NetColor net)
                        net.Value = JunimoPalette.Get(colour);
                    else
                        monitor.Log($"{JunimoName}: could not set the colour of {name}; it keeps a random one.", LogLevel.Trace);
                    evt.actors.Add(junimo);
                }
                catch (Exception ex)
                {
                    monitor.Log($"{JunimoName}: could not add {name} ({ex.GetType().Name}: {ex.Message}); skipping.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });
        }

        private static void DrawBlack(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            if (_black <= 0f) return;
            Event ev = Game1.CurrentEvent;
            if (ev == null || ev.id != EndingEventKeys.EventId)
            {
                _fadingIn = false;
                _fadingOut = false;
                _saying = false;
                if (_afterEnd < 0f)
                {
                    _black = 0f;   // the event went away without our fade-out: never stay black
                    return;
                }
                // Hold, then lift, then stop tracking.
                _afterEnd += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                float lift = MathHelper.Clamp((_afterEnd - HoldAfterEndMs) / LiftAfterEndMs, 0f, 1f);
                _black = 1f - lift;
                if (lift >= 1f) { _black = 0f; _afterEnd = -1f; return; }
            }
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.graphics.GraphicsDevice.Viewport.Width, Game1.graphics.GraphicsDevice.Viewport.Height), Color.Black * _black);
        }

        private static void HoldTreesTranslucent()
        {
            if (_fadeTrees == null) return;
            Event ev = Game1.CurrentEvent;
            if (ev == null || ev.id != EndingEventKeys.EventId)
            {
                _fadeTrees = null;   // the ending is over; trees return to normal on their own
                return;
            }
            GameLocation loc = Game1.currentLocation;
            if (loc == null) return;
            Rectangle r = _fadeTrees.Value;
            foreach (var pair in loc.terrainFeatures.Pairs)
            {
                if (pair.Value is Tree tree && r.Contains((int)pair.Key.X, (int)pair.Key.Y))
                    tree.alpha = TreeAlpha;
            }
        }
    }
}
