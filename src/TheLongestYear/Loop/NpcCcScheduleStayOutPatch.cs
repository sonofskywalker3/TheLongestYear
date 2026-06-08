using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Keep villagers OUT of the Community Center during a TLY run, while leaving it walkable for
    /// the player.
    ///
    /// <para>
    /// <see cref="CcLocationAccessiblePatch"/> forces <c>Game1.isLocationAccessible("CommunityCenter")</c>
    /// true so the player can enter on Spring 1. But that exact flag is ALSO what vanilla NPC
    /// scheduling reads: <c>NPC.changeScheduleForLocationAccessibility</c> returns
    /// <c>!isLocationAccessible("CommunityCenter")</c>, and a <c>true</c> return cancels that
    /// schedule point (the villager falls back to their default schedule). By forcing accessibility
    /// true, we accidentally un-cancel every villager's CC schedule entry — so Clint, Gus, Evelyn,
    /// etc. start routing into the "abandoned" CC from day 1. Three beta reporters flagged it
    /// (u/Tutorem, dm_me_your_kindness, khauser13): "townspeople entering the community center…
    /// confused me before I figured out why the schedules had changed."
    /// </para>
    ///
    /// <para>
    /// One flag was doing double duty. This postfix severs the two behaviors: for the
    /// "CommunityCenter" destination it always returns <c>true</c> (cancel the entry → villager uses
    /// their default schedule, never enters), regardless of the player-facing accessibility override.
    /// That reproduces vanilla's pre-CC-completion NPC behavior — exactly what a TLY run wants — while
    /// the door/warp stay open for the player. The caller (NPC.cs:5746) handles a <c>true</c> return
    /// by re-parsing the "default"/"spring" schedule, a clean vanilla path (no entry into the CC).
    /// </para>
    ///
    /// Dormant on non-TLY saves via <see cref="Core.RunActivation.IsActive"/>.
    /// </summary>
    [HarmonyPatch(typeof(NPC), "changeScheduleForLocationAccessibility")]
    internal static class NpcCcScheduleStayOutPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by the PatchAll loop in ModEntry.
        private static void Postfix(ref string locationName, ref bool __result)
        {
            if (!Core.RunActivation.IsActive)
                return; // dormant on non-TLY saves — leave vanilla scheduling untouched.
            if (locationName == "CommunityCenter")
                __result = true; // always cancel CC schedule entries → villagers stay out.
        }
    }
}
