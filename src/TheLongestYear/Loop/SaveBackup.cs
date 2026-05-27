using System;
using System.IO;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// One-time backup of the current save folder, taken before the first destructive reset. Gated by
    /// <see cref="MetaState.BackupDone"/> (persisted on the next Saving) so it runs exactly once per save.
    /// PC path via SMAPI's Constants.CurrentSavePath; the Android port is deferred.
    ///
    /// 2026-05-27: backups are written into the mod's own folder (e.g.
    /// <c>Mods/TheLongestYear/backups/&lt;saveName&gt;_&lt;timestamp&gt;/</c>), NOT into the
    /// Stardew Saves directory. The previous destination (a sibling folder inside Saves) was
    /// being enumerated by Stardew's title screen as a second save — user reported "I keep
    /// having 2 saves every time I reopen the game." Putting the backup outside the Saves
    /// directory keeps the title-screen save list clean.
    /// </summary>
    internal static class SaveBackup
    {
        public static void BackupOnce(MetaState meta, IMonitor monitor, string modDirectory)
        {
            if (meta.BackupDone)
                return;

            string savePath = Constants.CurrentSavePath;
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath))
            {
                monitor.Log("Save backup skipped: no current save folder found.", LogLevel.Warn);
                return;
            }

            if (string.IsNullOrEmpty(modDirectory))
            {
                monitor.Log("Save backup skipped: mod directory not available.", LogLevel.Warn);
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string saveName = new DirectoryInfo(savePath).Name;
            string backupRoot = Path.Combine(modDirectory, "backups");
            string dest = Path.Combine(backupRoot, $"{saveName}_{stamp}");

            try
            {
                Directory.CreateDirectory(backupRoot);
                CopyDirectory(savePath, dest);
                meta.BackupDone = true;
                monitor.Log($"One-time save backup written to: {dest}", LogLevel.Info);
            }
            catch (IOException ex)
            {
                monitor.Log($"Save backup FAILED ({ex.Message}); reset aborted to protect the save.", LogLevel.Error);
                throw;
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
