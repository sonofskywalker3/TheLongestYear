using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Suppresses the vanilla "Rat Problem" quest (Data/Quests id 26 — "Lewis wants you to
    /// investigate the old community center") during a TLY run. In vanilla it's the breadcrumb that
    /// sends a first-time player into the CC to discover the Junimos. TLY opens the CC and reveals
    /// every Junimo Note from day 1 of every loop (see <see cref="CommunityCenterUnlock"/>), so the
    /// quest is stale and confusing — it points at something already done. khauser13 / niki_m_m3
    /// (Nexus) ran into it; the author committed to removing it.
    ///
    /// Two-pronged so it can't slip through:
    ///   - <see cref="AddQuestPrefix"/> intercepts <c>Farmer.addQuest("26")</c> so it never enters
    ///     the log mid-run (whatever delivers it — mail, calendar trigger).
    ///   - <see cref="StripFromLog"/> is called on save load to clear it from saves that already
    ///     carry it.
    /// Both gate on <see cref="RunActivation.IsActive"/>, so non-TLY saves keep the vanilla quest.
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.addQuest))]
    internal static class RatProblemQuestPatch
    {
        /// <summary>Data/Quests id for the "Rat Problem" CC-investigation quest.</summary>
        internal const string RatProblemQuestId = "26";

        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        [HarmonyPrefix]
        private static bool AddQuestPrefix(string questId)
        {
            if (!RunActivation.IsActive) return true;            // dormant on non-TLY saves
            return questId != RatProblemQuestId;                 // false => skip the vanilla add
        }

        /// <summary>Remove the Rat Problem quest from the current player's log if present. Called on
        /// save load (active run only) so an existing save that already received it gets cleaned.
        /// Idempotent and cheap. Returns true if a quest was removed (for logging).</summary>
        internal static bool StripFromLog(IMonitor monitor)
        {
            if (!RunActivation.IsActive || Game1.player == null) return false;
            if (!Game1.player.hasQuest(RatProblemQuestId)) return false;

            Game1.player.removeQuest(RatProblemQuestId);
            monitor.Log("Removed the vanilla 'Rat Problem' quest (id 26) — the CC is already open this run.",
                LogLevel.Info);
            return true;
        }
    }
}
