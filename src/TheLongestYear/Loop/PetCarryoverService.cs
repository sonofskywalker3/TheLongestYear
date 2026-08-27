using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Pre-reset capture + post-reset restore of the player's pets, gated on the
    /// <c>keep_pet</c> upgrade. Pure helper class — no state of its own, just two static
    /// methods called by <see cref="WorldResetService.PerformReset"/>.
    ///
    /// 2026-05-29 spec: pets survive loop resets WITH their accumulated friendship hearts.
    /// Barn/coop animals do NOT — they're rebuilt fresh by ApplyStartingAnimals (0 hearts
    /// per user direction "the 'keep 1 cow' should still start over with 0 hearts so they
    /// can't be getting large milk day 1"). Keep these two paths visually separate.
    ///
    /// Lifecycle:
    /// <list type="number">
    ///   <item><see cref="SnapshotPet"/>: call BEFORE <c>loadForNewGame</c>. Finds every
    ///         pet via <c>Utility.getAllPets()</c>, captures kind/breed/name/friendship of
    ///         each into <c>MetaState.PetStates</c>. Bails silently if the upgrade isn't
    ///         owned.</item>
    ///   <item><see cref="RestorePet"/> — call AFTER <c>loadForNewGame</c> (and after the
    ///         starting-animals placement so the farm is settled). Re-creates each
    ///         <see cref="Pet"/> from its snapshot and drops it on the farm porch, staggered
    ///         so they don't stack, then gives every restored pet a bowl of its own
    ///         (<see cref="EnsureBowls"/>).
    ///         Also sets the <c>MarniePetAdoption</c> mail flag so vanilla's day-1 pet-
    ///         adoption offer doesn't fire on top of the restored pet(s).</item>
    /// </list>
    /// </summary>
    internal static class PetCarryoverService
    {
        public const string UpgradeId = "keep_pet";

        /// <summary>Capture every pet on the farm into MetaState.PetStates. Idempotent -
        /// overwrites any prior snapshot. No-op when the upgrade isn't owned (so a player
        /// without keep_pet doesn't accidentally bank stale pets between toggles).</summary>
        public static void SnapshotPet(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;
            PetCarryover.MigrateLegacy(meta);

            var pets = Utility.getAllPets();
            meta.PetStates.Clear();
            if (pets == null || pets.Count == 0)
            {
                monitor?.Log("PetCarryover: no pets on the farm; snapshot cleared.", LogLevel.Info);
                return;
            }
            foreach (Pet pet in pets)
            {
                meta.PetStates.Add(new PetSnapshot(
                    PetType:    pet.petType?.Value   ?? "Cat",
                    WhichBreed: pet.whichBreed?.Value ?? "0",
                    Name:       pet.Name             ?? "Pet",
                    Friendship: pet.friendshipTowardFarmer?.Value ?? 0));
            }
            monitor?.Log(
                $"PetCarryover: snapshot {meta.PetStates.Count} pet(s): " +
                string.Join(", ", meta.PetStates.Select(p => $"'{p.Name}' ({p.PetType} breed {p.WhichBreed}, friendship {p.Friendship}/1000)")) + ".",
                LogLevel.Info);
        }

        /// <summary>Restore every previously-snapshotted pet on the Farm. No-op when the
        /// upgrade isn't owned or when no snapshots were captured. Sets MarniePetAdoption
        /// mail so the post-reset vanilla pet-adoption offer doesn't double up.</summary>
        public static void RestorePet(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;
            PetCarryover.MigrateLegacy(meta);
            if (meta.PetStates.Count == 0) return;

            Farm farm = Game1.getFarm();
            if (farm == null)
            {
                monitor?.Log("PetCarryover: Game1.getFarm() returned null; skipping restore.", LogLevel.Warn);
                return;
            }
            var restoredPets = new List<Pet>();
            for (int i = 0; i < meta.PetStates.Count; i++)
            {
                PetSnapshot snap = meta.PetStates[i];
                (int x, int y) = PetCarryover.RestoreTile(i);
                try
                {
                    Pet pet = new Pet(x, y, snap.WhichBreed, snap.PetType) { Name = snap.Name, displayName = snap.Name };
                    if (pet.friendshipTowardFarmer != null)
                        pet.friendshipTowardFarmer.Value = PetCarryover.ClampFriendship(snap.Friendship);
                    farm.characters.Add(pet);
                    restoredPets.Add(pet);
                    monitor?.Log($"PetCarryover: restored '{snap.Name}' ({snap.PetType}, breed {snap.WhichBreed}, friendship {snap.Friendship}/1000) at ({x}, {y}).", LogLevel.Info);
                }
                catch (System.Exception ex)
                {
                    monitor?.Log($"PetCarryover: restore of '{snap.Name}' threw {ex.GetType().Name}: {ex.Message}. Snapshot preserved for next attempt.", LogLevel.Error);
                }
            }
            EnsureBowls(farm, restoredPets, monitor);
            if (restoredPets.Count > 0 && !Game1.player.mailReceived.Contains("MarniePetAdoption"))
                Game1.player.mailReceived.Add("MarniePetAdoption");
        }

        /// <summary>Morning pass for saves already bitten by the one-bowl restore: a Keep Pet
        /// owner whose extra pet is sitting bowl-less in the farmhouse (0.13.0 through 0.16.2)
        /// gets it a bowl on the next day start instead of waiting for the next rewind, so it
        /// stops losing 10 friendship a day. No-op without the upgrade or when every pet already
        /// owns a bowl, so it costs nothing on a healthy save.</summary>
        public static void EnsureBowlsForAllPets(MetaState meta, IMonitor monitor)
        {
            if (meta == null || !meta.HasUpgrade(UpgradeId)) return;
            Farm farm = Game1.getFarm();
            if (farm == null) return;
            List<Pet> pets = Utility.getAllPets();
            if (pets == null || pets.All(p => p.GetPetBowl() != null)) return;
            EnsureBowls(farm, pets, monitor);
        }

        private const string PetBowlBuildingId = "Pet Bowl";

        /// <summary>How far west of <see cref="PetCarryover.BowlTile"/> to keep looking when that
        /// tile can't take a building (map obstacle). One tile per try.</summary>
        private const int BowlPlacementTries = 6;

        /// <summary>Give every pet in <paramref name="pets"/> a bowl of its own. The rebuilt farm
        /// ships exactly one bowl, and vanilla binds one pet per bowl (PetBowl.petId): a pet with
        /// no bowl is warped into the farmhouse every morning and loses 10 friendship a day
        /// (decompile Pet.dayUpdate, Pet.cs:447-484). Bumblewyn's cat "didn't reappear" after the
        /// 0.13.0 all-pets restore for exactly this reason (Nexus bug 1122901, 27 Aug).
        ///
        /// A pet that already owns a bowl is left alone. Otherwise the first unclaimed bowl on the
        /// farm is assigned; when none is left a new one is placed at
        /// <see cref="PetCarryover.BowlTile"/> for that pet's index (walking west if the tile is
        /// blocked by the map), debris cleared exactly like a kept building's footprint.
        /// Placing is best-effort: a pet that still ends up bowl-less is logged, never dropped.</summary>
        public static void EnsureBowls(Farm farm, IReadOnlyList<Pet> pets, IMonitor monitor)
        {
            if (farm == null || pets == null) return;
            for (int i = 0; i < pets.Count; i++)
            {
                Pet pet = pets[i];
                if (pet == null || pet.GetPetBowl() != null) continue;

                PetBowl bowl = farm.buildings.OfType<PetBowl>().FirstOrDefault(b => !b.HasPet())
                               ?? PlaceBowl(farm, i, monitor);
                if (bowl == null)
                {
                    monitor?.Log($"PetCarryover: no bowl for '{pet.Name}' — vanilla will keep it indoors and dock friendship until one is built.", LogLevel.Warn);
                    continue;
                }
                bowl.AssignPet(pet);
                monitor?.Log($"PetCarryover: '{pet.Name}' owns the bowl at ({bowl.tileX.Value}, {bowl.tileY.Value}).", LogLevel.Info);
            }
        }

        /// <summary>Vanilla's GameLocation.isBuildable, evaluated against the FARM. The vanilla
        /// method reads the Buildable/Diggable tile properties off <c>Game1.currentLocation</c>
        /// (GameLocation.cs:16930-16945), which during a reset or a morning pass is the farmhouse
        /// the player woke up in, so it rejected every farm tile (2026-08-27 smoke: "no buildable
        /// tile within 6 tiles west of (51,7)"). Same rules, right map.</summary>
        private static bool IsBowlTile(Farm farm, Vector2 tile)
        {
            int x = (int)tile.X, y = (int)tile.Y;
            Rectangle rect = farm.GetBuildableRectangle();
            if (rect != Rectangle.Empty && !rect.Contains(x, y)) return false;
            if (farm.getBuildingAt(tile) != null) return false;
            if (!farm.CanItemBePlacedHere(tile, itemIsPassable: false, CollisionMask.All, ~CollisionMask.Objects, useFarmerTile: true))
                return false;
            string buildable = farm.doesTileHavePropertyNoNull(x, y, "Buildable", "Back");
            if (buildable.Equals("t", System.StringComparison.OrdinalIgnoreCase) || buildable.Equals("true", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null
                && !buildable.Equals("f", System.StringComparison.OrdinalIgnoreCase);
        }

        private static PetBowl PlaceBowl(Farm farm, int petIndex, IMonitor monitor)
        {
            (int startX, int y) = PetCarryover.BowlTile(petIndex);
            for (int attempt = 0; attempt < BowlPlacementTries; attempt++)
            {
                int x = startX - attempt;
                var tile = new Vector2(x, y);
                // The fresh farm regenerates weeds/stones/twigs anywhere, including here; clear
                // them first (same ruling as kept buildings) so only real map obstacles say no.
                WorldResetService.ClearFootprint(farm, x, y, 1, 1);
                if (!IsBowlTile(farm, tile)) continue;

                if (Building.CreateInstanceFromId(PetBowlBuildingId, tile) is not PetBowl bowl)
                {
                    monitor?.Log($"PetCarryover: '{PetBowlBuildingId}' did not create a PetBowl; cannot place extra bowls.", LogLevel.Warn);
                    return null;
                }
                bowl.daysOfConstructionLeft.Value = 0;
                bowl.load();
                farm.buildings.Add(bowl);
                monitor?.Log($"PetCarryover: placed a pet bowl at ({x}, {y}).", LogLevel.Info);
                return bowl;
            }
            monitor?.Log($"PetCarryover: no buildable tile for a pet bowl within {BowlPlacementTries} tiles west of ({startX}, {y}).", LogLevel.Warn);
            return null;
        }

        /// <summary>Mail flag vanilla accepts as "this farmer has already been through the pet
        /// adoption question", which is one of the three gates that put the Adopt option on
        /// Marnie counter menu.</summary>
        private const string RejectedAdoptionMail = "MarniePetRejectedAdoption";

        /// <summary>Give a petless post-reset farm a way to get a pet again (Nexus post, rose1729:
        /// declined Keep Pet on loop 1, then was never offered a pet in loops 2 or 3).
        ///
        /// Both vanilla doors are shut by the rewind. The pet-arrival cutscene is re-marked seen by
        /// FarmerReset eventsSeen re-seed (only the furnace teach and the Demetrius cave are
        /// exempt), and Marnie counter "Adopt" option is gated on
        /// <c>(Utility.getAllPets().Count == 0 &amp;&amp; Game1.year >= 2) || mailReceived
        /// "MarniePetAdoption" || "MarniePetRejectedAdoption"</c> (decompile GameLocation.cs:10908
        /// and :10935), and the reset puts the year back to 1 and clears mailReceived. So a player
        /// who let the pet go could never get another one.
        ///
        /// Stamping the rejected-adoption flag on a petless loop re-opens the Marnie route without
        /// touching the cutscene or handing out a free pet: the player still has to walk to Marnie
        /// and adopt, and it starts at 0 hearts like the animals ruling. No-op when a pet survived
        /// the rewind (Keep Pet) or when the flag is already set. Call AFTER RestorePet.</summary>
        public static void EnableAdoptionIfPetless(IMonitor monitor)
        {
            if (Game1.player == null) return;
            if (Utility.getAllPets().Any()) return;                       // Keep Pet brought one back
            if (Game1.player.mailReceived.Contains("MarniePetAdoption")) return;
            if (Game1.player.mailReceived.Contains(RejectedAdoptionMail)) return;

            Game1.player.mailReceived.Add(RejectedAdoptionMail);
            monitor?.Log(
                "PetCarryover: no pet on the farm after the rewind; stamped " + RejectedAdoptionMail +
                " so Marnie counter offers the Adopt option this loop.", LogLevel.Info);
        }
    }
}
