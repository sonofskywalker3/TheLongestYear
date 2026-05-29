using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Pre-reset capture + post-reset restore of the player's pet, gated on the
    /// <c>keep_pet</c> upgrade. Pure helper class — no state of its own, just two static
    /// methods called by <see cref="WorldResetService.PerformReset"/>.
    ///
    /// 2026-05-29 spec: pet survives loop resets WITH its accumulated friendship hearts.
    /// Barn/coop animals do NOT — they're rebuilt fresh by ApplyStartingAnimals (0 hearts
    /// per user direction "the 'keep 1 cow' should still start over with 0 hearts so they
    /// can't be getting large milk day 1"). Keep these two paths visually separate.
    ///
    /// Lifecycle:
    /// <list type="number">
    ///   <item><see cref="SnapshotPet"/> — call BEFORE <c>loadForNewGame</c>. Finds the
    ///         player's pet via <c>Utility.getAllPets()</c>, captures kind/breed/name/
    ///         friendship into <c>MetaState.PetState</c>. Bails silently if the upgrade
    ///         isn't owned or no pet exists.</item>
    ///   <item><see cref="RestorePet"/> — call AFTER <c>loadForNewGame</c> (and after the
    ///         starting-animals placement so the farm is settled). Re-creates the
    ///         <see cref="Pet"/> from the snapshot and drops it on the farm porch.
    ///         Also sets the <c>MarniePetAdoption</c> mail flag so vanilla's day-1 pet-
    ///         adoption offer doesn't fire on top of the restored pet.</item>
    /// </list>
    /// </summary>
    internal static class PetCarryoverService
    {
        public const string UpgradeId = "keep_pet";

        /// <summary>Where the restored pet lands on the Farm — near the farmhouse porch
        /// (StandardFarm coords). The pet AI wanders freely from here.</summary>
        private const int RestoreTileX = 54;
        private const int RestoreTileY = 8;

        /// <summary>Capture the player's pet into MetaState.PetState. Idempotent — overwrites
        /// any prior snapshot. No-op when the upgrade isn't owned (so a player without
        /// keep_pet doesn't accidentally bank a stale pet between toggles).</summary>
        public static void SnapshotPet(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;

            var pets = Utility.getAllPets();
            if (pets == null || pets.Count == 0)
            {
                // No pet on the farm — clear any stale snapshot so a future restore won't
                // resurrect a pet the player intentionally let lapse.
                meta.PetState = null;
                return;
            }

            // One pet is the only supported case in vanilla until late game; if the player
            // somehow has multiple (mod / multiplayer), the first one wins.
            Pet pet = pets[0];
            meta.PetState = new PetSnapshot(
                PetType:   pet.petType?.Value   ?? "Cat",
                WhichBreed: pet.whichBreed?.Value ?? "0",
                Name:       pet.Name             ?? "Pet",
                Friendship: pet.friendshipTowardFarmer?.Value ?? 0);

            monitor?.Log(
                $"PetCarryover: snapshot '{pet.Name}' ({pet.petType?.Value}, breed " +
                $"{pet.whichBreed?.Value}, friendship {pet.friendshipTowardFarmer?.Value}/1000).",
                LogLevel.Info);
        }

        /// <summary>Restore the previously-snapshotted pet on the Farm. No-op when the
        /// upgrade isn't owned or when no snapshot was captured. Sets MarniePetAdoption mail
        /// so the post-reset vanilla pet-adoption offer doesn't double up.</summary>
        public static void RestorePet(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;
            if (meta.PetState == null) return;

            var snap = meta.PetState;
            try
            {
                Pet pet = new Pet(RestoreTileX, RestoreTileY, snap.WhichBreed, snap.PetType)
                {
                    Name = snap.Name,
                    displayName = snap.Name
                };
                if (pet.friendshipTowardFarmer != null)
                    pet.friendshipTowardFarmer.Value = System.Math.Max(0, System.Math.Min(1000, snap.Friendship));

                Farm farm = Game1.getFarm();
                if (farm == null)
                {
                    monitor?.Log("PetCarryover: Game1.getFarm() returned null; skipping restore.", LogLevel.Warn);
                    return;
                }
                farm.characters.Add(pet);

                // Suppress vanilla's day-1 Marnie adoption offer (mailReceived flag) so the
                // player doesn't get asked about a pet they already have. Idempotent —
                // mailReceived is a HashSet-style net collection in practice.
                if (!Game1.player.mailReceived.Contains("MarniePetAdoption"))
                    Game1.player.mailReceived.Add("MarniePetAdoption");

                monitor?.Log(
                    $"PetCarryover: restored '{snap.Name}' ({snap.PetType}, breed " +
                    $"{snap.WhichBreed}, friendship {snap.Friendship}/1000) at " +
                    $"({RestoreTileX}, {RestoreTileY}) on Farm.",
                    LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                monitor?.Log(
                    $"PetCarryover: restore threw {ex.GetType().Name}: {ex.Message}. " +
                    "Pet not restored this loop; snapshot preserved for next attempt.",
                    LogLevel.Error);
            }
        }
    }
}
