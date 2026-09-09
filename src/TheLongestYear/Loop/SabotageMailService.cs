using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The town's one glimpse of each front (Jeff, 2026-09-09): the first time a front
    /// strikes on a save, a villager writes the next morning about what they half saw. Linus for
    /// blight, Shane for a donation coming undone, Lewis for the board changing. Once per save
    /// (MetaState.SabotageLettersSent), so a player who loops sees each letter once; the town's
    /// forgetting is a per-loop thing, the player's learning is not.</summary>
    internal sealed class SabotageMailService
    {
        public const string BlightMailKey = "TLY_Darkness_Blight";
        public const string ReversionMailKey = "TLY_Darkness_Reversion";
        public const string TamperMailKey = "TLY_Darkness_Tamper";

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public SabotageMailService(IMonitor monitor, MetaStore meta)
        {
            _monitor = monitor;
            _meta = meta;
        }

        public static string MailKeyFor(SabotageKind kind) => kind switch
        {
            SabotageKind.Blight => BlightMailKey,
            SabotageKind.Reversion => ReversionMailKey,
            SabotageKind.Tampering => TamperMailKey,
            _ => "",
        };

        /// <summary>Register the three letter bodies in Data/Mail. Hooked from ModEntry.Entry.</summary>
        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Mail")) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                data[BlightMailKey] = Strings.Get("mail.darkness.blight.body") + "[#]" + Strings.Get("mail.darkness.blight.title");
                data[ReversionMailKey] = Strings.Get("mail.darkness.reversion.body") + "[#]" + Strings.Get("mail.darkness.reversion.title");
                data[TamperMailKey] = Strings.Get("mail.darkness.tamper.body") + "[#]" + Strings.Get("mail.darkness.tamper.title");
            }, AssetEditPriority.Default);
        }

        /// <summary>Queue the letter for a front's first strike on this save, if it has not gone
        /// out yet. Called from the morning report so the letter and the HUD line land together.</summary>
        public void SendFirstStrikeLetter(SabotageKind kind)
        {
            string key = MailKeyFor(kind);
            if (key.Length == 0) return;
            HashSet<string> sent = _meta.State.SabotageLettersSent ??= new HashSet<string>();
            if (sent.Contains(key)) return;
            if (Game1.mailbox.Contains(key) || Game1.player.mailReceived.Contains(key)) { sent.Add(key); return; }
            Game1.mailbox.Add(key);
            sent.Add(key);
            _meta.Save();
            _monitor.Log($"Darkness: first {kind} on this save, letter '{key}' delivered.", LogLevel.Info);
        }
    }
}
