using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Keep Farmhouse Furniture (spec 2026-10-01, Addendum 2). <see cref="Capture"/> lifts every
    /// furniture piece out of the farmhouse and its cellar just before loadForNewGame (step 0h), wiping
    /// non-cosmetic contents (FarmhouseFurnitureKeep). <see cref="Restore"/> runs after the rebuilt house
    /// has its starter furniture (step 14b): the starter set goes, each kept piece goes back on its tile
    /// and rotation when the vanilla placement check allows it, and the rest drops as a pickup inside
    /// the house by the front door. The same instance moves, so rotation, colour, a lamp's state and
    /// cosmetic contents all survive. Nothing escapes: each piece is guarded on its own.</summary>
    internal static class FarmhouseFurnitureCarryover
    {
        internal sealed class Entry
        {
            public Furniture Piece;
            public Vector2 Tile;
            public bool FromCellar;
        }

        internal sealed class Snapshot
        {
            public readonly List<Entry> Entries = new();
            public int WipedContents;
            /// <summary>The old house's upgrade level: house tiles shift by level like a vanilla upgrade.</summary>
            public int HouseLevel;
        }

        private const int CellarHouseLevel = 3;
        private static readonly Vector2 FallbackStarterBedTile = new(9f, 8f);

        // The player stands in the house during the reset; a piece placed under him is fine
        // (Furniture.OnAdded gives him temporary passable tiles), so farmers and characters do
        // not block a kept piece.
        private const CollisionMask PlacementMask = CollisionMask.All & ~(CollisionMask.Farmers | CollisionMask.Characters);

        public static Snapshot Capture(IMonitor monitor)
        {
            var snap = new Snapshot();
            if (Utility.getHomeOfFarmer(Game1.player) is not FarmHouse house)
            {
                monitor.Log("Keep Farmhouse Furniture: no farmhouse before the rewind; nothing kept.", LogLevel.Warn);
                return snap;
            }
            int failed = 0;
            try
            {
                snap.HouseLevel = house.upgradeLevel;
                failed += LiftFrom(house, fromCellar: false, snap, monitor);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's location hook. What was lifted so far is kept.
                monitor.Log($"Keep Farmhouse Furniture: lifting from the house failed partway; keeping the {snap.Entries.Count} piece(s) lifted so far. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }
            try
            {
                GameLocation cellar = FindCellar(house, monitor);
                if (cellar != null)
                    failed += LiftFrom(cellar, fromCellar: true, snap, monitor);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's location hook. What was lifted so far is kept.
                monitor.Log($"Keep Farmhouse Furniture: lifting from the cellar failed partway; keeping what was lifted. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }

            monitor.Log($"Keep Farmhouse Furniture: lifted {snap.Entries.Count} piece(s) out of the house before the rewind, " +
                        $"wiped {snap.WipedContents} non-cosmetic item(s) inside them" +
                        (failed > 0 ? $"; {failed} could not be lifted and stay with the old house." : "."),
                        failed > 0 ? LogLevel.Warn : LogLevel.Info);
            return snap;
        }

        private static int LiftFrom(GameLocation location, bool fromCellar, Snapshot snap, IMonitor monitor)
        {
            int failed = 0;
            foreach (Furniture piece in location.furniture.ToList())
            {
                if (piece == null) continue;
                Vector2 tile = Vector2.Zero;
                bool removed = false;
                try
                {
                    if (!FarmhouseFurnitureKeep.IsKeptPiece(piece.QualifiedItemId)) continue;
                    tile = piece.TileLocation;
                    location.furniture.Remove(piece);
                    removed = true;
                }
                catch (System.Exception ex)
                {
                    // Exception on purpose: a mod's item or collection hook. Kept when it is off the old house anyway.
                    removed = !location.furniture.Contains(piece);
                    if (!removed)
                    {
                        failed++;
                        monitor.Log($"Keep Farmhouse Furniture: lifting a piece at ({tile.X}, {tile.Y}) threw; it stays with the old house. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                    }
                }
                if (!removed) continue;
                snap.WipedContents += WipeNonCosmetic(piece, monitor);
                snap.Entries.Add(new Entry { Piece = piece, Tile = tile, FromCellar = fromCellar });
            }
            return failed;
        }

        /// <summary>Remove what the stash rule would refuse: a table's held item, a dresser's or a
        /// tank's non-cosmetic slots (fish included). Cosmetic contents stay and are walked in turn.
        /// Returns how many items were wiped. Never throws.</summary>
        internal static int WipeNonCosmetic(Furniture piece, IMonitor monitor)
        {
            int wiped = 0;
            try
            {
                StardewValley.Object held = piece.heldObject.Value;
                if (held != null)
                {
                    if (!FarmhouseFurnitureKeep.KeepsContent(held.QualifiedItemId))
                    {
                        piece.heldObject.Value = null;
                        wiped++;
                    }
                    else if (held is Furniture heldPiece)
                        wiped += WipeNonCosmetic(heldPiece, monitor);
                }
                if (piece is StorageFurniture storage)
                {
                    for (int i = storage.heldItems.Count - 1; i >= 0; i--)
                    {
                        Item item = storage.heldItems[i];
                        if (item == null) continue;
                        if (!FarmhouseFurnitureKeep.KeepsContent(item.QualifiedItemId))
                        {
                            storage.heldItems.RemoveAt(i);
                            wiped++;
                        }
                        else if (item is Furniture inner)
                            wiped += WipeNonCosmetic(inner, monitor);
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's furniture subclass. The piece is still kept.
                monitor.Log($"Keep Farmhouse Furniture: emptying '{piece.QualifiedItemId}' threw; kept as it is. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
            }
            return wiped;
        }

        public static void Restore(Snapshot snap, GameLocation home, IMonitor monitor)
        {
            if (snap == null) return;
            if (home is not FarmHouse house)
            {
                monitor.Log($"Keep Farmhouse Furniture: no farmhouse after the rewind; {snap.Entries.Count} piece(s) not restored.", LogLevel.Error);
                return;
            }
            Vector2 door = DoorTile(house, monitor);
            var handled = new HashSet<Entry>();
            var unplaced = new List<Entry>();
            // The kept set replaces the starter set (no second bed or table). The starter bed is held
            // back in case the house would otherwise end without a bed.
            BedFurniture starterBed = null;
            try
            {
                starterBed = house.furniture.OfType<BedFurniture>().FirstOrDefault();
                RestoreInto(house, snap, starterBed, handled, unplaced, monitor);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: the restore runs inside the reset, and a throw can come from any mod.
                List<Entry> left = snap.Entries.Where(e => !handled.Contains(e)).ToList();
                monitor.Log($"Keep Farmhouse Furniture: the restore failed partway; dropping the {left.Count} piece(s) not yet placed by the front door.\n{ex}", LogLevel.Error);
                unplaced.AddRange(left.Where(e => !house.furniture.Contains(e.Piece)));
                EnsureStarterBed(house, starterBed, monitor);
            }

            int dropped = 0;
            foreach (Entry e in unplaced)
                if (GroundDrop.Near(house, door, e.Piece, monitor, $"Keep Farmhouse Furniture piece for ({e.Tile.X}, {e.Tile.Y}) had no room"))
                    dropped++;
            int keptUnplaced = unplaced.Count(snap.Entries.Contains);
            monitor.Log($"Keep Farmhouse Furniture: placed {snap.Entries.Count - keptUnplaced} of {snap.Entries.Count} kept piece(s), " +
                        $"dropped {dropped} by the front door at ({door.X}, {door.Y})" +
                        (dropped < unplaced.Count ? $", {unplaced.Count - dropped} could not be dropped (see errors)." : "."), LogLevel.Info);
            if (!HasSleepableBed(house))
                monitor.Log("Keep Farmhouse Furniture: the house ends the rewind with no bed the player can sleep in; " +
                            "a bed dropped by the front door must be placed before sleeping.", LogLevel.Error);
        }

        private static void RestoreInto(FarmHouse house, Snapshot snap, BedFurniture starterBed, HashSet<Entry> handled, List<Entry> unplaced, IMonitor monitor)
        {
            int starterCount = house.furniture.Count;
            house.furniture.Clear();
            monitor.Log($"Keep Farmhouse Furniture: removed {starterCount} starter piece(s).", LogLevel.Trace);

            // House tiles move with the house level the way a vanilla upgrade moves them
            // (FarmHouse.moveObjectsForHouseUpgrade); the cellar's map never changes.
            int level = house.upgradeLevel;
            (int dx, int dy) = FarmhouseFurnitureKeep.TileShift(snap.HouseLevel, level);
            if (dx != 0 || dy != 0)
            {
                foreach (Entry e in snap.Entries.Where(e => !e.FromCellar))
                    e.Tile += new Vector2(dx, dy);
                monitor.Log($"Keep Farmhouse Furniture: house level {snap.HouseLevel} to {level}; house tiles shift by ({dx}, {dy}).", LogLevel.Info);
            }

            GameLocation cellar = level >= CellarHouseLevel ? FindCellar(house, monitor) : null;
            IReadOnlyList<int> order = FarmhouseFurnitureKeep.PlacementOrder(
                snap.Entries.Select(e => e.Piece.furniture_type.Value == Furniture.rug).ToList());
            foreach (int index in order)
            {
                Entry e = snap.Entries[index];
                handled.Add(e);
                GameLocation target = e.FromCellar ? cellar : house;
                if (!TryPlace(target, e, house, monitor))
                    unplaced.Add(e);
            }

            Entry keptBed = unplaced.FirstOrDefault(e => !e.FromCellar && e.Piece is BedFurniture bed
                && Kind(bed) is BedKind kind && FarmhouseFurnitureKeep.IsStarterSpotCandidate(kind, level));
            BedFallback fallback = FarmhouseFurnitureKeep.ForBed(house.GetPlayerBed() != null, HasSleepableBed(house), keptBed != null);
            Vector2 bedTile = starterBed?.TileLocation ?? FallbackStarterBedTile;
            if (fallback == BedFallback.KeptBedAtStarterSpot)
            {
                Vector2 was = keptBed.Tile;
                keptBed.Tile = bedTile;
                if (TryPlace(house, keptBed, house, monitor))
                {
                    unplaced.Remove(keptBed);
                    monitor.Log($"Keep Farmhouse Furniture: the kept bed had no room at ({was.X}, {was.Y}); it went on the starter bed's spot.", LogLevel.Info);
                    return;
                }
                keptBed.Tile = was;
                fallback = FarmhouseFurnitureKeep.ForBed(house.GetPlayerBed() != null, HasSleepableBed(house), legalKeptBedUnplaced: false);
            }
            if (fallback == BedFallback.StarterBed && starterBed != null)
            {
                var starter = new Entry { Piece = starterBed, Tile = bedTile };
                if (TryPlace(house, starter, house, monitor))
                    monitor.Log("Keep Farmhouse Furniture: no kept bed fit, so the starter bed stays.", LogLevel.Info);
                else
                    unplaced.Add(starter);
            }
        }

        // After a failed restore: the starter bed goes back whenever the house has no bed to sleep in.
        // Never throws.
        private static void EnsureStarterBed(FarmHouse house, BedFurniture starterBed, IMonitor monitor)
        {
            try
            {
                if (starterBed == null || house.furniture.Contains(starterBed)) return;
                BedFallback fallback = FarmhouseFurnitureKeep.ForBed(house.GetPlayerBed() != null, HasSleepableBed(house), legalKeptBedUnplaced: false);
                if (fallback != BedFallback.StarterBed) return;
                house.furniture.Add(starterBed);
                monitor.Log("Keep Farmhouse Furniture: put the starter bed back after the failed restore.", LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's collection hook. The no-bed error below still reports it.
                monitor.Log($"Keep Farmhouse Furniture: putting the starter bed back threw. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }
        }

        // An adult bed is in the house (a child bed is not the player's).
        private static bool HasSleepableBed(FarmHouse house)
            => house.furniture.OfType<BedFurniture>().Any(b => Kind(b) is BedKind kind && kind != BedKind.Child);

        private static BedKind? Kind(BedFurniture bed)
            => System.Enum.TryParse(bed.bedType.ToString(), out BedKind kind) ? kind : null;

        // Place one piece on its own tile when its room exists and the vanilla check allows it. True
        // when it is in the room. Never throws.
        private static bool TryPlace(GameLocation target, Entry e, FarmHouse house, IMonitor monitor)
        {
            try
            {
                bool fits = target != null && Fits(target, e);
                if (FarmhouseFurnitureKeep.Decide(target != null, fits) != HouseFurnitureOutcome.Place)
                    return false;
                target.furniture.Add(e.Piece);
                if (target == house && Game1.currentLocation == house)
                    e.Piece.actionOnPlayerEntryOrPlacement(house, dropDown: false);
                return true;
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's furniture or collection hook.
                bool landed = target != null && target.furniture.Contains(e.Piece);
                monitor.Log($"Keep Farmhouse Furniture: placing a piece at ({e.Tile.X}, {e.Tile.Y}) threw; " +
                            (landed ? "it is in the house anyway." : "dropping it by the front door.") +
                            $" {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return landed;
            }
        }

        /// <summary>The vanilla placement check at the piece's tile and rotation, plus vanilla's bed
        /// rule from BedFurniture.placementAction (a double bed needs house level 1, a child bed
        /// level 2), which canBePlacedHere does not check. A wall piece the check would slide to
        /// another wall row does not fit (it would not be where the player put it).</summary>
        internal static bool Fits(GameLocation target, Entry e)
        {
            if (e.Piece is BedFurniture bed && target is FarmHouse house
                && Kind(bed) is BedKind kind && !FarmhouseFurnitureKeep.IsBedLegal(kind, house.upgradeLevel))
                return false;
            if (e.Piece.TileLocation != e.Tile)
                e.Piece.TileLocation = e.Tile;
            bool ok = e.Piece.canBePlacedHere(target, e.Tile, PlacementMask, showError: false);
            bool stayed = e.Piece.TileLocation == e.Tile;
            if (!stayed)
                e.Piece.TileLocation = e.Tile;
            return ok && stayed;
        }

        // The tile just inside the front door, where the player walks in. Never throws.
        internal static Vector2 DoorTile(FarmHouse house, IMonitor monitor)
        {
            try
            {
                Point entry = house.getEntryLocation();
                return new Vector2(entry.X, entry.Y);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's map property. The bed spot fallback is inside the house too.
                monitor.Log($"Keep Farmhouse Furniture: reading the house entry threw; using the starter bed's spot. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return FallbackStarterBedTile + new Vector2(0f, 1f);
            }
        }

        // The farmhouse's cellar when one exists (FarmHouse.GetCellar throws when the location is
        // missing, so look it up by name). Never throws.
        internal static GameLocation FindCellar(FarmHouse house, IMonitor monitor)
        {
            try
            {
                string name = house.GetCellarName();
                return name != null ? Game1.getLocationFromName(name) : null;
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: cellar assignments can be in any state mid-reset.
                monitor.Log($"Keep Farmhouse Furniture: looking up the cellar threw; skipping it. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return null;
            }
        }
    }
}
