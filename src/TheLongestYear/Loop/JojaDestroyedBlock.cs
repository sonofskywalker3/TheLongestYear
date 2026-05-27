using HarmonyLib;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// v1 design: Joja Mart must look intact on every fresh-run Spring 1, but
    /// vanilla's <see cref="Town"/> map-setup code repaints it to the cracked /
    /// abandoned "destroyed Joja" backdrop whenever event 191393 has been seen
    /// (Town.cs:577-583). Our <see cref="CommunityCenterUnlock"/> step adds
    /// 191393 to <c>MasterPlayer.eventsSeen</c> intentionally — that's what
    /// lets the player walk into the CC on day 1 without sitting through the
    /// Demetrius unlock cutscene. The unwanted side-effect was a destroyed
    /// Joja exterior even though Joja itself is still functioning.
    ///
    /// Fix: prefix <c>Town.showDestroyedJoja</c> with a no-op. Vanilla's only
    /// other path to this method is the Joja-victory ending arc, and v1 blocks
    /// Joja membership entirely via <see cref="JojaMembershipBlock"/>, so there
    /// is no legitimate world state where we want destroyed-Joja tiles drawn.
    ///
    /// Identified during 2026-05-26 playtest. See the night-2 handoff spec.
    /// </summary>
    [HarmonyPatch(typeof(Town), "showDestroyedJoja")]
    internal static class JojaDestroyedBlock
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static bool Prefix() => false;
    }
}
