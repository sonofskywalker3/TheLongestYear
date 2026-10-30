using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Drops the DayStarted SMAPI raises right after the post-reset save (see
    /// <see cref="PostSaveDayStartLatch"/>).
    ///
    /// SMAPI raises Saved and then DayStarted from the same step, and it raises an event only
    /// while the event says it has listeners. So our Saved handler, the last one to run, switches
    /// SMAPI's DayStarted off for that one step, and the first tick after switches it back on.
    /// Every mod's handlers stay registered; they simply are not called for the day start that is
    /// not a new day.
    ///
    /// 1.1.5 did this with a Harmony prefix on SMAPI's event Raise instead. That method is shared
    /// by every SMAPI event, so another mod patching any event's Raise (Ultra Smooth patches
    /// TimeChanged's) replaced the compiled body with one that held only its own patches, and our
    /// prefix never ran (Celjhyn, 1.2.0). Switching the event off goes through no patched method,
    /// so no other mod's patch can undo it.
    ///
    /// The event is found by reflection on SMAPI's internals; if a SMAPI update moves it, the drop
    /// is skipped with a warning and the reset behaves as before. If a day start ever gets past
    /// the drop, our own DayStarted handler sees the armed latch and warns.
    ///
    /// Limits: this covers SMAPI's own raise, which goes through RaiseEmpty and its listener check.
    /// Anything calling the event's Raise directly bypasses the flag. A Saved handler at the same
    /// lowest priority registered after ours that adds a DayStarted handler switches the event back
    /// on; the leak warning catches both. SMAPI's event manager is shared by every split-screen
    /// screen, and the latch is too; the post-reset save runs on the main screen only.</summary>
    internal static class PostSaveDayStartDrop
    {
        private const string ManagedEventTypeName = "StardewModdingAPI.Framework.Events.ManagedEvent`1";

        internal static readonly PostSaveDayStartLatch Latch = new();

        private static IMonitor _monitor;
        /// <summary>SMAPI's ManagedEvent&lt;DayStartedEventArgs&gt;, or null if it was not found.</summary>
        private static object _dayStarted;
        /// <summary>Its HasListeners property; SMAPI raises the event only while it is true.</summary>
        private static PropertyInfo _hasListeners;
        /// <summary>We switched the event off and owe a switch back on.</summary>
        private static bool _switchedOff;
        private static bool _loggedReEnabled;
        private static bool _loggedRestoreFailure;

        /// <summary>Find SMAPI's DayStarted event behind this mod's helper.</summary>
        public static void Init(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            try
            {
                object gameLoop = helper.Events.GameLoop;
                object eventManager = AccessTools.Field(gameLoop.GetType(), "EventManager")?.GetValue(gameLoop);
                object dayStarted = eventManager == null ? null : MemberValue(eventManager, "DayStarted");
                PropertyInfo hasListeners = dayStarted == null ? null : AccessTools.Property(dayStarted.GetType(), "HasListeners");
                if (hasListeners?.GetSetMethod(nonPublic: true) == null || hasListeners.PropertyType != typeof(bool))
                {
                    monitor.Log(
                        "Day-28 save: SMAPI's day start event was not found, so the post-reset save still raises a second day start. " +
                        "Mods that spawn things each morning (Farm Type Manager) may spawn twice on Spring 1.", LogLevel.Warn);
                    return;
                }
                _dayStarted = dayStarted;
                _hasListeners = hasListeners;
            }
            catch (Exception ex)
            {
                monitor.Log(
                    $"Day-28 save: could not reach SMAPI's day start event ({ex.GetType().Name}: {ex.Message}), so the post-reset save " +
                    "still raises a second day start.", LogLevel.Warn);
            }
        }

        /// <summary>The post-reset save's Saving: drop the day start that follows its Saved.</summary>
        public static void Arm() => Latch.Arm();

        /// <summary>SMAPI's GameLoop.Saved, run after every other mod's Saved handler: a mod that
        /// adds a DayStarted handler in its own Saved switches the event back on.</summary>
        [EventPriority((EventPriority)int.MinValue)]
        public static void OnSaved(object sender, SavedEventArgs e)
        {
            if (!Latch.Gate() || _dayStarted == null) return;
            try
            {
                if (!(bool)_hasListeners.GetValue(_dayStarted)) return;
                _hasListeners.SetValue(_dayStarted, false);
                _switchedOff = true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Day-28 save: could not hold back the day start after the save ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
            }
        }

        /// <summary>SMAPI's GameLoop.DayStarted, first of ours. Normally it does not run for the day
        /// start we drop at all; if it does while the latch is armed, the drop failed.</summary>
        [EventPriority(EventPriority.High)]
        public static void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Latch.DayStartArrived()) return;
            string patchers = EventPatchOwners();
            _monitor?.Log(
                "Day-28 save: a second Spring 1 day start ran after the post-reset save even though it should have been dropped" +
                (_dayStarted == null ? " (SMAPI's day start event was not found at launch)" : _switchedOff ? " (the event was switched off and something switched it back on)" : "") +
                ". Mods that spawn things each morning (Farm Type Manager) may have spawned twice, which can break tonight's save." +
                (patchers.Length > 0 ? $" Mods patching SMAPI's event code: {patchers}." : ""), LogLevel.Warn);
        }

        /// <summary>Every tick (and leaving the save): switch the day start back on and report.
        /// SMAPI raises Saved and DayStarted before the first tick after a save, so by now the
        /// day start it owed has either been dropped or run.</summary>
        public static void Settle()
        {
            PostSaveDayStartLatch.Outcome outcome = Latch.Settle();
            TryRestore();
            switch (outcome)
            {
                case PostSaveDayStartLatch.Outcome.Dropped:
                    _monitor?.Log(
                        "Day-28 save: dropped the day start SMAPI raises after a save. The post-reset save is not a new day, " +
                        "and mods already had Spring 1's day start after the reload.", LogLevel.Info);
                    break;
                case PostSaveDayStartLatch.Outcome.NoSaved:
                    _monitor?.Log("Day-28 save: no Saved followed the post-reset save's Saving; nothing was dropped.", LogLevel.Trace);
                    break;
            }
        }

        /// <summary>Switch the day start back on. The flag that says we owe it is cleared only once
        /// the switch has worked, so a failure is tried again next tick rather than leaving every
        /// later morning without its day start.</summary>
        private static void TryRestore()
        {
            if (!_switchedOff) return;
            try
            {
                // Something turned it back on between our Saved handler and now: a Saved handler
                // that ran after ours (same lowest priority, registered later) adding a DayStarted
                // handler. That day start then ran, and OnDayStarted has already warned.
                if ((bool)_hasListeners.GetValue(_dayStarted) && !_loggedReEnabled)
                {
                    _loggedReEnabled = true;
                    _monitor?.Log("Day-28 save: the day start event was switched back on by something else before the drop could hold.", LogLevel.Info);
                }
                _hasListeners.SetValue(_dayStarted, true);
                _switchedOff = false;
                _loggedRestoreFailure = false;
            }
            catch (Exception ex)
            {
                if (_loggedRestoreFailure) return;
                _loggedRestoreFailure = true;
                _monitor?.Log(
                    $"Day-28 save: could not switch the day start event back on ({ex.GetType().Name}: {ex.Message}); retrying every tick. " +
                    "Until it works, mods get no day start.", LogLevel.Error);
            }
        }

        public static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (Latch.Armed || Latch.Gated) Settle();
            else if (_switchedOff) TryRestore();
        }
        private static object MemberValue(object owner, string name)
        {
            Type type = owner.GetType();
            FieldInfo field = AccessTools.Field(type, name);
            if (field != null) return field.GetValue(owner);
            return AccessTools.Property(type, name)?.GetValue(owner);
        }

        /// <summary>Other mods' Harmony patches on SMAPI's event code, for the leak warning.</summary>
        private static string EventPatchOwners()
        {
            try
            {
                var owners = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MethodBase method in Harmony.GetAllPatchedMethods())
                {
                    Type declaring = method.DeclaringType;
                    if (declaring == null) continue;
                    Type definition = declaring.IsGenericType ? declaring.GetGenericTypeDefinition() : declaring;
                    if (definition.FullName != ManagedEventTypeName && definition.FullName != "StardewModdingAPI.Framework.InternalExtensions")
                        continue;
                    HarmonyLib.Patches info =Harmony.GetPatchInfo(method);
                    if (info == null) continue;
                    foreach (string owner in info.Owners) owners.Add(owner);
                }
                return string.Join(", ", owners);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Day-28 save: could not list event patches ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
                return "";
            }
        }
    }
}
