using System;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>tly_shrinegoals and tly_shrinedonate: headless checks of the random shrine goals.</summary>
    internal static class ShrineDonationDebug
    {
        public static void List(IMonitor monitor)
        {
            ShrineDonationService service = ShrineDonationService.Active;
            if (service == null) { monitor.Log("tly_shrinegoals: load a TLY save first.", LogLevel.Warn); return; }
            if (service.Goals.Count == 0) { monitor.Log("tly_shrinegoals: no shrine goals this week.", LogLevel.Info); return; }
            for (int i = 0; i < service.Goals.Count; i++)
            {
                ShrineGoal g = service.Goals[i];
                monitor.Log($"tly_shrinegoals: [{i}] list {g.ListIndex} {g.ItemId} x{g.Stack} deposited={g.Deposited} paid={g.Paid}", LogLevel.Info);
            }
        }

        /// <summary>Spawns the goal's stack when no inventory stack can fill it, then donates through
        /// <see cref="ShrineDonationService.Donate"/> (the Donate tab's path).</summary>
        public static void Donate(string[] args, IMonitor monitor)
        {
            ShrineDonationService service = ShrineDonationService.Active;
            if (service == null) { monitor.Log("tly_shrinedonate: load a TLY save first.", LogLevel.Warn); return; }
            if (args.Length < 1 || !int.TryParse(args[0], out int index) || index < 0 || index >= service.Goals.Count)
            {
                monitor.Log($"Usage: tly_shrinedonate <index 0..{service.Goals.Count - 1}> (see tly_shrinegoals).", LogLevel.Warn);
                return;
            }
            ShrineGoal goal = service.Goals[index];
            if (goal.Deposited) { monitor.Log($"tly_shrinedonate: goal {index} is already deposited.", LogLevel.Warn); return; }

            Item stack = FindFilling(service, index);
            if (stack == null)
            {
                Item spawned;
                try { spawned = ItemRegistry.Create(goal.ItemId, goal.Stack); }
                catch (Exception ex)
                {
                    monitor.Log($"tly_shrinedonate: couldn't create '{goal.ItemId}': {ex.Message}", LogLevel.Warn);
                    return;
                }
                if (!Game1.player.addItemToInventoryBool(spawned))
                {
                    monitor.Log("tly_shrinedonate: inventory is full, nothing spawned.", LogLevel.Warn);
                    return;
                }
                monitor.Log($"tly_shrinedonate: spawned {goal.Stack}x {goal.ItemId}.", LogLevel.Info);
                stack = FindFilling(service, index);
            }
            if (stack == null) { monitor.Log("tly_shrinedonate: no single inventory stack can fill the goal.", LogLevel.Warn); return; }

            long before = service.JunimoPoints;
            bool ok = service.Donate(index, stack);
            monitor.Log(ok
                    ? $"tly_shrinedonate: goal {index} donated (JP {before} -> {service.JunimoPoints})."
                    : $"tly_shrinedonate: goal {index} refused.",
                ok ? LogLevel.Info : LogLevel.Warn);
        }

        private static Item FindFilling(ShrineDonationService service, int index)
        {
            foreach (Item item in Game1.player.Items)
                if (service.CanFill(index, item)) return item;
            return null;
        }
    }
}
