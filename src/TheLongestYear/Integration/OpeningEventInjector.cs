using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>Replaces vanilla's arrival event (60367 in Data/Events/BusStop) with the opening's
    /// script while the mod is enabled. Keeping vanilla's key keeps every vanilla behaviour around it:
    /// Skip intro marks it seen, FarmerReset marks it seen after a rewind, and "end beginGame" puts the
    /// farmer to bed for Spring 1 exactly as vanilla does.</summary>
    internal sealed class OpeningEventInjector
    {
        internal const string AssetName = "Data/Events/BusStop";
        private readonly IMonitor _monitor;
        private readonly Func<bool> _enabled;

        public OpeningEventInjector(IMonitor monitor, Func<bool> enabled)
        {
            _monitor = monitor;
            _enabled = enabled;
        }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!_enabled() || !e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                if (!data.ContainsKey(OpeningScript.VanillaKey))
                    _monitor.Log($"Opening: {AssetName} has no '{OpeningScript.VanillaKey}' entry (another mod replaced it?); adding ours.", LogLevel.Warn);
                data[OpeningScript.VanillaKey] = OpeningScript.Build(EventText, IntroEventKeys.CcSeenMail);
            }, AssetEditPriority.Late);
        }

        /// <summary>Same sanitiser as the ending: a translated '"' or '/' would break the script.
        /// Internal so <c>tly_replayintro</c> can build the same script to start the event directly.</summary>
        internal static string EventText(string key)
        {
            string value = Strings.Get(key);
            return string.IsNullOrEmpty(value) ? value : value.Replace('"', '\'').Replace('/', ',');
        }
    }
}
