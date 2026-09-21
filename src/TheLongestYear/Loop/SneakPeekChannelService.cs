using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Relabels the Wednesday TV channel while the Sneak Peek Boost is active.
    ///
    /// <see cref="QueenOfSaucePatch"/> makes that slot air a year-2 episode instead of a rerun, so
    /// the vanilla label ("The Queen Of Sauce (Re-run)") would name a show the player is not about
    /// to watch. TV.checkForAction builds its channel list from
    /// <c>Strings\StringsFromCSFiles:TV.cs.13117</c> every time the set is clicked, so editing that
    /// one string is enough: no Harmony patch on the menu, and the Sunday label (13114) is a
    /// different key, so the normal episode is left alone.
    ///
    /// Cache discipline mirrors <see cref="PierreYear2SeedsService"/>: the Boost is per save and
    /// per season, so ModEntry invalidates this asset when a TLY save loads, when the Boost is
    /// bought, when it expires at the season roll, and on return to title (BoostChecker is null
    /// there, so no edit is made).
    /// </summary>
    internal sealed class SneakPeekChannelService
    {
        public const string StringsAssetName = "Strings/StringsFromCSFiles";

        /// <summary>The Wednesday rerun entry in TV.checkForAction's channel list.</summary>
        private const string RerunChannelKey = "TV.cs.13117";

        private readonly IMonitor _monitor;

        public SneakPeekChannelService(IMonitor monitor) => _monitor = monitor;

        private static bool Active => BoostChecker.SneakPeekActive?.Invoke() == true;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(StringsAssetName)) return;
            if (!Active) return;

            e.Edit(asset =>
            {
                var strings = asset.AsDictionary<string, string>().Data;
                if (!strings.ContainsKey(RerunChannelKey))
                {
                    _monitor.Log($"SneakPeek: {RerunChannelKey} not found in {StringsAssetName} — channel keeps its vanilla label.", LogLevel.Warn);
                    return;
                }

                strings[RerunChannelKey] = Strings.Get("tv.sneak-peek-channel");
                _monitor.Log("SneakPeek: Wednesday channel relabelled for the season.", LogLevel.Trace);
            }, AssetEditPriority.Late);
        }
    }
}
