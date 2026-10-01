using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What kind of thing stands on a farm tile, as the game-bound snapshot classifies it.</summary>
public enum FarmThingKind { Flooring, Fence, Torch, Sign, Furniture, BigCraftable, Object }

/// <summary>Ground = a path or floor (terrain feature); Object = objects and furniture.</summary>
public enum DecorLayer { Ground, Object }

/// <summary>Why a tile cannot take decor on the fresh farm.</summary>
[System.Flags]
public enum TileBlock { None = 0, OffMap = 1, Building = 2, OtherObject = 4, SmallDebris = 8 }

/// <summary>The tool that breaks a large debris clump.</summary>
public enum DecorTool { Axe, Pickaxe }

/// <summary>One farm tile.</summary>
public readonly record struct DecorTile(int X, int Y);

/// <summary>A kept decor piece (one object, or one multi-tile furniture) and the tiles it covers.</summary>
public sealed record DecorPiece(int Id, DecorLayer Layer, IReadOnlyList<DecorTile> Tiles);

/// <summary>A large debris clump on the fresh farm: its vanilla index and the tiles it covers.</summary>
public sealed record DecorClump(int Id, int Index, IReadOnlyList<DecorTile> Tiles);

/// <summary>The tool, minimum tier and hardwood drop for breaking one kind of clump.</summary>
public sealed record ClumpRule(DecorTool Tool, int MinTier, int HardwoodDrop);

/// <summary>A clump the plan clears and the hardwood it drops on its own tiles.</summary>
public sealed record ClearedClump(int ClumpId, int HardwoodDrop);

/// <summary>Placed piece ids, displaced piece ids (stash or overflow chest), clumps to clear, small debris tiles to clear.</summary>
public sealed record DecorPlan(
    IReadOnlyList<int> Placed,
    IReadOnlyList<int> Displaced,
    IReadOnlyList<ClearedClump> ClearedClumps,
    IReadOnlyList<DecorTile> DebrisTilesToClear);
