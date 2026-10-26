using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Keeps other mods' per-save data through the rewind. The rewind gives the world a new
    /// <c>uniqueIDForThisGame</c> (fresh forage, cart, weather and bundle rolls each year), and the
    /// save folder is named after it, so the save lands in a new folder. What survives on its own:
    /// SMAPI save data (<c>WriteSaveData</c> lives in <c>Game1.CustomData</c>, which loadForNewGame
    /// keeps) and the farmer's modData (the Farmer is kept). What did not: files a mod names after
    /// the save folder or the id (UI Info Suite 2 keeps its settings in
    /// <c>data/&lt;SaveFolderName&gt;.json</c> and reads them on SaveLoaded, which SMAPI raises
    /// again after the rewind), and files a mod keeps inside the save folder, deleted with the old
    /// folder. Both are copied to the new key before SMAPI raises that SaveLoaded; the old-keyed
    /// copies are deleted with the old save folder, once the new save is on disk.
    /// </summary>
    internal static class ModSaveDataCarryover
    {
        /// <summary>SMAPI's global mod data (<c>helper.Data.WriteGlobalData</c>) lives here, one
        /// folder per mod.</summary>
        private static readonly string[] GlobalModDataPath = { ".smapi", "mod-data" };

        /// <summary>Copy every other mod's data keyed on the old save to the new key. Call right
        /// after the id changes. Returns what was copied, for <see cref="DeleteOldCopies"/>.</summary>
        public static SaveDataCarryResult Carry(
            IMonitor monitor, string oldSaveFolder, ulong oldId, IEnumerable<string> modFolders, string ownModFolder)
        {
            var result = new SaveDataCarryResult();
            string oldName = !string.IsNullOrEmpty(oldSaveFolder) ? Path.GetFileName(oldSaveFolder) : null;
            string newName = SaveGame.FilterFileName(Game1.GetSaveGameName(set_value: false)) + "_" + Game1.uniqueIDForThisGame;
            var rename = new SaveKeyRename(oldName, oldId.ToString(), newName, Game1.uniqueIDForThisGame.ToString());

            var roots = new List<string>(modFolders ?? Enumerable.Empty<string>())
            {
                Path.Combine(Constants.DataPath, Path.Combine(GlobalModDataPath)),
            };
            var excluded = new[] { ownModFolder, Constants.SavesPath };
            ModSaveDataCopier.CarryKeyedEntries(roots, rename, excluded, result);

            string newSaveFolder = Path.Combine(Constants.SavesPath, newName);
            ModSaveDataCopier.CarrySaveFolderExtras(oldSaveFolder, newSaveFolder, rename, result);

            foreach (string copy in result.Copies)
                monitor.Log($"In-place reset: carried another mod's save data to the new save: {copy}", LogLevel.Trace);
            foreach (string error in result.Errors)
                monitor.Log($"In-place reset: could not carry another mod's save data ({error}). That mod may start this year with fresh settings.", LogLevel.Warn);
            monitor.Log(
                $"In-place reset: carried {result.Copies.Count} other-mod save file(s) from {oldName ?? oldId.ToString()} to {newName}.",
                LogLevel.Info);
            return result;
        }

        /// <summary>Delete the old-keyed copies. Call only after the new save is confirmed, like
        /// the old save folder itself: until then they belong to the only loadable copy.</summary>
        public static void DeleteOldCopies(IMonitor monitor, SaveDataCarryResult carried)
        {
            if (carried == null || carried.CopiedSources.Count == 0)
                return;
            var errors = new List<string>();
            int deleted = ModSaveDataCopier.DeleteSources(carried.CopiedSources, errors);
            foreach (string error in errors)
                monitor.Log($"In-place reset: could not delete another mod's old save data ({error}); it is safe to delete manually.", LogLevel.Warn);
            monitor.Log($"In-place reset: deleted {deleted} other-mod save file(s) keyed on the abandoned save.", LogLevel.Trace);
        }

        /// <summary>Every loaded mod's folder. SMAPI's IModInfo does not expose it, but every
        /// entry it hands out is its ModMetadata, which has a public DirectoryPath.</summary>
        public static List<string> LoadedModFolders(IModRegistry registry, IMonitor monitor)
        {
            var folders = new List<string>();
            foreach (IModInfo mod in registry.GetAll())
            {
                try
                {
                    if (mod.GetType().GetProperty("DirectoryPath")?.GetValue(mod) is string path && path.Length > 0)
                        folders.Add(path);
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    monitor.Log($"In-place reset: could not read the folder of mod {mod.Manifest?.UniqueID}: {ex.InnerException?.Message}", LogLevel.Trace);
                }
            }
            return folders;
        }
    }
}
