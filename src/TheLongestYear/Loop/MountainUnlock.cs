using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Clears the day-1-to-day-4 landslide rubble at the mine entrance so the mines are accessible
    /// from Spring 1 of every run. Vanilla initialises <c>Mountain.landslide</c> to
    /// <c>Game1.stats.DaysPlayed &lt; 5</c> (Mountain.cs:34) and only clears it in dayUpdate when
    /// DaysPlayed >= 5 — TLY's runs start at Spring 1 / DaysPlayed = 1 every loop, so without this
    /// patch the mines would be re-blocked on every reset.
    ///
    /// Source-of-truth flags from the 1.6 Android decompile:
    ///   - Mountain.landslide (private readonly NetBool) — controls the visual + the
    ///     <c>isCollidingPosition</c> / <c>isTilePlaceable</c> rubble block (Mountain.cs:354/368).
    ///   - mail "landslideDone" — added by vanilla on the dayUpdate clear (Mountain.cs:229);
    ///     we set it here too so anything else gating on the flag (none in 1.6 vanilla, but
    ///     content packs may key off it) reads consistent state.
    /// </summary>
    internal sealed class MountainUnlock
    {
        private static readonly System.Reflection.FieldInfo LandslideField
            = AccessTools.Field(typeof(Mountain), "landslide");

        private readonly IMonitor _monitor;

        public MountainUnlock(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Clear the mine-entrance rubble for the current run. Idempotent.</summary>
        public void Apply()
        {
            Mountain mountain = Game1.getLocationFromName("Mountain") as Mountain;
            if (mountain == null)
            {
                _monitor.Log("MountainUnlock: Mountain location not loaded yet — skipping.", LogLevel.Trace);
                return;
            }

            if (LandslideField == null)
            {
                _monitor.Log(
                    "MountainUnlock: Mountain.landslide field not found via reflection — " +
                    "the field may have been renamed in this game version. Mines may stay blocked until day 5.",
                    LogLevel.Warn);
                return;
            }

            if (LandslideField.GetValue(mountain) is NetBool landslide)
            {
                if (landslide.Value)
                {
                    landslide.Value = false;
                    _monitor.Log("MountainUnlock: cleared the mine-entrance landslide rubble.", LogLevel.Info);
                }
            }

            // noLetter: true — set the flag silently. The visible "we cleared the boulder"
            // letter is stale every loop in TLY (it lands days after the player has already
            // walked the cleared path), reported by the 2026-05-28 playtest as "the joja
            // 'we cleared the rocks' mail." Nothing in 1.6 vanilla gates on the LETTER
            // arriving — only the flag — so suppressing the letter is safe.
            if (!Game1.MasterPlayer.hasOrWillReceiveMail("landslideDone"))
                Game1.addMail("landslideDone", noLetter: true, sendToEveryone: true);
        }
    }
}
