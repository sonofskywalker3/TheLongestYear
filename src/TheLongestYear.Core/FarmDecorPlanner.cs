using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Where kept decor goes on the fresh farm (spec 2026-10-01, "Conflicts on the fresh farm").
/// A piece goes to the stash when a tile is off the map or under a building (kept buildings are
/// placed first and win), or, for an object-layer piece, when another object or furniture now
/// stands there (a path may run under an object). Large debris touching a still-placed piece is
/// cleared if the KEPT tool tier breaks it, else every piece it touches goes to the stash.
/// Unbreakable clumps are resolved first so a breakable clump is never cleared for a piece the
/// stash takes anyway. Large debris elsewhere is never touched. Small debris under placed pieces
/// is listed for clearing (no drops).
/// </summary>
public static class FarmDecorPlanner
{
    private const TileBlock AlwaysBlocks = TileBlock.OffMap | TileBlock.Building;
    private const TileBlock ObjectLayerBlocks = AlwaysBlocks | TileBlock.OtherObject;

    public static DecorPlan Plan(IReadOnlyList<DecorPiece> pieces, IReadOnlyList<DecorClump> clumps,
        Func<int, int, TileBlock> blockAt, int axeTier, int pickaxeTier)
    {
        var displaced = new HashSet<int>();
        foreach (DecorPiece piece in pieces)
        {
            TileBlock blocking = piece.Layer == DecorLayer.Object ? ObjectLayerBlocks : AlwaysBlocks;
            if (piece.Tiles.Any(t => (blockAt(t.X, t.Y) & blocking) != 0))
                displaced.Add(piece.Id);
        }

        foreach (DecorClump clump in clumps.Where(c => !FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            foreach (DecorPiece piece in pieces)
                if (!displaced.Contains(piece.Id) && Overlaps(piece, clump))
                    displaced.Add(piece.Id);

        var cleared = new List<ClearedClump>();
        foreach (DecorClump clump in clumps.Where(c => FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            if (pieces.Any(p => !displaced.Contains(p.Id) && Overlaps(p, clump)))
                cleared.Add(new ClearedClump(clump.Id, FarmDecorKeep.RuleFor(clump.Index)!.HardwoodDrop));

        List<DecorPiece> placed = pieces.Where(p => !displaced.Contains(p.Id)).ToList();
        List<DecorTile> debris = placed.SelectMany(p => p.Tiles)
            .Where(t => (blockAt(t.X, t.Y) & TileBlock.SmallDebris) != 0)
            .Distinct()
            .ToList();

        return new DecorPlan(
            placed.Select(p => p.Id).ToList(),
            pieces.Where(p => displaced.Contains(p.Id)).Select(p => p.Id).ToList(),
            cleared,
            debris);
    }

    private static bool Overlaps(DecorPiece piece, DecorClump clump)
        => piece.Tiles.Any(t => clump.Tiles.Contains(t));
}
