using System;
using System.IO;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// One-time backup of the current save folder, taken before the first destructive reset. Gated by
    /// <see cref="MetaState.BackupDone"/> (persisted on the next Saving) so it runs exactly once.
    /// PC path via SMAPI's Constants.CurrentSavePath; the Android port is deferred.
    /// </summary>
    internal static class SaveBackup
    {
        public static void BackupOnce(MetaState meta, IMonitor monitor)
        {
            if (meta.BackupDone)
                return;

            string savePath = Constants.CurrentSavePath;
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath))
            {
                monitor.Log("Save backup skipped: no current save folder found.", LogLevel.Warn);
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string dest = $"{savePath}_TLY_BACKUP_{stamp}";

            try
            {
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
