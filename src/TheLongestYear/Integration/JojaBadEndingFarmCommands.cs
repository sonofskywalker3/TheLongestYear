using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's farm commands. The ones that change the farm (clear, pave, build)
    /// refuse to run anywhere but the farm inside the bad ending, which always ends at the title
    /// unsaved: everything they do is in memory only.
    ///
    /// <c>tlyClearFarm</c>: empties the whole farm (JojaFarmClear.ClearAll).
    /// <c>tlyPaveFarm &lt;floorId&gt;</c>: flooring on every tile it can legally go (JojaFarmClear.Pave).
    /// <c>tlyJojaFarm</c>: the rows of real coops and barns and their animals (JojaFactoryFarm), then
    /// splices the pan across every row into the script right after itself.
    /// <c>tlyAnimalsHome</c>: from now on each building's animals get the evening signal once its
    /// door is on screen (JojaFarmAnimals). Continues at once.
    /// <c>tlyAnimalsWait &lt;ms&gt;</c>: waits until every scene animal is inside, at most ms; then any
    /// still out go in the way vanilla moves animals home unseen.</summary>
    internal static partial class JojaBadEndingCommands
    {
        public const string ClearFarmName = "tlyClearFarm";
        public const string PaveFarmName = "tlyPaveFarm";
        public const string JojaFarmName = "tlyJojaFarm";
        public const string AnimalsHomeName = "tlyAnimalsHome";
        public const string AnimalsWaitName = "tlyAnimalsWait";

        private static float _waitElapsed = -1f;

        private static void RegisterFarmCommands()
        {
            Event.RegisterCommand(ClearFarmName, (evt, args, context) => OnSceneFarm(evt, ClearFarmName,
                farm => $"cleared {JojaFarmClear.ClearAll(farm)} (in memory; the scene ends unsaved)"));
            Event.RegisterCommand(PaveFarmName, PaveFarm);
            Event.RegisterCommand(JojaFarmName, JojaFarm);
            Event.RegisterCommand(AnimalsHomeName, (evt, args, context) => Guarded(evt, AnimalsHomeName, JojaFarmAnimals.ArmWhenSeen));
            Event.RegisterCommand(AnimalsWaitName, AnimalsWait);
        }

        private static void ResetFarmCommands() => _waitElapsed = -1f;

        /// <summary>Runs <paramref name="change"/> on the farm only when this is the bad ending on the
        /// farm; logs its summary; always advances.</summary>
        private static void OnSceneFarm(Event evt, string name, Func<Farm, string> change)
        {
            Guarded(evt, name, () =>
            {
                if (!_running || !IsBadEnding(evt) || !IsBadEnding(Game1.CurrentEvent) || Game1.currentLocation is not Farm farm)
                {
                    _monitor.Log($"{name}: only runs on the farm inside the bad ending; skipping.", LogLevel.Warn);
                    return;
                }
                _monitor.Log($"{name}: {change(farm)}.", LogLevel.Info);
            });
        }

        private static void PaveFarm(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string floor, out string error))
            {
                Skip(evt, PaveFarmName, error);
                return;
            }
            OnSceneFarm(evt, PaveFarmName, farm => $"flooring '{floor}' laid on {JojaFarmClear.Pave(farm, floor)} tiles (in memory)");
        }

        private static void JojaFarm(Event evt, string[] args, EventContext context)
        {
            List<string> pan = null;
            OnSceneFarm(evt, JojaFarmName, farm =>
            {
                var (summary, script) = JojaFactoryFarm.Build(farm);
                pan = script;
                return $"{summary} (in memory)";
            });
            if (pan == null || pan.Count == 0) return;
            try
            {
                // Guarded moved CurrentCommand past this command: the pan goes in right there.
                var commands = evt.eventCommands.ToList();
                commands.InsertRange(Math.Min(evt.CurrentCommand, commands.Count), pan);
                evt.eventCommands = commands.ToArray();
                _monitor.Log($"{JojaFarmName}: pan across the rows: {string.Join(" / ", pan)}", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{JojaFarmName}: could not add the pan ({ex.GetType().Name}: {ex.Message}); the scene goes on without it.", LogLevel.Warn);
            }
        }

        private static void AnimalsWait(Event evt, string[] args, EventContext context)
        {
            try
            {
                ArgUtility.TryGetOptionalInt(args, 1, out int timeout, out _, 20000);
                if (_waitElapsed < 0f) _waitElapsed = 0f;
                _waitElapsed += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                Farm farm = Game1.currentLocation as Farm;
                int outside = farm == null ? 0 : JojaFarmAnimals.Outside(farm);
                if (outside > 0 && _waitElapsed < timeout) return;   // called every tick until they are in
                if (outside > 0)
                    _monitor.Log($"{AnimalsWaitName}: {outside} of {JojaFarmAnimals.Count} animal(s) still out after {timeout} ms; moved in directly: "
                                 + string.Join("; ", JojaFarmAnimals.SendStragglersInside(farm)), LogLevel.Info);
                else
                    _monitor.Log($"{AnimalsWaitName}: all {JojaFarmAnimals.Count} animals went in on their own ({(int)_waitElapsed} ms after the last pan).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{AnimalsWaitName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
            _waitElapsed = -1f;
            evt.CurrentCommand++;
        }
    }
}
