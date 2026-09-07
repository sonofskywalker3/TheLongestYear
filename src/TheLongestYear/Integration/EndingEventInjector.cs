using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;

namespace TheLongestYear.Integration
{
    internal static class EndingEventKeys
    {
        public const string EventId = "sonofskywalker3.TLY.Ending";
        public const string SeenMail = "tly_ending_seen";
    }

    /// <summary>Year One Ending (spec 2026-09-06 section 4): the six-scene script, built the way
    /// IntroEventInjector builds the intro. Started by EndingEventDriver on the Farm, so scene 1 needs
    /// no changeLocation. Town tiles are fixed and known, so actors may walk there; on the Farm they
    /// are placed by warp. Not skippable: the last line adds the seen mail the driver waits for.
    /// The staging (who stands where, who says what, every transition) is written up for editing in
    /// docs/superpowers/specs/2026-09-07-year-one-ending-script.md; keep the two in step.</summary>
    internal static class EndingEventInjector
    {
        // Town: the Community Center doors are the warp at (52,20); the steps run along row 22.
        private const int HallX = 52, HallY = 22;

        // Ceremony blocking (Town). Lewis on the top step in front of the doors, the farmer on the
        // paving below him, the crowd in a loose fan around the farmer (rows 23..27, never the bush
        // east of the steps at column 57+, where a straight line of twelve ended up on 2026-09-07).
        // The speaker starts two tiles west of the farmer and steps in beside them for the crack.
        private const int FarmerX = HallX, FarmerY = HallY + 2;
        private const int LewisX = HallX, LewisY = HallY - 1;
        private const int SpeakerX = HallX - 2, SpeakerY = HallY + 2;
        private static readonly (int X, int Y)[] CrowdSlots =
        {
            (49, 23), (55, 23),
            (48, 24), (56, 24),
            (47, 25), (50, 25), (54, 25), (57, 25),
            (49, 26), (52, 26), (55, 26),
            (51, 27), (53, 27),
        };

        // Four Junimos on the hall roof (Town, the ridge above the doors), six on the hall floor.
        private const int RoofY = HallY - 8;
        private static readonly (int X, int Y)[] HallSeats =
        {
            (29, 12), (31, 13), (33, 12), (35, 13), (37, 12), (32, 14),
        };

        // Morris walks the paving row in from the east edge of the screen and back out again. At
        // 1080p the viewport is ~30 tiles wide, so 18 tiles east of the hall centre is off screen.
        private const int MorrisFarX = HallX + 18, MorrisNearX = HallX + 5, MorrisSpeed = 5;

        // Scene 6 timings.
        private const int PanMs = 6000;

        private static string Junimo(int i) => $"Junimo{i}";

        /// <summary>Localized text made safe to drop inside an event script. A script is one string
        /// whose commands are joined with '/', and a <c>speak</c> / <c>message</c> payload is wrapped
        /// in double quotes, so a translated line containing either character would split the script
        /// into bogus commands or unbalance the quotes and break the whole ending. English never does
        /// (I18nGuardTests asserts that), but a community translation is outside our control, so
        /// sanitise at the point of use: '"' becomes a single quote, '/' becomes a comma. Both
        /// substitutions read naturally in prose and neither introduces an em dash.</summary>
        private static string EventText(string key, IReadOnlyDictionary<string, string> tokens = null)
        {
            string value = tokens == null ? Strings.Get(key) : Strings.Get(key, tokens);
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace('"', '\'').Replace('/', ',');
        }

