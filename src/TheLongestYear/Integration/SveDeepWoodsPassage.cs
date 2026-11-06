using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using xTile;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Opens the way into Deep Woods when Stardew Valley Expanded is installed with it.
    /// <para>
    /// Deep Woods cuts its passage into the Secret Woods at fixed tiles of the vanilla map. SVE
    /// replaces that map with a larger one, where those tiles are a closed pocket, and ships a map
    /// patch that joins the pocket to the woods (<see cref="PatchFile"/>). SVE applies it only when
    /// the mod <see cref="SveDeepWoodsPassageRule.OldDeepWoodsId"/> is installed, the id Deep Woods had under its first
    /// author; today's Deep Woods is <see cref="SveDeepWoodsPassageRule.DeepWoodsId"/>, so the patch never runs and Deep
    /// Woods can be reached only by minecart (verified in game 2026-11-06, SVE 1.15.11 with Deep
    /// Woods 4.3.1-beta: no path from the woods' entrance to any passage tile).
    /// </para>
    /// <para>
    /// This applies SVE's own patch, from SVE's own files, to the same area SVE names, before Deep
    /// Woods makes its cut. Nothing of SVE's is copied here. It does nothing when the old id is
    /// installed (SVE then patches the map itself), when either mod is missing, when the file is
    /// gone, or on a save that is not a Longest Year run. Fail open: an error is logged once and the
    /// map is left as the two mods made it.
    /// </para>
    /// </summary>
    internal sealed class SveDeepWoodsPassage
    {
        private const string WoodsMap = "Maps/Woods";
        /// <summary>SVE's file and target area (its code/Locations/MapPatches.json, "DeepWoodsCompatibility").</summary>
        private const string PatchFile = "Assets/Maps/MapPatches/DeepWoodsCompatibility.tmx";
        private static readonly Rectangle PatchArea = new(23, 21, 11, 12);
        private const string ContentPackProperty = "ContentPack";

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly bool _applies;
        private bool _warned;

        public SveDeepWoodsPassage(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
            _applies = SveDeepWoodsPassageRule.Applies(helper.ModRegistry.IsLoaded);
            if (!_applies) return;
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        /// <summary>Reads the map again. The map is first read before the save says whether it is a
        /// Longest Year run, and stays cached after the run is left, so ModEntry calls this right after
        /// the run is switched on and right after it is switched off.</summary>
        public void Reload()
        {
            if (_applies) _helper.GameContent.InvalidateCache(WoodsMap);
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(WoodsMap)) return;
            // Early: after SVE's map is loaded, before Deep Woods cuts its passage and adds its warps.
            e.Edit(asset =>
            {
                if (!RunActivation.IsActive) return;
                try
                {
                    IContentPack pack = SvePack();
                    if (pack == null || !pack.HasFile(PatchFile))
                    {
                        WarnOnce("Stardew Valley Expanded's Deep Woods map patch was not found; the passage into Deep Woods stays as the two mods left it.");
                        return;
                    }
                    Map patch = pack.ModContent.Load<Map>(PatchFile);
                    asset.AsMap().PatchMap(patch, targetArea: PatchArea, patchMode: PatchMapMode.ReplaceByLayer);
                    _monitor.Log("Applied Stardew Valley Expanded's Deep Woods map patch to the Secret Woods (it is keyed on Deep Woods' old id and no longer runs by itself).", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    WarnOnce($"Could not apply Stardew Valley Expanded's Deep Woods map patch; the passage into Deep Woods stays as the two mods left it. {ex.GetType().Name}: {ex.Message}");
                }
            }, AssetEditPriority.Early);
        }

        /// <summary>SVE's content pack, from SMAPI's record of the mod (the registry's public face does
        /// not carry it). Null when SMAPI no longer holds it under that name.</summary>
        private IContentPack SvePack()
        {
            IModInfo info = _helper.ModRegistry.Get(SveUnlocks.ModId);
            return info == null ? null : AccessTools.Property(info.GetType(), ContentPackProperty)?.GetValue(info) as IContentPack;
        }

        private void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            _monitor.Log(message, LogLevel.Warn);
        }
    }
}
