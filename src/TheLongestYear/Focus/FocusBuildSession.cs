using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;

namespace TheLongestYear.Focus
{
    /// <summary>One free building placement for a focus card. Opens Robin's vanilla carpenter
    /// screen with only the card's building, free and instant, with demolish, move and paint
    /// hidden. When the screen closes, a building that was not on the farm before is the
    /// placement: it is finished on the spot and <c>onPlaced</c> runs (that is where the card is
    /// charged). No new building means the player backed out: <c>onCancelled</c> runs and nothing
    /// is charged. Host only; one session at a time.</summary>
    internal static class FocusBuildSession
    {
        private const string Builder = "Robin";

        private static CarpenterMenu _menu;
        private static string _buildingType;
        private static Farm _farm;
        private static HashSet<Building> _before;
        private static Action _onPlaced;
        private static Action _onCancelled;

        internal static IMonitor Monitor { get; set; }

        public static bool IsActive => _menu != null;

        /// <summary>True when <paramref name="menu"/> is this session's placement screen.</summary>
        internal static bool Owns(CarpenterMenu menu) => menu != null && ReferenceEquals(menu, _menu);

        /// <summary>Opens the placement screen. Returns false, with nothing opened and nothing
        /// charged, when the screen cannot open: another session is running, a scene or festival
        /// is on, a menu is up that will not close, or the building type is unknown.</summary>
        public static bool Start(string buildingType, Action onPlaced, Action onCancelled)
        {
            if (IsActive || !Context.IsWorldReady || !Context.IsMainPlayer) return false;
            if (Game1.eventUp || Game1.isFestival() || Game1.currentLocation == null) return false;
            Farm farm = Game1.getFarm();
            if (farm == null || buildingType == null
                || !Game1.buildingData.TryGetValue(buildingType, out BuildingData data)) return false;
            if (!CloseOpenMenu()) return false;

            _buildingType = buildingType;
            _farm = farm;
            _before = new HashSet<Building>(farm.buildings, ReferenceEqualityComparer.Instance);
            _onPlaced = onPlaced;
            _onCancelled = onCancelled;

            var menu = new CarpenterMenu(Builder, farm);
            menu.Blueprints.Clear();
            menu.Blueprints.Add(new CarpenterMenu.BlueprintEntry(0, buildingType, FreeCopy(data), null));
            menu.SetNewActiveBlueprint(0);
            HideOtherTools(menu);
            _menu = menu;
            Game1.activeClickableMenu = menu;
            Monitor?.Log($"Focus build: placement screen opened for '{buildingType}'.", LogLevel.Trace);
            return true;
        }

        /// <summary>UpdateTicked: once the placement screen is no longer the active menu, settle
        /// the session (placed or cancelled).</summary>
        public static void Tick()
        {
            if (!IsActive || ReferenceEquals(Game1.activeClickableMenu, _menu)) return;
            Settle();
        }

        /// <summary>Returning to title or a reload: drop the session as a cancel, nothing charged.</summary>
        public static void Abort()
        {
            if (!IsActive) return;
            Action cancelled = _onCancelled;
            Clear();
            Monitor?.Log("Focus build: placement dropped (save closed); nothing charged.", LogLevel.Trace);
            cancelled?.Invoke();
        }

        private static void Settle()
        {
            Action placed = _onPlaced, cancelled = _onCancelled;
            Building built = null;
            try
            {
                built = FindPlaced();
                if (built != null)
                    Finish(built);
                else
                    Monitor?.Log($"Focus build: placement of '{_buildingType}' closed with nothing placed; nothing charged.", LogLevel.Info);
            }
            finally
            {
                Clear();
            }
            if (built != null) placed?.Invoke();
            else cancelled?.Invoke();
        }

        /// <summary>Finishes the new building now. A failure is logged and the card is still
        /// charged, because the building stands on the farm either way.</summary>
        private static void Finish(Building built)
        {
            try
            {
                built.FinishConstruction();
                Monitor?.Log($"Focus build: '{_buildingType}' placed at {built.tileX.Value},{built.tileY.Value} and finished.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Focus build: '{_buildingType}' was placed but finishing it failed ({ex.GetType().Name}: {ex.Message}); it finishes on Robin's schedule instead.", LogLevel.Error);
            }
        }

        private static Building FindPlaced()
        {
            if (!Context.IsWorldReady) return null;
            // A different farm object means the save was reloaded under the screen: every building
            // would look new, so nothing counts as placed.
            Farm farm = Game1.getFarm();
            if (farm == null || !ReferenceEquals(farm, _farm)) return null;
            foreach (Building b in farm.buildings)
                if (!_before.Contains(b) && b.buildingType.Value == _buildingType)
                    return b;
            return null;
        }

        private static void Clear()
        {
            _menu = null;
            _farm = null;
            _buildingType = null;
            _before = null;
            _onPlaced = null;
            _onCancelled = null;
        }

        /// <summary>The planner may only close a menu that agrees to close; anything that opens a
        /// new menu on its way out also blocks the placement.</summary>
        private static bool CloseOpenMenu()
        {
            IClickableMenu open = Game1.activeClickableMenu;
            if (open == null) return true;
            if (!open.readyToClose()) return false;
            open.exitThisMenuNoSound();
            if (Game1.activeClickableMenu != null) return false;
            Game1.player?.forceCanMove();
            return true;
        }

        /// <summary>A copy of the building's data with no gold, no materials and no build days, so
        /// the screen shows the card's terms. The building itself is still created from the real
        /// data (vanilla builds from the type id), and <see cref="Settle"/> finishes it.</summary>
        private static BuildingData FreeCopy(BuildingData data)
        {
            var copy = (BuildingData)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(data, null);
            copy.BuildCost = 0;
            copy.BuildMaterials = new List<BuildingMaterial>();
            copy.BuildDays = 0;
            return copy;
        }

        internal static void HideOtherTools(CarpenterMenu menu)
        {
            if (menu.demolishButton != null) menu.demolishButton.visible = false;
            if (menu.moveButton != null) menu.moveButton.visible = false;
            if (menu.paintButton != null) menu.paintButton.visible = false;
        }
    }

    /// <summary>While a focus build session owns the carpenter screen: the build is free (enough
    /// resources, nothing consumed), Robin's construction dialogue is skipped (she did not build
    /// it), and demolish, move and paint stay hidden. Every other carpenter screen is untouched.</summary>
    [HarmonyPatch(typeof(CarpenterMenu))]
    internal static class FocusBuildSessionPatches
    {
        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CarpenterMenu.DoesFarmerHaveEnoughResourcesToBuild))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool BeforeResourceCheck(CarpenterMenu __instance, ref bool __result)
        {
            if (!FocusBuildSession.Owns(__instance)) return true;
            __result = true;
            return false;
        }

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CarpenterMenu.CanBuildCurrentBlueprint))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool BeforeCanBuild(CarpenterMenu __instance, ref bool __result)
        {
            if (!FocusBuildSession.Owns(__instance)) return true;
            __result = true;
            return false;
        }

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CarpenterMenu.ConsumeResources))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool BeforeConsume(CarpenterMenu __instance) => !FocusBuildSession.Owns(__instance);

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CarpenterMenu.robinConstructionMessage))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool BeforeRobinMessage(CarpenterMenu __instance)
        {
            if (!FocusBuildSession.Owns(__instance)) return true;
            __instance.exitThisMenu();
            Game1.player.forceCanMove();
            return false;
        }

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch("resetBounds")]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterResetBounds(CarpenterMenu __instance)
        {
            if (FocusBuildSession.Owns(__instance)) FocusBuildSession.HideOtherTools(__instance);
        }
    }
}
