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
            // A failed backup no longer aborts the reset (Jeff, 2026-10-10): an aborted rewind left the
            // player in the next season with the failed gate never applied, a free gate clear. The
            // backup is one-time insurance; BackupDone stays false so the next rewind tries again.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                monitor.Log($"Save backup failed ({ex.Message}); the rewind goes ahead without it and the next rewind retries.", LogLevel.Warn);
                RemovePartialCopy(dest, monitor);
            }
        }

        /// <summary>A copy that failed partway is not a backup: left in the backups folder it reads
        /// like one, and a player restoring from it would get a save with files missing.</summary>
        private static void RemovePartialCopy(string dest, IMonitor monitor)
        {
            try
            {
                if (Directory.Exists(dest))
                    Directory.Delete(dest, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                monitor.Log($"Save backup: could not remove the partial copy at {dest} ({ex.Message}).", LogLevel.Warn);
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
