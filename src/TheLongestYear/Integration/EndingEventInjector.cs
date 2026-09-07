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

        // Ceremony blocking (Town), laid out from a 1080p screenshot at the player's zoom (about
        // 15 by 8 tiles on screen; the dialogue box covers everything from row 26 down when the
        // camera sits on row 25). Lewis on the top step in front of the doors, the farmer on the
        // paving below him, the crowd in rows 23 to 25 around the farmer, clear of the big bushes
        // (columns 46 to 49 and 54 to 57 above row 23) and the bush east of the steps.
        private const int FarmerX = HallX, FarmerY = HallY + 2;
        private const int LewisX = HallX, LewisY = HallY - 1;
        private const int CameraY = HallY + 2;
        // The speaker stands on the farmer's row, as vanilla stages side-by-side talk (one row up
        // read as half a tile too high, the same row as half a tile low; the row wins, 2026-09-07).
        private const int SpeakerX = HallX - 2, SpeakerY = HallY + 2;
        private static readonly (int X, int Y)[] CrowdSlots =
        {
            (48, 23), (54, 23), (56, 23),
            (47, 24), (49, 24), (55, 24), (57, 24),
            (46, 25), (48, 25), (50, 25), (54, 25), (56, 25),
            (52, 26),
        };

        // Four Junimos on the open grass at the two edges of the screen, outside the crowd (the
        // roof hid them under the front layer, the first ground spots put two inside a tree and a
        // bush, 2026-09-07). Six on the hall floor, in front of the farmer.
        private static readonly (int X, int Y)[] TownJunimos =
        {
            (40, 23), (42, 25), (62, 23), (64, 25),
        };
        // Framed from a screenshot: the camera cannot go higher than about row 16 in the hall, and
        // the dialogue box covers rows 16 and down, so the cast sits on rows 12 to 15 and stays
        // clear of the potted plant at columns 34 to 36, rows 13 to 14 (2026-09-07).
        private const int HallFarmerX = 32, HallFarmerY = 15, HallViewY = 13;
        private static readonly (int X, int Y)[] HallSeats =
        {
            (29, 13), (31, 12), (33, 12), (30, 14), (28, 15), (36, 15),
        };

        // Morris comes and goes along row 28, the open plaza south of the crowd (the paving row at
        // HallY + 2 runs through the bush east of the steps, 2026-09-07), from off screen east
        // (the screen ends about 8 tiles east of the hall centre at this zoom; 18 is safe for a
        // zoomed-out player too). He stops east of the crowd and steps up three tiles to row 25,
        // the last row the dialogue box leaves visible.
        private const int MorrisRow = HallY + 6, MorrisFarX = HallX + 18, MorrisNearX = HallX + 6;
        private const int MorrisStepUp = 3, MorrisSpeed = 5;

        // Scene 6 timings.
        private const int PanMs = 6000;
        private const int FadeInMs = 1400;

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
                "viewport 66 18 clamp true",
                "faceDirection farmer 1",
                "pause 800",
                $"speak Lewis \"{EventText("event.ending.lewis-porch-1")}\"",
                "pause 200",
                $"speak Lewis \"{EventText("event.ending.lewis-porch-2")}\"",
                "pause 400",

                // ---- Scene 2: the hall steps ----
                // tlyChangeLocation fades to black with the world intact, warps under black and fades
                // back in on the hall (see EndingEventCommands). The viewport commands after each
                // change carry no "true": that flag is vanilla's cut-to-black-then-fade-in, which
                // read as a second flash on every transition (2026-09-07).
                // The screen stays black after the change until tlyFadeIn, so the whole cast is in
                // place before anyone sees the hall (they popped in after the fade, 2026-09-07).
                $"{EndingEventCommands.ChangeLocationName} Town {FarmerX} {FarmerY}",
                EndingEventCommands.RefurbishHallName,
                $"warp farmer {FarmerX} {FarmerY}",
                "faceDirection farmer 0",
                $"viewport {HallX} {CameraY}",
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
            for (int j = 0; j < TownJunimos.Length; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {TownJunimos[j].X} {TownJunimos[j].Y} {j}");
            s.AddRange(new[]
            {
                $"{EndingEventCommands.FadeInName} {FadeInMs}",
                "pause 600",
                "playSound reward",
                "screenFlash 0.4",
                $"jump {Junimo(0)} 8", $"jump {Junimo(1)} 8", $"jump {Junimo(2)} 8", $"jump {Junimo(3)} 8",
                "playSound junimoMeep1",
                "pause 800",
                $"{EndingEventCommands.SayName} Lewis \"{EventText("event.ending.lewis-hall-1")}\"",
                "pause 200",
                $"{EndingEventCommands.SayName} Lewis \"{EventText("event.ending.lewis-hall-2")}\"",
                "pause 200",
                $"{EndingEventCommands.SayName} Lewis \"{EventText("event.ending.lewis-hall-3")}\"",
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
                    $"{EndingEventCommands.SayName} {cast.Speaker} \"{EventText(EndingLine.OpenKey)}\"",
                    $"emote {cast.Speaker} 8",
                    "pause 900",
                    $"{EndingEventCommands.SayName} {cast.Speaker} \"{EventText(cast.SpeakerMiddleKey, tokens)}\"",
                    "pause 400",
                    $"{EndingEventCommands.SayName} {cast.Speaker} \"{EventText(EndingLine.CloseKey)}\"",
                    "pause 600",
                    "faceDirection farmer 0",
                });
            }

            // ---- Scene 4: Morris ----
            // Morris walks row 28 in from off screen east, steps up to the crowd's edge and leaves
            // the same way, at speed 5 so neither walk drags. The farmer turns to face him while he
            // talks. His "dark" sheet (MorrisDarkSprite) is the whole figure shadowed with red eyes;
            // it goes on at the throwaway line under a red screen glow and STAYS on him as he walks
            // off. His exit does not wait: the cut to the hall fades out over his walk.
            s.AddRange(new[]
            {
                "stopMusic",
                $"addTemporaryActor Morris 16 32 {MorrisFarX} {MorrisRow} 3 true Character",
                $"speed Morris {MorrisSpeed}",
                $"move Morris {MorrisNearX - MorrisFarX} 0 3",
                $"move Morris 0 {-MorrisStepUp} 3",
                "faceDirection farmer 1",
                "pause 500",
                $"{EndingEventCommands.SayName} Morris \"{EventText("event.ending.morris-1")}\"",
                "pause 200",
                $"{EndingEventCommands.SayName} Morris \"{EventText("event.ending.morris-2")}\"",
                "pause 200",
                $"{EndingEventCommands.SayName} Morris \"{EventText("event.ending.morris-3")}\"",
                "pause 300",
                "changeSprite Morris Dark",
                "glow 90 0 0 true",
                "playSound shadowDie",
                "pause 400",
                $"{EndingEventCommands.SayName} Morris \"{EventText("event.ending.morris-4")}\"",
                "pause 700",
                "stopGlowing",
                "pause 300",
                $"jump {Junimo(0)} 4", $"jump {Junimo(2)} 4",
                "playSound junimoMeep1",
                $"move Morris 0 {MorrisStepUp} 2",
                $"move Morris {MorrisFarX - MorrisNearX} 0 1 true",
                "faceDirection farmer 0",
                "pause 900",

                // ---- Scene 5: inside the hall, six Junimos ----
                $"{EndingEventCommands.ChangeLocationName} CommunityCenter {HallFarmerX} {HallFarmerY}",
                $"warp farmer {HallFarmerX} {HallFarmerY}",
                "faceDirection farmer 0",
                $"viewport {HallFarmerX} {HallViewY}",
            });
            for (int j = 0; j < HallSeats.Length; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {HallSeats[j].X} {HallSeats[j].Y} {j}");
            s.Add($"{EndingEventCommands.FadeInName} {FadeInMs}");
            // The lines pass between the Junimos: 0 opens, 1 and 2 carry the middle, 3 the warning,
            // 0 closes. Each speaker hops before its line so the eye finds it.
            string junimo4 = crack ? EventText("event.ending.junimo-4") : EventText("event.ending.junimo-4-nocrack");
            s.AddRange(new[]
            {
                "playSound junimoMeep1",
                $"jump {Junimo(0)} 8", $"jump {Junimo(1)} 8", $"jump {Junimo(2)} 8", $"jump {Junimo(3)} 8", $"jump {Junimo(4)} 8", $"jump {Junimo(5)} 8",
                "pause 800",
                $"jump {Junimo(0)} 6",
                $"{EndingEventCommands.SayName} {Junimo(0)} \"{EventText("event.ending.junimo-1")}\"",
                "pause 200",
                $"jump {Junimo(1)} 6",
                $"{EndingEventCommands.SayName} {Junimo(1)} \"{EventText("event.ending.junimo-2")}\"",
                "pause 400",
                $"jump {Junimo(2)} 6",
                $"{EndingEventCommands.SayName} {Junimo(2)} \"{EventText("event.ending.junimo-3")}\"",
                "pause 300",
                $"jump {Junimo(3)} 6",
                $"{EndingEventCommands.SayName} {Junimo(3)} \"{junimo4}\"",
                "pause 300",
                $"jump {Junimo(0)} 6",
                $"{EndingEventCommands.SayName} {Junimo(0)} \"{EventText("event.ending.junimo-5")}\"",
                "pause 600",

                // ---- Scene 6: home, then the shrine at dusk ----
                // Land on the Farm two tiles below the farmhouse door, walk in (door sound, farmer
                // hidden), then glide the camera to the shrine, clamped to the map so no black edge
                // shows. Grandpa's line, THEN the candle, then to black. The farmer is put back on
                // the doorstep under the fade so the continuation finds them somewhere sensible.
                $"{EndingEventCommands.ChangeLocationName} Farm {cast.DoorX} {cast.DoorY + 2}",
                $"warp farmer {cast.DoorX} {cast.DoorY + 2}",
                "faceDirection farmer 0",
                $"viewport {cast.DoorX} {cast.DoorY} clamp",
                // ambientLight is subtractive (the amount taken from each channel). Vanilla's evening
                // takes red and green and leaves blue; this is that at about half strength. The first
                // try (120 100 160) went green, the second (50 120 90) red.
                "ambientLight 120 120 40",
                $"{EndingEventCommands.FadeInName} {FadeInMs}",
                "pause 600",
                // DoorY is the doorstep (the tile below the door itself); one step up from
                // DoorY + 2 ends on it. Two steps walked the farmer into the wall (2026-09-07).
                "move farmer 0 -1 0",
                "pause 200",
                "playSound doorClose",
                "warp farmer -100 -100",
                "pause 900",
                $"{EndingEventCommands.FadeTreesName} {cast.ShrineX} {cast.ShrineY}",
                $"{EndingEventCommands.PanToName} {cast.ShrineX} {cast.ShrineY} {PanMs}",
                "pause 1200",
                // "message" does not expand @ the way "speak" does, so the farmer's name goes in here.
                $"message \"{EventText("event.ending.grandpa").Replace("@", StardewValley.Game1.player.Name)}\"",
                "pause 900",
                GrandpaCandleCommand.Name,
                "pause 2200",
                $"{EndingEventCommands.FadeOutName} 1500",
                $"warp farmer {cast.DoorX} {cast.DoorY}",
                $"addMailReceived {EndingEventKeys.SeenMail}",
                "end",
            });
            return string.Join("/", s);
        }
    }
}
