using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Shane's walk past the Community Center in the hall scene, in Town tiles (Jeff,
/// 2026-09-23 and 2026-09-25). He has left the Saloon and taken the long way round to clear his
/// head: up the riverside path east of the hall and over the wooden bridge, then west along the dirt
/// road that runs below the hall. Level with the hall he sees the lit windows, stops dead, jumps,
/// backs off down the road, and then hurries home: down the dirt path that runs south west to the
/// square, which is his way back through town to Marnie's ranch, where he lives.
///
/// He NEVER walks up the cobbled approach to the door (52..53, 21..23) or onto the lawn.
///
/// EVERY TILE WAS READ OFF THE MAP, NOT GUESSED. The corners below were chosen on Town's own Back
/// layer (exported with <c>patch export Maps/Town</c>): each tile they expand to has a Back tile
/// whose <c>Type</c> is <c>Dirt</c> or <c>Wood</c> (the bridge), no Buildings tile and no Paths
/// layer object (the trees and bushes Town spawns from that layer). <c>HallRouteTests</c> holds the
/// same export as a mask and checks every tile against it, and the scene checks every tile again
/// against the live map before it uses the route.</summary>
public static class HallRoute
{
    /// <summary>Up from the river over the wooden bridge (column 75, rows 34 to 30), onto the dirt
    /// road, and west along its row 28 to the spot below the hall's right hand window.</summary>
    public static readonly IReadOnlyList<(int X, int Y)> WayInCorners = new[]
    {
        (75, 34), (75, 29), (74, 29), (74, 28), (57, 28),
    };

    /// <summary>Two tiles straight back down the road, still facing the hall.</summary>
    public static readonly IReadOnlyList<(int X, int Y)> BackAwayCorners = new[]
    {
        (57, 28), (57, 30),
    };

    /// <summary>Home: down the road's south west arm and along the dirt path to the one that runs
    /// south into the square.</summary>
    public static readonly IReadOnlyList<(int X, int Y)> WayOutCorners = new[]
    {
        (57, 30), (57, 33), (56, 33), (56, 34), (55, 34), (55, 35), (54, 35), (54, 36), (49, 36), (49, 42),
    };

    /// <summary>Every tile of the way in, the bridge first, ending where he stops.</summary>
    public static IReadOnlyList<(int X, int Y)> WayIn { get; } = TileRoute.Expand(WayInCorners);

    /// <summary>Every tile of the back away, from where he stopped.</summary>
    public static IReadOnlyList<(int X, int Y)> BackAway { get; } = TileRoute.Expand(BackAwayCorners);

    /// <summary>Every tile of the way home, from where he backed away to.</summary>
    public static IReadOnlyList<(int X, int Y)> WayOut { get; } = TileRoute.Expand(WayOutCorners);

    /// <summary>How far round the whole tiles of the frame still count as in shot when cutting
    /// the way home. The frame is whole tiles only, so the row its edge cuts through is partly
    /// visible, and a man standing on the row below the frame still shows his head in it (he is
    /// two tiles tall). Two tiles all round covers both.</summary>
    public const int ShotMarginTiles = 2;

    /// <summary>How many tiles of the way home are kept beyond the last one in shot.</summary>
    public const int OutOfShotTiles = 3;

    /// <summary>The part of <paramref name="wayOut"/> to walk for a camera showing these whole
    /// tiles: up to where he is wholly out of shot and a few tiles beyond, or ALL of it when it never
    /// leaves the shot (a very tall or wide screen). Every tile returned is a tile of
    /// <paramref name="wayOut"/>, and played back with <see cref="TileRoute.At"/> he stops on its
    /// last tile rather than walking on past it.</summary>
    public static IReadOnlyList<(int X, int Y)> WayOutInShot(
        IReadOnlyList<(int X, int Y)> wayOut, int frameLeft, int frameTop, int frameWidth, int frameHeight)
        => SceneRoute.OutOfFrame(
            wayOut,
            frameLeft - ShotMarginTiles,
            frameTop - ShotMarginTiles,
            frameWidth + 2 * ShotMarginTiles,
            frameHeight + 2 * ShotMarginTiles,
            OutOfShotTiles);

    /// <summary>Where he stops, sees the windows and jumps.</summary>
    public static (int X, int Y) Stop => WayIn[WayIn.Count - 1];
}
