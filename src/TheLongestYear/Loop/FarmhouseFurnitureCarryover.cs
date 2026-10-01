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
            int failed = LiftFrom(house, fromCellar: false, snap, monitor);
            GameLocation cellar = FindCellar(house, monitor);
            if (cellar != null)
                failed += LiftFrom(cellar, fromCellar: true, snap, monitor);

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
                if (piece == null || !FarmhouseFurnitureKeep.IsKeptPiece(piece.QualifiedItemId)) continue;
                var tile = piece.TileLocation;
                try
                {
                    location.furniture.Remove(piece);
                }
                catch (System.Exception ex)
                {
                    // Exception on purpose: a mod's collection hook. Kept when it is off the old house anyway.
                    if (location.furniture.Contains(piece))
                    {
                        failed++;
                        monitor.Log($"Keep Farmhouse Furniture: lifting '{piece.QualifiedItemId}' at ({tile.X}, {tile.Y}) threw; it stays with the old house. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                        continue;
                    }
                }
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
            try
            {
                RestoreInto(house, snap, door, handled, unplaced, monitor);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: the restore runs inside the reset, and a throw can come from any mod.
                List<Entry> left = snap.Entries.Where(e => !handled.Contains(e)).ToList();
                monitor.Log($"Keep Farmhouse Furniture: the restore failed partway; dropping the {left.Count} piece(s) not yet placed by the front door.\n{ex}", LogLevel.Error);
                unplaced.AddRange(left.Where(e => !house.furniture.Contains(e.Piece)));
            }

            int dropped = 0;
            foreach (Entry e in unplaced)
                if (GroundDrop.Near(house, door, e.Piece, monitor, $"Keep Farmhouse Furniture piece from ({e.Tile.X}, {e.Tile.Y}) had no room"))
                    dropped++;
            int keptUnplaced = unplaced.Count(snap.Entries.Contains);
            monitor.Log($"Keep Farmhouse Furniture: placed {snap.Entries.Count - keptUnplaced} of {snap.Entries.Count} kept piece(s), " +
                        $"dropped {dropped} by the front door at ({door.X}, {door.Y})" +
                        (dropped < unplaced.Count ? $", {unplaced.Count - dropped} could not be dropped (see errors)." : "."), LogLevel.Info);
        }

        private static void RestoreInto(FarmHouse house, Snapshot snap, Vector2 door, HashSet<Entry> handled, List<Entry> unplaced, IMonitor monitor)
        {
            // The kept set replaces the starter set (no second bed or table). The starter bed is held
            // back in case the house would otherwise end without a bed.
            BedFurniture starterBed = house.furniture.OfType<BedFurniture>().FirstOrDefault();
            int starterCount = house.furniture.Count;
            house.furniture.Clear();
            monitor.Log($"Keep Farmhouse Furniture: removed {starterCount} starter piece(s).", LogLevel.Trace);

            GameLocation cellar = house.upgradeLevel >= CellarHouseLevel ? FindCellar(house, monitor) : null;
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

            bool bedPlaced = house.furniture.Any(f => f is BedFurniture);
            Entry keptBed = unplaced.FirstOrDefault(e => e.Piece is BedFurniture && !e.FromCellar);
            BedFallback fallback = FarmhouseFurnitureKeep.ForBed(bedPlaced, keptBed != null);
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
                fallback = BedFallback.StarterBed;
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
                monitor.Log($"Keep Farmhouse Furniture: placing '{e.Piece.QualifiedItemId}' at ({e.Tile.X}, {e.Tile.Y}) threw; " +
                            (landed ? "it is in the house anyway." : "dropping it by the front door.") +
                            $" {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return landed;
            }
        }

        /// <summary>The vanilla placement check at the piece's own tile and rotation. A wall piece the
        /// check would slide to another wall row does not fit (it would not be where the player put it).</summary>
        internal static bool Fits(GameLocation target, Entry e)
        {
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
