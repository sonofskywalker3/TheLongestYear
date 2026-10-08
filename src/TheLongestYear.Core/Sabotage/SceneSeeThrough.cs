using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>A box of tiles, edges included.</summary>
public readonly record struct TileBox(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
}

/// <summary>What goes see-through in the thief scene (designer, 2026-10-08: the chest was hidden
/// behind a tree, so he never saw it). Anything tall drawn in front of the chest, its lid or the
/// thief's path fades the way vanilla fades a tree when the farmer stands behind it. The boxes are
/// vanilla's own fade regions, read off the 1.6 decompile and put in tiles: a thing fades when one
/// of the watched tiles falls inside its box. Pure tile arithmetic, no game types.</summary>
public static class SceneSeeThrough
{
    /// <summary>A figure in a scene stands on his tile and his head is in the tile above (the
    /// Brute's sheet is two tiles tall).</summary>
    public const int FigureTilesHigh = 2;

    /// <summary>The chest's lid is drawn a tile above the chest (Chest.draw).</summary>
    public const int LidTilesAbove = 1;

    /// <summary>A grown tree (Tree.tickUpdate: growth stage 5 or more, standing): the canopy over
    /// the three columns around its trunk and five rows above it.</summary>
    public static TileBox Tree(int x, int y) => new(x - 1, y - 5, x + 1, y - 1);

    /// <summary>A grown fruit tree (FruitTree.tickUpdate: growth stage 4 or more): three columns,
    /// four rows above the trunk.</summary>
    public static TileBox FruitTree(int x, int y) => new(x - 1, y - 4, x + 1, y - 1);

    /// <summary>A bush (no vanilla fade; Bush.draw): it is drawn one tile taller than its footprint,
    /// <paramref name="extraTilesWide"/> + 1 tiles wide, so it covers the row above it.</summary>
    public static TileBox Bush(int x, int y, int extraTilesWide) => new(x, y - 1, x + Math.Max(0, extraTilesWide), y - 1);

    /// <summary>A building (Building.UpdateTransparency): the part of its sprite drawn above its
    /// footprint. <paramref name="spriteTilesHigh"/> is the sprite's height in tiles.</summary>
    public static TileBox Building(int tileX, int tileY, int tilesWide, int tilesHigh, int spriteTilesHigh)
        => new(tileX, tileY + tilesHigh - spriteTilesHigh, tileX + tilesWide - 1, tileY - 1);

    /// <summary>The tiles that must stay visible: the target, its lid, and every tile of the walk
    /// with the figure's head above it.</summary>
    public static HashSet<(int X, int Y)> Watched((int X, int Y) target, IEnumerable<(int X, int Y)>? walk)
    {
        var tiles = new HashSet<(int X, int Y)>();
        for (int up = 0; up <= LidTilesAbove; up++) tiles.Add((target.X, target.Y - up));
        if (walk != null)
            foreach ((int X, int Y) step in walk)
                for (int up = 0; up < FigureTilesHigh; up++) tiles.Add((step.X, step.Y - up));
        return tiles;
    }

    /// <summary>Does this box hide any watched tile?</summary>
    public static bool Hides(TileBox box, IReadOnlyCollection<(int X, int Y)> watched)
    {
        if (watched is null) throw new ArgumentNullException(nameof(watched));
        foreach ((int X, int Y) t in watched)
            if (box.Contains(t.X, t.Y)) return true;
        return false;
    }

    /// <summary>The faded opacity, vanilla's own (a tree behind which the farmer stands).</summary>
    public const float FadedAlpha = 0.4f;

    /// <summary>How fast a thing fades in a tick: vanilla's tree step (0.09) net of the 0.05 its own
    /// update adds back each tick.</summary>
    public const float FadeStep = 0.09f;

    /// <summary>One tick of fading toward <see cref="FadedAlpha"/>.</summary>
    public static float FadeToward(float alpha) => Math.Max(FadedAlpha, alpha - FadeStep);
}
