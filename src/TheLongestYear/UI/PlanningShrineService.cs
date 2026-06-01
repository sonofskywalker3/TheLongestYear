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
                    // Texture path uses backslashes: Data/Furniture is split on '/', so a "Mods/.../Shrine"
                    // path would be truncated to "Mods" in field 9. SMAPI normalizes '\' vs '/'.
                    string tex = ShrineTextureAsset.Replace('/', '\\');
                    data[ShrineId] = $"Planning Shrine/decor/1 2/1 1/1/0/-1/Junimo Planning Shrine/0/{tex}";
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

        /// <summary>Keep the shrine fixed in place (the player can't pick it up), matching the
        /// home-anchored stash chest — they're both auto-placed fixtures, re-placed each loop.
        /// canBeRemoved gates furniture pickup in GameLocation; force it false for our shrine.</summary>
        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.canBeRemoved))]
        internal static class ShrineCannotBeRemovedPatch
        {
            private static void Postfix(StardewValley.Objects.Furniture __instance, ref bool __result)
            {
                if (__instance.ItemId == ShrineId) __result = false;
            }
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

                // First open: dismiss the indicator (so the intro quest won't re-add) and
                // complete the shrine intro quest if it's still in the log.
                state.DismissedIndicators.Add("tly.shrine");
                if (Game1.player?.questLog != null)
                {
                    foreach (var q in Game1.player.questLog)
                    {
                        if (q != null && q.id.Value == "tly.-9005")
                        {
                            q.questComplete();
                            break;
                        }
                    }
                }

                Game1.activeClickableMenu = new ShrinePreviewMenu(state);
                __result = true;
                return false;
            }
        }
    }
}
