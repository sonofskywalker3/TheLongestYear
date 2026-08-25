using System.Linq;
using StardewModdingAPI;
using StardewValley;
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
    ///   <item><see cref="SnapshotPet"/> — call BEFORE <c>loadForNewGame</c>. Finds every
    ///         pet via <c>Utility.getAllPets()</c>, captures kind/breed/name/friendship of
    ///         each into <c>MetaState.PetStates</c>. Bails silently if the upgrade isn't
    ///         owned.</item>
    ///   <item><see cref="RestorePet"/> — call AFTER <c>loadForNewGame</c> (and after the
    ///         starting-animals placement so the farm is settled). Re-creates each
    ///         <see cref="Pet"/> from its snapshot and drops it on the farm porch, staggered
    ///         so they don't stack.
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
            int restored = 0;
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
                    restored++;
                    monitor?.Log($"PetCarryover: restored '{snap.Name}' ({snap.PetType}, breed {snap.WhichBreed}, friendship {snap.Friendship}/1000) at ({x}, {y}).", LogLevel.Info);
                }
                catch (System.Exception ex)
                {
                    monitor?.Log($"PetCarryover: restore of '{snap.Name}' threw {ex.GetType().Name}: {ex.Message}. Snapshot preserved for next attempt.", LogLevel.Error);
                }
            }
            if (restored > 0 && !Game1.player.mailReceived.Contains("MarniePetAdoption"))
                Game1.player.mailReceived.Add("MarniePetAdoption");
        }
    }
}
