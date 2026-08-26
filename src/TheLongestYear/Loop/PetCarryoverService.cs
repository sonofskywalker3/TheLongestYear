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
    ///   <item><see cref="SnapshotPet"/>: call BEFORE <c>loadForNewGame</c>. Finds every
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
