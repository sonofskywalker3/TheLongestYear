using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Where kept decor goes on the fresh farm (spec 2026-10-01, "Conflicts on the fresh farm").
/// A piece is displaced (to the stash, else dropped beside its blocker: Addendum 2) when a tile is off the map or under a building (kept buildings are
/// placed first and win), or, for an object-layer piece, when another object or furniture now
/// stands there (a path may run under an object). Large debris touching a still-placed piece is
/// cleared if the KEPT tool tier breaks it, else every piece it touches goes to the stash.
/// Unbreakable clumps are resolved first so a breakable clump is never cleared for a piece the
/// stash takes anyway. Large debris elsewhere is never touched. Small debris under placed pieces
/// is listed for clearing (no drops). An object-layer piece whose tile holds a fresh object with
/// the same item id (the starting fences a farm map spawns) is not displaced: the fresh one is
/// listed for swapping, so the kept piece is not duplicated into the stash every loop.
/// </summary>
public static class FarmDecorPlanner
{
    private const TileBlock AlwaysBlocks = TileBlock.OffMap | TileBlock.Building;
    private const TileBlock ObjectLayerBlocks = AlwaysBlocks | TileBlock.OtherObject | TileBlock.FreshObject;

    /// <param name="freshObjectIdAt">The qualified item id of the fresh object on a tile, or null.
    /// Only read for tiles that report FreshObject. Null when the caller has no ids (no swaps).</param>
    public static DecorPlan Plan(IReadOnlyList<DecorPiece> pieces, IReadOnlyList<DecorClump> clumps,
        Func<int, int, TileBlock> blockAt, int axeTier, int pickaxeTier,
        Func<int, int, string?>? freshObjectIdAt = null)
    {
        var displaced = new HashSet<int>();
        var blockedAt = new Dictionary<int, DecorTile>();
        var swaps = new Dictionary<int, List<DecorTile>>();
        foreach (DecorPiece piece in pieces)
        {
            TileBlock blocking = piece.Layer == DecorLayer.Object ? ObjectLayerBlocks : AlwaysBlocks;
            var pieceSwaps = new List<DecorTile>();
            foreach (DecorTile t in piece.Tiles)
            {
                TileBlock block = blockAt(t.X, t.Y);
                if (piece.Layer == DecorLayer.Object && IsSameObject(piece, t, block, freshObjectIdAt))
                {
                    block &= ~TileBlock.FreshObject;
                    pieceSwaps.Add(t);
                }
                if ((block & blocking) != 0)
                {
                    displaced.Add(piece.Id);
                    blockedAt[piece.Id] = t;
                    break;
                }
            }
            if (pieceSwaps.Count > 0)
                swaps[piece.Id] = pieceSwaps;
        }

        foreach (DecorClump clump in clumps.Where(c => !FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            foreach (DecorPiece piece in pieces)
                if (!displaced.Contains(piece.Id) && Overlaps(piece, clump))
                {
                    displaced.Add(piece.Id);
                    blockedAt[piece.Id] = piece.Tiles.First(t => clump.Tiles.Contains(t));
                }

        var cleared = new List<ClearedClump>();
        foreach (DecorClump clump in clumps.Where(c => FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            if (pieces.Any(p => !displaced.Contains(p.Id) && Overlaps(p, clump)))
                cleared.Add(new ClearedClump(clump.Id, FarmDecorKeep.RuleFor(clump.Index)!.HardwoodDrop));

        List<DecorPiece> placed = pieces.Where(p => !displaced.Contains(p.Id)).ToList();
        List<DecorTile> debris = placed.SelectMany(p => p.Tiles)
            .Where(t => (blockAt(t.X, t.Y) & TileBlock.SmallDebris) != 0)
            .Distinct()
            .ToList();

        List<DecorTile> swapTiles = placed
            .SelectMany(p => swaps.TryGetValue(p.Id, out List<DecorTile>? s) ? s : new List<DecorTile>())
            .Distinct()
            .ToList();

        return new DecorPlan(
            placed.Select(p => p.Id).ToList(),
            pieces.Where(p => displaced.Contains(p.Id)).Select(p => p.Id).ToList(),
            cleared,
            debris,
            swapTiles,
            blockedAt);
    }

    private static bool IsSameObject(DecorPiece piece, DecorTile t, TileBlock block, Func<int, int, string?>? freshObjectIdAt)
        => piece.ItemId != null
           && freshObjectIdAt != null
           && (block & TileBlock.FreshObject) != 0
           && string.Equals(freshObjectIdAt(t.X, t.Y), piece.ItemId, StringComparison.Ordinal);

    private static bool Overlaps(DecorPiece piece, DecorClump clump)
        => piece.Tiles.Any(t => clump.Tiles.Contains(t));
}
