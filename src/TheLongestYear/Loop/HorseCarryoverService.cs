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
            string name = horse?.Name ?? Game1.player?.horseName?.Value ?? "Horse";
            string hatId = horse?.hat?.Value?.QualifiedItemId;

            meta.HorseState = new HorseSnapshot(stable.tileX.Value, stable.tileY.Value, name, hatId);
            monitor?.Log(
                $"HorseCarryover: snapshot stable at ({stable.tileX.Value},{stable.tileY.Value}), " +
                $"horse '{name}'{(hatId != null ? $", hat {hatId}" : "")}.",
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
                stable.load();
                farm.buildings.Add(stable);
                stable.grabHorse();

                Horse horse = stable.getStableHorse();
                if (horse != null)
                {
                    horse.Name = snap.HorseName;
                    horse.displayName = snap.HorseName;
                    if (Game1.player?.horseName != null)
                        Game1.player.horseName.Value = snap.HorseName;
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
