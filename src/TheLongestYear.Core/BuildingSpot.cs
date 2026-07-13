namespace TheLongestYear.Core;

/// <summary>
/// Last-known top-left tile of a kept-building family's building on the Farm, captured before
/// every loop reset and persisted in <see cref="MetaState.KeptBuildingSpots"/> so the reset
/// rebuilds the kept building exactly where the player had it (2026-07-13 user ruling: never
/// relocate carefully placed buildings — clear the footprint instead). Keyed by chain family
/// ("coop"/"barn"/"silo"), so an upgraded building (e.g. Deluxe Coop) still anchors the spot
/// for the lower-tier keep that gets rebuilt.
/// </summary>
/// <param name="X">Building tile X on the Farm.</param>
/// <param name="Y">Building tile Y on the Farm.</param>
public sealed record BuildingSpot(int X, int Y);
