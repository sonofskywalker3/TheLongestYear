using System.IO;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>The settings copy kept outside the mod folder, in SMAPI's per-mod global data
        /// (<c>%AppData%/StardewValley/.smapi/mod-data/&lt;mod id&gt;/</c>).</summary>
        private const string ConfigBackupKey = "config-backup";

        /// <summary>Reads config.json, restoring it from the backup first when it is missing.
        /// Mod managers (Vortex and others) replace the whole mod folder on an update, which took
        /// config.json with it and put every setting back to default (Nexus bug, 2026-10-23).</summary>
        private GameplayConfig ReadConfigWithBackup(IModHelper helper)
        {
            bool configExists = File.Exists(Path.Combine(helper.DirectoryPath, "config.json"));
            if (!configExists && helper.Data.ReadGlobalData<GameplayConfig>(ConfigBackupKey) is { } backup)
            {
                helper.WriteConfig(backup);
                this.Monitor.Log(
                    "config.json was missing (a mod manager replaces the mod folder on update); your settings were restored from the backup copy.",
                    LogLevel.Info);
            }
            return helper.ReadConfig<GameplayConfig>();
        }

        /// <summary>Writes config.json and the backup copy together. Every settings write goes
        /// through here so the backup never falls behind.</summary>
        private void WriteConfigWithBackup()
        {
            this.Helper.WriteConfig(_config);
            this.Helper.Data.WriteGlobalData(ConfigBackupKey, _config);
        }
    }
}
