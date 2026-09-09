using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    internal static class SeasonTurnEventKeys
    {
        public const string EventId = "sonofskywalker3.TLY.SeasonTurn";
        public const string SeenMail = "tly_turn_seen";
    }

    /// <summary>Season Turn Beats (spec 2026-09-07): the porch scene on the morning of Summer 1,
    /// Fall 1 or Winter 1 after a passed gate. Built like EndingEventInjector and played with the
    /// same custom commands. It starts wherever the farmer woke (the farmhouse) under black and
    /// moves to the doorstep, so no frame of the bedroom draws. Marks are relative to the farm's
    /// door tile so every farm type works. Who says what is SeasonTurn.Lines; the words are i18n.</summary>
    internal static class SeasonTurnEventInjector
    {
        // The door tile the farm reports is the doorway itself; the farmer stands one below it, on
        // the step, and the Junimos on the grass under the deck (the deck and the stash chest hid
        // one on the first try, 2026-09-07): A on the path below, B and C to either side, D out wide.
        private const int StepDown = 1;
        private static readonly (int X, int Y)[] Marks = { (0, 3), (-2, 3), (2, 3), (-4, 4) };
        private const int FadeMs = 1400;

        private static string Junimo(int i) => $"Junimo{i}";

        /// <summary>Same sanitising as the ending: a script is '/'-joined and a line is quoted.</summary>
        private static string EventText(string key) => Sanitise(Strings.Get(key));

        private static string Sanitise(string value)
            => string.IsNullOrEmpty(value) ? value : value.Replace('"', '\'').Replace('/', ',');

        /// <summary>Darkness pushback (Jeff, 2026-09-09): the morning after the board changed, two
        /// Junimos on the porch explain it. The darkness got into the old item, everywhere, so the
        /// hall cannot take one without letting it in; they crossed it off and ask for something
        /// the darkness has not reached. Same staging as the turn, no music, the Winter glow.</summary>
        internal static string BuildTamper(int doorX, int doorY, string oldItemName, string newItemName, bool skippable)
        {
            const int count = 2;
            int stepY = doorY + StepDown;
            var s = new List<string>
            {
                "none",
                "-1000 -1000",
                $"farmer {doorX} {stepY} 2",
                EndingEventCommands.BlackName,
            };
            if (skippable) s.Add("skippable");
            s.AddRange(new[]
            {
                $"{EndingEventCommands.ChangeLocationName} Farm {doorX} {stepY}",
                $"warp farmer {doorX} {stepY}",
                "faceDirection farmer 2",
                $"viewport {doorX} {stepY} clamp",
            });
            for (int j = 0; j < count; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {doorX + Marks[j].X} {doorY + Marks[j].Y} {j}");
            s.Add($"{EndingEventCommands.FadeInName} {FadeMs}");
            s.Add("pause 400");
            for (int j = 0; j < count; j++) s.Add($"jump {Junimo(j)} 8");
            s.Add("playSound junimoMeep1");
            s.Add("pause 500");

            // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
            string[] lines =
            {
                Strings.Get("event.darkness.tamper-1", new Dictionary<string, string> { ["old"] = oldItemName ?? "" }),
                Strings.Get("event.darkness.tamper-2"),
                Strings.Get("event.darkness.tamper-3", new Dictionary<string, string> { ["new"] = newItemName ?? "" }),
            };
            int[] who = { 0, 1, 0 };
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 1) { s.Add("glow 60 0 90 true"); s.Add("playSound shadowDie"); }
                if (i == lines.Length - 1) s.Add("stopGlowing");
                s.Add($"jump {Junimo(who[i])} 6");
                s.Add($"{EndingEventCommands.SayName} {Junimo(who[i])} \"{Sanitise(lines[i])}\"");
                s.Add("pause 250");
            }

            s.Add("pause 400");
            for (int j = 0; j < count; j++) s.Add($"jump {Junimo(j)} 8");
            s.Add("playSound junimoMeep1");
            s.Add("pause 600");
            s.Add($"{EndingEventCommands.FadeOutName} 1200");
            s.Add($"addMailReceived {SeasonTurnEventKeys.SeenMail}");
            s.Add("end");
            return string.Join("/", s);
        }

        internal static string Build(SeasonTurnKind kind, int doorX, int doorY, bool skippable)
        {
            int count = SeasonTurn.JunimoCount(kind);
            int stepY = doorY + StepDown;
            var s = new List<string>
            {
                kind == SeasonTurnKind.Winter ? "none" : "junimoStarSong",
                "-1000 -1000",
                $"farmer {doorX} {stepY} 2",
                EndingEventCommands.BlackName,
            };
            if (skippable) s.Add("skippable");
            s.AddRange(new[]
            {
                $"{EndingEventCommands.ChangeLocationName} Farm {doorX} {stepY}",
                $"warp farmer {doorX} {stepY}",
                "faceDirection farmer 2",
                $"viewport {doorX} {stepY} clamp",
            });
            for (int j = 0; j < count; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {doorX + Marks[j].X} {doorY + Marks[j].Y} {j}");
            s.Add($"{EndingEventCommands.FadeInName} {FadeMs}");
            s.Add("pause 500");
            for (int j = 0; j < count; j++) s.Add($"jump {Junimo(j)} 8");
            s.Add("playSound junimoMeep1");
            s.Add("pause 700");

            IReadOnlyList<(int Junimo, string Key)> lines = SeasonTurn.Lines(kind);
            for (int i = 0; i < lines.Count; i++)
            {
                var (who, key) = lines[i];
                // Mood beats: the uneasy turn drops the music at its second line; the alarmed turn
                // holds a dim purple glow under its second line with a low sound.
                if (kind == SeasonTurnKind.Fall && i == 1) s.Add("stopMusic");
                if (kind == SeasonTurnKind.Winter && i == 1) { s.Add("glow 60 0 90 true"); s.Add("playSound shadowDie"); }
                if (kind == SeasonTurnKind.Winter && i == lines.Count - 1) s.Add("stopGlowing");
                s.Add($"jump {Junimo(who)} 6");
                s.Add($"{EndingEventCommands.SayName} {Junimo(who)} \"{EventText(key)}\"");
                s.Add("pause 250");
            }

            s.Add("pause 400");
            for (int j = 0; j < count; j++) s.Add($"jump {Junimo(j)} 8");
            s.Add("playSound junimoMeep1");
            s.Add("pause 600");
            s.Add($"{EndingEventCommands.FadeOutName} 1200");
            s.Add($"addMailReceived {SeasonTurnEventKeys.SeenMail}");
            s.Add("end");
            return string.Join("/", s);
        }
    }
}
