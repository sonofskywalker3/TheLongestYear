using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;

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
    /// frame of Town up and to the right of the hall). This one lands the farmer on the target tile in
    /// the same call, through the same private Event.changeLocation the vanilla command uses.
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
        private const string JunimoDisplayName = "Junimo";

        private static readonly MethodInfo EventChangeLocation = typeof(Event).GetMethod(
            "changeLocation", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(string), typeof(int), typeof(int), typeof(Action) }, null);
        private static readonly MethodInfo TownRefurbish = typeof(Town).GetMethod(
            "refurbishCommunityCenter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo JunimoColour = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);

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

        public static void Register(IMonitor monitor)
        {
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
                if (!ArgUtility.TryGet(args, 1, out string location, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int y, out error)
                    || EventChangeLocation == null)
                {
                    monitor.Log($"{ChangeLocationName}: {error ?? "Event.changeLocation not found"}; skipping.", LogLevel.Warn);
                    evt.CurrentCommand++;
                    return;
                }
                try
                {
                    Action onComplete = () =>
                    {
                        Game1.currentLocation.ResetForEvent(evt);
                        evt.CurrentCommand++;
                    };
                    EventChangeLocation.Invoke(evt, new object[] { location, x, y, onComplete });
                }
                catch (Exception ex)
                {
                    monitor.Log($"{ChangeLocationName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
                    evt.CurrentCommand++;
                }
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
    }
}
