using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>Rewrites grandpa's deathbed speech and the letter for the opening. Active whenever the
    /// mod is enabled: while it is, every new farm is a Longest Year farm, and these strings are only
    /// read by the new-game minigame (GrandpaStory.cs), so a loaded vanilla save never sees them.</summary>
    internal sealed class OpeningStringsEditor
    {
        private const string AssetName = "Strings/StringsFromCSFiles";
        private readonly IMonitor _monitor;
        private readonly Func<bool> _enabled;

        public OpeningStringsEditor(IMonitor monitor, Func<bool> enabled)
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
                int replaced = OpeningStrings.Apply(data, Strings.Get);
                _monitor.Log($"Opening: replaced {replaced} of {OpeningStrings.Replacements.Count} deathbed and letter strings.", LogLevel.Trace);
            }, AssetEditPriority.Default);
        }
    }
}