        internal static string Build(EndingCast cast)
        {
            var s = new List<string>
            {
                "junimoStarSong",
                "66 18",
                "farmer 66 18 2",

                // ---- Scene 1: the porch (Standard-farm tiles; the game offsets per farm type) ----
                "warp farmer 66 18 true",
                "addTemporaryActor Lewis 16 32 68 18 3 true Character",
                "viewport 66 18 true",
                "faceDirection farmer 1",
                "pause 800",
                $"speak Lewis \"{EventText("event.ending.lewis-porch-1")}\"",
                "pause 200",
                $"speak Lewis \"{EventText("event.ending.lewis-porch-2")}\"",
                "pause 400",

                // ---- Scene 2: the hall steps ----
                // tlyChangeLocation lands the farmer on the target tile inside the warp itself, so the
                // first Town frame is already centred on the hall (vanilla changeLocation kept the
                // Farm tile for a frame: one frame of Town up and to the right, 2026-09-07). No
                // globalFade around it: the warp runs its own fade, and an extra global fade left
                // Game1.locationRequest pending forever (2026-09-06).
                $"{EndingEventCommands.ChangeLocationName} Town {FarmerX} {FarmerY}",
                EndingEventCommands.RefurbishHallName,
                "faceDirection farmer 0",
                $"viewport {HallX} {HallY} true",
                $"addTemporaryActor Lewis 16 32 {LewisX} {LewisY} 2 true Character",
            };
            int slot = 0;
            foreach (string name in cast.Crowd)
            {
                // Lewis is on the step already; the speaker has their own mark.
                if (name == "Lewis" || name == cast.Speaker || slot >= CrowdSlots.Length) continue;
                var (x, y) = CrowdSlots[slot++];
                s.Add($"addTemporaryActor {name} 16 32 {x} {y} 0 true Character");
            }
            if (cast.Speaker != null)
                s.Add($"addTemporaryActor {cast.Speaker} 16 32 {SpeakerX} {SpeakerY} 0 true Character");
            for (int j = 0; j < 4; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {HallX - 3 + j * 2} {RoofY} {j}");
            s.AddRange(new[]
            {
                "pause 600",
                "playSound reward",
                "screenFlash 0.4",
                $"jump {Junimo(0)} 8", $"jump {Junimo(1)} 8", $"jump {Junimo(2)} 8", $"jump {Junimo(3)} 8",
                "playSound junimoMeep1",
                "pause 800",
                $"speak Lewis \"{EventText("event.ending.lewis-hall-1")}\"",
                "pause 200",
                $"speak Lewis \"{EventText("event.ending.lewis-hall-2")}\"",
                "pause 200",
                $"speak Lewis \"{EventText("event.ending.lewis-hall-3")}\"",
                "pause 600",
            });

            // ---- Scene 3: the crack (only with a speaker) ----
            bool crack = cast.Speaker != null && cast.SpeakerMiddleKey != null;
            if (crack)
            {
                var tokens = new Dictionary<string, string>
                {
                    ["scene"] = cast.SceneKey != null ? Strings.Get(cast.SceneKey) : string.Empty,
                };
                s.AddRange(new[]
                {
                    $"move {cast.Speaker} 1 0 1",
                    "faceDirection farmer 3",
                    "pause 400",
                    $"speak {cast.Speaker} \"{EventText(EndingLine.OpenKey)}\"",
                    $"emote {cast.Speaker} 8",
                    "pause 900",
                    $"speak {cast.Speaker} \"{EventText(cast.SpeakerMiddleKey, tokens)}\"",
                    "pause 400",
                    $"speak {cast.Speaker} \"{EventText(EndingLine.CloseKey)}\"",
                    "pause 600",
                    "faceDirection farmer 0",
                });
            }

            // ---- Scene 4: Morris ----
            // Morris walks the paving row (HallY + 2) in from off screen east and leaves the same way,
            // at speed 5 so neither walk drags. The farmer turns to face him while he talks. His
            // "dark" sheet (MorrisDarkSprite) is the whole figure shadowed with red eyes, held under
            // a red screen glow until the line is done.
            s.AddRange(new[]
            {
                "stopMusic",
                $"addTemporaryActor Morris 16 32 {MorrisFarX} {FarmerY} 3 true Character",
                $"speed Morris {MorrisSpeed}",
                $"move Morris {MorrisNearX - MorrisFarX} 0 3",
                "faceDirection farmer 1",
                "pause 500",
                $"speak Morris \"{EventText("event.ending.morris-1")}\"",
                "pause 200",
                $"speak Morris \"{EventText("event.ending.morris-2")}\"",
                "pause 200",
                $"speak Morris \"{EventText("event.ending.morris-3")}\"",
                "pause 300",
                "changeSprite Morris Dark",
                "glow 90 0 0 true",
                "playSound shadowDie",
                "pause 400",
                $"speak Morris \"{EventText("event.ending.morris-4")}\"",
                "pause 700",
                "stopGlowing",
                "changeSprite Morris",
                "pause 300",
                $"jump {Junimo(0)} 4", $"jump {Junimo(2)} 4",
                "playSound junimoMeep1",
                $"move Morris {MorrisFarX - MorrisNearX} 0 1",
                "faceDirection farmer 0",
                "pause 400",
                "playSound doorClose",
                "pause 300",
                "playSound thudStep",
                "pause 800",

                // ---- Scene 5: inside the hall, six Junimos ----
                $"{EndingEventCommands.ChangeLocationName} CommunityCenter 32 16",
                "faceDirection farmer 0",
                "viewport 32 14 true",
            });
            for (int j = 0; j < HallSeats.Length; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {HallSeats[j].X} {HallSeats[j].Y} {j}");
            // The lines pass between the Junimos: 0 opens, 1 and 2 carry the middle, 3 the warning,
            // 0 closes. Each speaker hops before its line so the eye finds it.
            string junimo4 = crack ? EventText("event.ending.junimo-4") : EventText("event.ending.junimo-4-nocrack");
            s.AddRange(new[]
            {
                "playSound junimoMeep1",
                $"jump {Junimo(0)} 8", $"jump {Junimo(1)} 8", $"jump {Junimo(2)} 8", $"jump {Junimo(3)} 8", $"jump {Junimo(4)} 8", $"jump {Junimo(5)} 8",
                "pause 800",
                $"jump {Junimo(0)} 6",
                $"speak {Junimo(0)} \"{EventText("event.ending.junimo-1")}\"",
                "pause 200",
                $"jump {Junimo(1)} 6",
                $"speak {Junimo(1)} \"{EventText("event.ending.junimo-2")}\"",
                "pause 400",
                $"jump {Junimo(2)} 6",
                $"speak {Junimo(2)} \"{EventText("event.ending.junimo-3")}\"",
                "pause 300",
                $"jump {Junimo(3)} 6",
                $"speak {Junimo(3)} \"{junimo4}\"",
                "pause 300",
                $"jump {Junimo(0)} 6",
                $"speak {Junimo(0)} \"{EventText("event.ending.junimo-5")}\"",
                "pause 600",

                // ---- Scene 6: home, then the shrine at dusk ----
                // Land on the Farm two tiles below the farmhouse door, walk in (door sound, farmer
                // hidden), then glide the camera to the shrine, clamped to the map so no black edge
                // shows. Grandpa's line, THEN the candle, then to black. The farmer is put back on
                // the doorstep under the fade so the continuation finds them somewhere sensible.
                $"{EndingEventCommands.ChangeLocationName} Farm {cast.DoorX} {cast.DoorY + 2}",
                "faceDirection farmer 0",
                $"viewport {cast.DoorX} {cast.DoorY} clamp true",
                "ambientLight 120 100 160",
                "pause 600",
                "move farmer 0 -2 0",
                "pause 200",
                "playSound doorClose",
                "warp farmer -100 -100",
                "pause 900",
                $"{EndingEventCommands.PanToName} {cast.ShrineX} {cast.ShrineY} {PanMs}",
                "pause 1200",
                // "message" does not expand @ the way "speak" does, so the farmer's name goes in here.
                $"message \"{EventText("event.ending.grandpa").Replace("@", StardewValley.Game1.player.Name)}\"",
                "pause 900",
                GrandpaCandleCommand.Name,
                "pause 2200",
                "globalFade",
                $"warp farmer {cast.DoorX} {cast.DoorY}",
                $"addMailReceived {EndingEventKeys.SeenMail}",
                "end",
            });
            return string.Join("/", s);
        }
    }
}
