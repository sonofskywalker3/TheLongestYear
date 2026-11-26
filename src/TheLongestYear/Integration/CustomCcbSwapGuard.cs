using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Custom Community Center Bundles and Challenging Community Center Bundles (one code base, type
    /// <c>CCCB.CCCB</c>) swap bundles in every morning: on DayStarted each live bundle whose name is in
    /// their pack (Spring Crops, Animal, Crab Pot, the Vault...) gets the pack's ingredients, and on
    /// DayEnding and Saving the old values go back (decompile CCCB.cs 59-96). On a TLY Custom board that
    /// leaves the Community Center asking for the pack's fixed bundle under TLY's name all day while the
    /// Bundle Log tracks TLY's board (mjlol73, 2026-11-25: "the exact same spring crop bundle" every save).
    /// So while TLY's own board owns the Center, both handlers are skipped; on a vanilla-type board they run
    /// as before and TLY re-reads the board they leave (ModEntry.ReclassifyIfBoardChanged). Skipping, not
    /// undoing afterwards: the swap's SetBundleData would grow the bundles' completion arrays for the
    /// pack's larger bundles, and the game never shrinks them (NetWorldState.SetBundleData). Soft: applied
    /// at GameLaunched, only to a type whose handlers and fields match the code read.
    /// </summary>
    internal static class CustomCcbSwapGuard
    {
        private const string TypeFullName = "CCCB.CCCB";
        /// <summary>CCCB's morning swap (DayStarted, named after an older hook) and its evening restore
        /// (DayEnding and Saving).</summary>
        private static readonly string[] Handlers = { "OnSaveLoaded", "OnSaving" };
        private static readonly string[] Fields = { "OldBundleData", "NewBundleData", "CustomBundleData" };

        /// <summary>True while a TLY run is active on a TLY Custom board; set by ModEntry.</summary>
        internal static Func<bool> TlyBoardOwnsTheCenter = () => false;

        internal static void Apply(Harmony harmony, IMonitor monitor)
        {
            foreach (Type type in FindTypes(monitor))
            {
                string name = type.Assembly.GetName().Name;
                var patched = new List<System.Reflection.MethodInfo>();
                try
                {
                    if (Fields.Any(f => AccessTools.Field(type, f) == null))
                    {
                        monitor.Log($"{name}: its bundle fields are not the ones TLY knows; its morning bundle swap is left alone.", LogLevel.Warn);
                        continue;
                    }
                    var methods = Handlers
                        .Select(handler => AccessTools.Method(type, handler, new[] { typeof(object), typeof(EventArgs) }))
                        .ToList();
                    if (methods.Any(m => m == null))
                    {
                        monitor.Log($"{name}: its bundle swap handlers are not the ones TLY knows; its morning bundle swap is left alone.", LogLevel.Warn);
                        continue;
                    }
                    foreach (var method in methods)
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(CustomCcbSwapGuard), nameof(Prefix)));
                        patched.Add(method);
                    }
                    monitor.Log($"{name}: its morning bundle swap is skipped while a TLY Custom board is in play.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    // Both handlers or neither: a skipped swap with a live restore would put a stale board back.
                    foreach (var method in patched)
                        try { harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id); }
                        catch (Exception unpatchEx) { monitor.Log($"{name}: removing a half-installed hook failed ({unpatchEx.Message}).", LogLevel.Warn); }
                    monitor.Log($"{name}: hooking its bundle swap failed ({ex.Message}); it keeps swapping bundles on TLY Custom boards.", LogLevel.Warn);
                }
            }
        }

        /// <summary>Every loaded assembly's <c>CCCB.CCCB</c>, asked of each assembly on its own so one that
        /// cannot list its types stops nothing.</summary>
        private static IEnumerable<Type> FindTypes(IMonitor monitor)
        {
            var found = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic) continue;
                    Type type = assembly.GetType(TypeFullName, throwOnError: false);
                    if (type != null) found.Add(type);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Looking for Custom Community Center Bundles in {assembly.GetName().Name} failed ({ex.Message}).", LogLevel.Trace);
                }
            }
            return found;
        }

        private static bool Prefix() => !TlyBoardOwnsTheCenter();
    }
}
