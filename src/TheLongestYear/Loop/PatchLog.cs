using StardewModdingAPI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Shared static monitor used by Harmony patches that don't have an IMonitor injected
    /// (free static classes annotated with <see cref="HarmonyLib.HarmonyPatchAttribute"/>).
    /// Wired once from <c>ModEntry</c> at startup. No-op until connected, so a stray patch
    /// that fires before <c>Connect</c> won't NRE.
    ///
    /// Added 2026-05-28 round 4 to log bonus-drop firings (forage_yield_up, mine_drops_up,
    /// all_drops_up) so the playtester can confirm via the SMAPI log that the bonuses are
    /// actually triggering — user reported "I'm not sure I'm actually ever getting a +1 item."
    /// </summary>
    internal static class PatchLog
    {
        private static IMonitor _monitor;

        public static void Connect(IMonitor monitor) => _monitor = monitor;

        public static void Trace(string message) => _monitor?.Log(message, LogLevel.Trace);
        public static void Info(string message)  => _monitor?.Log(message, LogLevel.Info);
        public static void Warn(string message)  => _monitor?.Log(message, LogLevel.Warn);

        private static readonly System.Collections.Generic.HashSet<string> Logged = new();

        /// <summary>Trace a failure once per <paramref name="key"/> for the session, for fallbacks on
        /// paths that run every frame (a draw call would otherwise log the same line 60 times a second).</summary>
        public static void TraceOnce(string key, string message)
        {
            if (Logged.Add(key)) Trace(message);
        }

        /// <summary>Warn once per <paramref name="key"/> for the session.</summary>
        public static void WarnOnce(string key, string message)
        {
            if (Logged.Add(key)) Warn(message);
        }
    }
}
