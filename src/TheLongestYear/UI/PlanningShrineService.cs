using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The in-world, view-only planning shrine. Registered as custom furniture (so it has
    /// the shrine sprite) and auto-placed on the Farm ~5 tiles left of the stash each load, so it's
    /// present from loop 1 and home-anchored (re-placed even if moved). Interacting opens the
    /// read-only <see cref="ShrinePreviewMenu"/> — no JP is spent here.</summary>
    internal sealed class PlanningShrineService
    {
        internal const string ShrineId = "sonofskywalker3.TheLongestYear_PlanningShrine";
        internal const string ShrineTextureAsset = "Mods/sonofskywalker3.TheLongestYear/Shrine";

        private readonly IMonitor _monitor;
        private static System.Func<MetaState> _state;

        public PlanningShrineService(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        public void AttachState(System.Func<MetaState> state) => _state = state;

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(ShrineTextureAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/shrine.png", AssetLoadPriority.Medium);
                return;
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // 1x2 decoration (16x32 sprite), no placement restriction, free.
                    data[ShrineId] = $"Planning Shrine/decor/1 2/1 1/1/0/-1/Junimo Planning Shrine/0/{ShrineTextureAsset}";
                }, AssetEditPriority.Default);
            }
        }

        /// <summary>(Re-)place the shrine ~5 tiles left of the stash. Removes any existing instance
        /// first so it stays home-anchored and never duplicates.</summary>
        public void Place(Vector2? stashTile)
        {
            if (stashTile == null) return;
            Farm farm = Game1.getFarm();
            if (farm == null) return;

            var stale = new System.Collections.Generic.List<StardewValley.Objects.Furniture>();
            foreach (StardewValley.Objects.Furniture f in farm.furniture)
                if (f.ItemId == ShrineId) stale.Add(f);
            foreach (StardewValley.Objects.Furniture f in stale)
                farm.furniture.Remove(f);

            Vector2 tile = new Vector2(stashTile.Value.X - 5, stashTile.Value.Y);
            StardewValley.Objects.Furniture shrine = StardewValley.Objects.Furniture.GetFurnitureInstance(ShrineId, tile);
            farm.furniture.Add(shrine);
            _monitor.Log($"PlanningShrineService: placed planning shrine at ({tile.X}, {tile.Y}).", LogLevel.Info);
        }

        /// <summary>Opens the read-only planning menu when the shrine furniture is acted on.</summary>
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.checkForAction))]
        internal static class ShrineActionPatch
        {
            private static bool Prefix(StardewValley.Objects.Furniture __instance, bool justCheckingForActivity, ref bool __result)
            {
                if (justCheckingForActivity) return true;
                if (__instance.ItemId != ShrineId) return true;
                MetaState state = _state?.Invoke();
                if (state == null) return true;
                Game1.activeClickableMenu = new ShrinePreviewMenu(state);
                __result = true;
                return false;
            }
        }
    }
}
