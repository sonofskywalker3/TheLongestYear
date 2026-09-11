using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Pre-reset capture + post-reset restore of the player's Zoom Level and UI Scale.
    /// Pure helper class — no state of its own, just <see cref="Capture"/> /
    /// <see cref="Restore"/>, called either side of <c>loadForNewGame</c> by
    /// <see cref="WorldResetService.PerformReset"/>.
    ///
    /// Why this is needed (Nexus posts tab, RiseiJaku 2026-09-09: "the UI size and Zoom
    /// settings get reset to the game's default values" on every loop reset, "and I don't
    /// think there's a way to change what the game's default values are"). Both halves of
    /// that report are right, and the decompile says why:
    /// <list type="bullet">
    ///   <item><c>Game1.loadForNewGame(loadedGame: false)</c> does
    ///         <c>options = new Options(); options.LoadDefaultOptions();</c>
    ///         (Game1.cs:2954) — the whole Options instance is replaced.</item>
    ///   <item><c>Options.LoadDefaultOptions</c> (Options.cs:582) then repopulates that fresh
    ///         instance from the global <c>default_options</c> file, which is why every OTHER
    ///         setting the player touched (volume, snappy menus, autorun, keybinds) comes back
    ///         on its own and nobody has reported those resetting.</item>
    ///   <item>But it copies only fields WITHOUT <c>[DontLoadDefaultSetting]</c>, and both
    ///         <c>singlePlayerBaseZoomLevel</c> (XML "zoomLevel") and
    ///         <c>singlePlayerDesiredUIScale</c> (XML "uiScale") carry that attribute
    ///         (Options.cs:275-284) — vanilla treats them as per-save, not per-player. So they
    ///         alone fall back to the fresh instance's defaults (1.0 and -1), and no
    ///         <c>default_options</c> entry can ever hold them, exactly as reported.</item>
    /// </list>
    ///
    /// A vanilla new game deliberately starts at default zoom; a TLY loop reset is not a new
    /// game from the player's chair, so the two dials carry over instead.
    ///
    /// Restoring the raw backing fields is enough to apply the change: <c>Game1.Update</c>
    /// (Game1.cs:3336-3358) compares <c>desiredUIScale</c>/<c>desiredBaseZoomLevel</c> against
    /// the applied <c>baseUIScale</c>/<c>baseZoomLevel</c> every tick and calls
    /// <c>refreshWindowSettings()</c> itself when they differ. We write the fields rather than
    /// the <c>desiredUIScale</c> property because that property's setter is a no-op unless
    /// <c>Game1.gameMode == 3</c> (Options.cs:502), and the reset runs across a mode change.
    ///
    /// Local-coop's separate pair is carried too, so a split-screen host doesn't lose its
    /// (differently defaulted) values on the same reset.
    /// </summary>
    internal static class DisplayOptionsCarryover
    {
        /// <summary>The four per-save display fields, read straight off <see cref="Options"/>.
        /// Value type, so it survives the <c>options = new Options()</c> that happens between
        /// <see cref="Capture"/> and <see cref="Restore"/>.</summary>
        internal readonly struct Snapshot
        {
            public Snapshot(float singlePlayerZoom, float localCoopZoom, float singlePlayerUiScale, float localCoopUiScale)
            {
                this.Captured = true;
                this.SinglePlayerZoom = singlePlayerZoom;
                this.LocalCoopZoom = localCoopZoom;
                this.SinglePlayerUiScale = singlePlayerUiScale;
                this.LocalCoopUiScale = localCoopUiScale;
            }

            /// <summary>False on a default-constructed snapshot — i.e. one taken when
            /// <c>Game1.options</c> was null. <see cref="Restore"/> is a no-op for those, so a
            /// missing capture can never write a bogus 0x zoom over the fresh defaults.</summary>
            public bool Captured { get; }

            public float SinglePlayerZoom { get; }
            public float LocalCoopZoom { get; }
            public float SinglePlayerUiScale { get; }
            public float LocalCoopUiScale { get; }
        }

        /// <summary>Read the player's current zoom + UI scale. Call BEFORE
        /// <c>loadForNewGame</c>, which replaces the whole Options instance.</summary>
        public static Snapshot Capture()
        {
            Options options = Game1.options;
            if (options == null)
                return default;

            return new Snapshot(
                options.singlePlayerBaseZoomLevel,
                options.localCoopBaseZoomLevel,
                options.singlePlayerDesiredUIScale,
                options.localCoopDesiredUIScale);
        }

        /// <summary>Write the captured zoom + UI scale onto the post-reset Options instance.
        /// Call AFTER <c>loadForNewGame</c>. Game1.Update applies them on the next tick.</summary>
        public static void Restore(Snapshot snapshot, IMonitor monitor)
        {
            Options options = Game1.options;
            if (!snapshot.Captured || options == null)
                return;

            options.singlePlayerBaseZoomLevel = snapshot.SinglePlayerZoom;
            options.localCoopBaseZoomLevel = snapshot.LocalCoopZoom;
            options.singlePlayerDesiredUIScale = snapshot.SinglePlayerUiScale;
            options.localCoopDesiredUIScale = snapshot.LocalCoopUiScale;

            monitor?.Log(
                $"Reset: display options carried over. Zoom {snapshot.SinglePlayerZoom:0.##}x, " +
                $"UI scale {(snapshot.SinglePlayerUiScale < 0f ? "unset" : snapshot.SinglePlayerUiScale.ToString("0.##") + "x")}.",
                LogLevel.Trace);
        }
    }
}
