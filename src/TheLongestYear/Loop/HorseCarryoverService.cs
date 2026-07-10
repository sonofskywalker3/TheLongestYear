using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Pre-reset capture + post-reset restore of the player's stable + horse, gated on the
    /// <c>early_horse</c> ("Keep Horse") upgrade. Pure carry-over: there is NO fixed auto-build, so
    /// the stable is only ever placed where the player built it. A player who hasn't built a stable
    /// yet simply has no horse that loop; once they build one it carries over at its tile every loop,
    /// keeping the horse's name and hat.
    ///
    /// Mirrors <see cref="PetCarryoverService"/>: <see cref="SnapshotHorse"/> runs BEFORE
    /// loadForNewGame (while the stable still exists); <see cref="RestoreHorse"/> runs AFTER, once
    /// the rebuilt Farm is settled.
    /// </summary>
    internal static class HorseCarryoverService
    {
        public const string UpgradeId = "early_horse";

        /// <summary>Fallback horse name. Vanilla treats a null/empty <c>Farmer.horseName</c> as
        /// "unnamed": <c>Stable.updateHorseOwnership</c> (run every morning from
        /// <c>Game1.UpdateHorseOwnership</c>) copies the owner's horseName onto the horse and, when
        /// that value is NULL, blanks the horse's name instead. An empty name then re-triggers
        /// <c>Horse.checkAction</c>'s NamingMenu. So we must never persist an empty name.</summary>
        private const string FallbackHorseName = "Horse";

        /// <summary>First non-blank of the candidates, else <see cref="FallbackHorseName"/>. Guards the
        /// empty-string trap: <c>??</c> only falls through on null, so `horse.Name ?? player.horseName`
        /// happily returns "" once vanilla has blanked the horse (see <see cref="FallbackHorseName"/>).
        /// Snapshotting "" made the blanking self-perpetuating across every loop — the player had to
        /// re-name the horse every single morning (user report 2026-07-09; save showed
        /// `&lt;horseName /&gt;` + horse `&lt;name /&gt;`).</summary>
        private static string FirstNonBlank(params string[] candidates)
        {
            foreach (string c in candidates)
                if (!string.IsNullOrWhiteSpace(c))
                    return c;
            return FallbackHorseName;
        }

        /// <summary>Capture the player's stable tile + horse name/hat into MetaState.HorseState.
        /// Clears the snapshot when no stable exists, so a demolished stable isn't resurrected.</summary>
        public static void SnapshotHorse(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;

            Farm farm = Game1.getFarm();
            Stable stable = farm?.buildings.OfType<Stable>().FirstOrDefault();
            if (stable == null)
            {
                meta.HorseState = null;
                return;
            }

            Horse horse = stable.getStableHorse();
            string name = FirstNonBlank(
                horse?.Name, Game1.player?.horseName?.Value, meta.HorseState?.HorseName);
            string hatId = horse?.hat?.Value?.QualifiedItemId;

            meta.HorseState = new HorseSnapshot(stable.tileX.Value, stable.tileY.Value, name, hatId);
            monitor?.Log(
                $"HorseCarryover: snapshot stable at ({stable.tileX.Value},{stable.tileY.Value}), " +
                $"horse '{name}'{(hatId != null ? $", hat {hatId}" : "")}.",
                LogLevel.Info);
        }

        /// <summary>
        /// Day-start repair for the "have to re-name the horse every morning" bug (user report
        /// 2026-07-09). Vanilla's <c>Game1.UpdateHorseOwnership</c> runs each new day and calls
        /// <c>Stable.updateHorseOwnership</c>, which blanks the horse's name whenever the owning
        /// farmer's <c>horseName</c> is null — and <c>Horse.checkAction</c> then re-opens the
        /// NamingMenu. A loop reset builds a fresh Farmer (horseName null), so once the name was
        /// lost it could never come back on its own.
        ///
        /// Re-asserts, in order: the stable's owner (so <c>getOwner()</c> resolves), the farmer's
        /// horseName, and the horse's own name. Idempotent, and a no-op once everything is sane —
        /// so it also repairs saves already stuck in the blank-name state.
        /// </summary>
        public static void EnsureHorseNamed(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;

            Farm farm = Game1.getFarm();
            Stable stable = farm?.buildings.OfType<Stable>().FirstOrDefault();
            if (stable == null) return;

            long playerId = Game1.player?.UniqueMultiplayerID ?? 0L;
            bool ownerFixed = false;
            if (playerId != 0L && stable.owner.Value != playerId)
            {
                stable.owner.Value = playerId;
                ownerFixed = true;
            }

            Horse horse = stable.getStableHorse();
            string current = FirstNonBlank(
                horse?.Name, Game1.player?.horseName?.Value, meta.HorseState?.HorseName);

            bool nameFixed = false;
            if (Game1.player?.horseName != null
                && string.IsNullOrWhiteSpace(Game1.player.horseName.Value))
            {
                Game1.player.horseName.Value = current;
                nameFixed = true;
            }
            if (horse != null && string.IsNullOrWhiteSpace(horse.Name))
            {
                horse.Name = current;
                horse.displayName = current;
                nameFixed = true;
            }

            if (ownerFixed || nameFixed)
                monitor?.Log(
                    $"HorseCarryover: repaired horse identity (owner={(ownerFixed ? "set" : "ok")}, " +
                    $"name={(nameFixed ? $"restored to '{current}'" : "ok")}) — vanilla blanks an " +
                    "unowned/unnamed horse every morning.",
                    LogLevel.Info);
        }

        /// <summary>Rebuild the snapshotted stable at its saved tile and re-spawn the horse with its
        /// kept name + hat. No-op without the upgrade, without a snapshot, or if a stable is already
        /// present. Best-effort: a failure is logged and the snapshot preserved for next loop.</summary>
        public static void RestoreHorse(MetaState meta, IMonitor monitor)
        {
            if (meta == null) return;
            if (!meta.HasUpgrade(UpgradeId)) return;
            if (meta.HorseState == null) return;

            Farm farm = Game1.getFarm();
            if (farm == null)
            {
                monitor?.Log("HorseCarryover: Game1.getFarm() returned null; skipping restore.", LogLevel.Warn);
                return;
            }
            if (farm.buildings.OfType<Stable>().Any())
                return; // already present — idempotent across re-resets

            HorseSnapshot snap = meta.HorseState;
            try
            {
                var stable = new Stable(new Vector2(snap.StableTileX, snap.StableTileY));
                stable.daysOfConstructionLeft.Value = 0;
                // Claim the stable for the player BEFORE grabHorse: it copies owner.Value onto the
                // spawned horse's ownerId, and vanilla only self-heals an unset owner from its own
                // -6666666 sentinel (Game1.UpdateHorseOwnership), never from 0. An owner-less horse
                // has no getOwner(), so the morning ownership pass can't keep its name.
                stable.owner.Value = Game1.player?.UniqueMultiplayerID ?? 0L;
                stable.load();
                farm.buildings.Add(stable);

                // The farm was just regenerated (loadForNewGame), so the stable's tile may now have
                // freshly-spawned weeds/stones/twigs/grass/clumps poking through it. Clear the
                // footprint exactly as Robin's own build does, so the restored stable looks clean.
                farm.removeObjectsAndSpawned(
                    stable.tileX.Value, stable.tileY.Value, stable.tilesWide.Value, stable.tilesHigh.Value);

                stable.grabHorse();

                Horse horse = stable.getStableHorse();
                if (horse != null)
                {
                    // Never write a blank name (see FallbackHorseName): Farmer.horseName is the
                    // authority the morning ownership pass copies from, and a null/empty one blanks
                    // the horse and re-opens the NamingMenu every day.
                    string name = FirstNonBlank(snap.HorseName, Game1.player?.horseName?.Value);
                    horse.Name = name;
                    horse.displayName = name;
                    if (Game1.player?.horseName != null)
                        Game1.player.horseName.Value = name;
                    if (snap.HatQualifiedItemId != null)
                    {
                        try { horse.hat.Value = ItemRegistry.Create<Hat>(snap.HatQualifiedItemId); }
                        catch (System.Exception) { /* hat id no longer valid — leave bare-headed */ }
                    }
                }

                monitor?.Log(
                    $"HorseCarryover: restored stable at ({snap.StableTileX},{snap.StableTileY}), " +
                    $"horse '{snap.HorseName}'.",
                    LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                monitor?.Log(
                    $"HorseCarryover: restore threw {ex.GetType().Name}: {ex.Message}. " +
                    "Stable not restored this loop; snapshot preserved.",
                    LogLevel.Error);
            }
        }
    }
}
