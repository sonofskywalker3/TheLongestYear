using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What the night pass does with a freshly picked strike.</summary>
public enum StrikePickAction
{
    /// <summary>Its kind's scene already played this loop: it lands now, with no scene by design.</summary>
    LandNoSceneByDesign,

    /// <summary>Its scene is due and can show the pick: it waits for the overnight slot.</summary>
    WaitForScene,

    /// <summary>Its scene is due but cannot stage or cannot show the pick (the system is broken):
    /// it lands now, with no scene, and the scene stays due.</summary>
    LandBare,
}

/// <summary>Conflict versus broken (Jeff, 2026-10-08): "If there's a CONFLICT and a different scene
/// runs, we push back one day. If the SYSTEM is broken and a scene CAN'T run ever, then they miss
/// out on the cool scene, but still get hit." A conflict is another event owning the overnight
/// slot (<see cref="StrikeSlot.Decide"/>), or another mod replacing our scene before its setUp: the
/// strike is postponed and queued (<see cref="StrikeQueue"/>). A broken scene is one that cannot
/// stage (checked at the pick, or found in setUp): the strike lands now, bare, recorded like any
/// landed strike, and is never queued. A scene that cannot stage never stops its kind from acting.</summary>
public static class StrikeStaging
{
    /// <param name="sceneDue">The kind's scene has not played this loop.</param>
    /// <param name="sceneCanStage">Can the kind's scene stage tonight at all (its map, its
    /// texture)? Asked only while the scene is due.</param>
    /// <param name="sceneCanShowPick">Can it show tonight's pick? Asked only when it can stage.</param>
    public static StrikePickAction AtPick(bool sceneDue, Func<bool> sceneCanStage, Func<bool> sceneCanShowPick)
    {
        if (sceneCanStage is null) throw new ArgumentNullException(nameof(sceneCanStage));
        if (sceneCanShowPick is null) throw new ArgumentNullException(nameof(sceneCanShowPick));
        if (!sceneDue) return StrikePickAction.LandNoSceneByDesign;
        return sceneCanStage() && sceneCanShowPick() ? StrikePickAction.WaitForScene : StrikePickAction.LandBare;
    }

    /// <summary>Why the net postpones a strike: a replaced scene is a conflict (queued); a scene
    /// never handed the slot is not (a fail night, a night with no pickFarmEvent).</summary>
    public static PostponeCause CauseAtNet(StrikeNetAction action) => action switch
    {
        StrikeNetAction.Replaced => PostponeCause.SlotTaken,
        StrikeNetAction.Postpone => PostponeCause.NeverStaged,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "only a postponing net action has a cause"),
    };
}
