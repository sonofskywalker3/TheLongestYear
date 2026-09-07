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
    /// are placed by warp. Not skippable: the last line adds the seen mail the driver waits for.</summary>
    internal static class EndingEventInjector
    {
        // Town: the Community Center doors are the warp at (52,20); the steps run along row 22.
        private const int HallX = 52, HallY = 22;

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
                // No globalFade/globalFadeToClear around a changeLocation: changeLocation calls
                // Game1.warpFarmer, which runs its own fade to black and back. Live run 2026-09-06:
                // a globalFade immediately before changeLocation left Game1.locationRequest pending
                // forever (the event hung in Town with eventUp + locationRequest and never reached
                // the Community Center), because the global fade had already consumed the frame the
                // warp fade needed to complete on.
                "changeLocation Town",
                $"warp farmer {HallX} {HallY + 2} true",
                "faceDirection farmer 0",
            };
            int col = HallX - 5;
            foreach (string name in cast.Crowd)
            {
                if (name == cast.Speaker) continue;
                s.Add($"addTemporaryActor {name} 16 32 {col} {HallY} 2 true Character");
                col += (col == HallX - 1) ? 3 : 1;   // leave the centre for the speaker
            }
            if (cast.Speaker != null)
                s.Add($"addTemporaryActor {cast.Speaker} 16 32 {HallX} {HallY} 2 true Character");
            for (int j = 0; j < 4; j++)
                s.Add($"addTemporaryActor Junimo 16 16 {HallX - 3 + j * 2} {HallY - 4} 2 false character Junimo{j}");
            s.AddRange(new[]
            {
                $"viewport {HallX} {HallY} true",
                "pause 600",
                "playSound reward",
                "screenFlash 0.4",
                "jump Junimo0 8", "jump Junimo1 8", "jump Junimo2 8", "jump Junimo3 8",
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
            if (cast.Speaker != null && cast.SpeakerMiddleKey != null)
            {
                var tokens = new Dictionary<string, string>
                {
                    ["scene"] = cast.SceneKey != null ? Strings.Get(cast.SceneKey) : string.Empty,
                };
                s.AddRange(new[]
                {
                    $"move {cast.Speaker} 0 1 2",
                    "pause 400",
                    $"speak {cast.Speaker} \"{EventText(EndingLine.OpenKey)}\"",
                    $"emote {cast.Speaker} 8",
                    "pause 900",
                    $"speak {cast.Speaker} \"{EventText(cast.SpeakerMiddleKey, tokens)}\"",
                    "pause 400",
                    $"speak {cast.Speaker} \"{EventText(EndingLine.CloseKey)}\"",
                    "pause 600",
                });
            }

            // ---- Scene 4: Morris ----
            // The two "move Morris" lines below walk the row at HallY+2, columns HallX+4 through
            // HallX+9 (the paved stretch east of the hall steps, toward the saloon). Verified live
            // 2026-09-06 on Standard and Meadowlands: both moves executed, the row walks clear and
            // no warp workaround is needed (see STATUS.md, Year One Ending).
            s.AddRange(new[]
            {
                "stopMusic",
                $"addTemporaryActor Morris 16 32 {HallX + 9} {HallY + 2} 3 true Character",
                $"move Morris -5 0 3",
                "pause 500",
                $"speak Morris \"{EventText("event.ending.morris-1")}\"",
                "pause 200",
                $"speak Morris \"{EventText("event.ending.morris-2")}\"",
                "pause 200",
                $"speak Morris \"{EventText("event.ending.morris-3")}\"",
                "pause 300",
                "changeSprite Morris Dark",
                "glow 90 0 0 false",
                "playSound shadowDie",
                $"speak Morris \"{EventText("event.ending.morris-4")}\"",
                "pause 700",
                "stopGlowing",
                "changeSprite Morris",
                "pause 300",
                "jump Junimo0 4", "jump Junimo2 4",
                "playSound junimoMeep1",
                $"move Morris 5 0 1",
                "pause 400",
                "playSound doorClose",
                "pause 300",
                "playSound thudStep",
                "pause 800",

                // ---- Scene 5: inside the hall, six Junimos ----
                "changeLocation CommunityCenter",
                "warp farmer 32 16 true",
                "faceDirection farmer 0",
            });
            for (int j = 0; j < 6; j++)
                s.Add($"addTemporaryActor Junimo 16 16 {28 + j * 2} {11 + (j % 2)} 2 false character Junimo{j}");
            s.AddRange(new[]
            {
                "viewport 32 14 true",
                "playSound junimoMeep1",
                "jump Junimo0 8", "jump Junimo1 8", "jump Junimo2 8", "jump Junimo3 8", "jump Junimo4 8", "jump Junimo5 8",
                "pause 800",
                $"speak Junimo0 \"{EventText("event.ending.junimo-1")}\"",
                "pause 200",
                $"speak Junimo0 \"{EventText("event.ending.junimo-2")}\"",
                "pause 400",
                $"speak Junimo0 \"{EventText("event.ending.junimo-3")}\"",
                "pause 300",
                $"speak Junimo0 \"{EventText("event.ending.junimo-4")}\"",
                "pause 300",
                $"speak Junimo0 \"{EventText("event.ending.junimo-5")}\"",
                "pause 600",

                // ---- Scene 6: the shrine at dusk ----
                "changeLocation Farm",
                $"warp farmer {cast.ShrineX + 1} {cast.ShrineY + 2} true",
                "faceDirection farmer 0",
                $"viewport {cast.ShrineX} {cast.ShrineY} true",
                "ambientLight 120 100 160",
                "pause 1200",
                GrandpaCandleCommand.Name,
                "pause 1500",
                $"message \"{EventText("event.ending.grandpa")}\"",
                "pause 1500",
                "globalFade",
                $"addMailReceived {EndingEventKeys.SeenMail}",
                "end",
            });
            return string.Join("/", s);
        }
    }
}
