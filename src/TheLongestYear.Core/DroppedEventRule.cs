namespace TheLongestYear.Core;

/// <summary>What to put right on a morning after the night dropped an event.
///
/// An event that starts while the day is ending never finishes: Game1._newDayAfterFade clears every
/// map's currentEvent (Game1.cs about 7480) but leaves Game1.eventUp raised and the HUD hidden, the
/// two things GameLocation.startEvent set. The game lowers the flag again only in its input pass,
/// which needs the window focused and nothing else in the way, and it never shows the HUD again.
/// It happens when passing out warps the farmer into a map with an event that has no time of day
/// (East Scarp's inn, the wake-up place for its maps: Nexus posts, Celjhyn, 2026-11-07). The morning
/// screens wait on the event flag and the mod draws on the HUD, so the player woke to neither.</summary>
public static class DroppedEventRule
{
    /// <param name="startedAsDayEnded">An event was running on the farmer's map while the day was ending.</param>
    /// <param name="eventNow">An event is running now (the morning started one of its own, or the same one survived).</param>
    /// <param name="busy">A farm event, a map change in progress or an event still closing: not ours to judge.</param>
    public static DroppedEventRepair Decide(bool startedAsDayEnded, bool eventNow, bool busy, bool eventFlagUp, bool hudShown)
        => !startedAsDayEnded || eventNow || busy
            ? DroppedEventRepair.None
            : new DroppedEventRepair(LowerEventFlag: eventFlagUp, ShowHud: !hudShown);
}

public readonly record struct DroppedEventRepair(bool LowerEventFlag, bool ShowHud)
{
    public static readonly DroppedEventRepair None = new(false, false);

    public bool Any => LowerEventFlag || ShowHud;
}
