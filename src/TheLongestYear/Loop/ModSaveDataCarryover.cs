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
    /// folder.
    ///
    /// Files keyed by name are copied to the new key right after the id changes, before SMAPI
    /// raises that SaveLoaded. Files inside the save folder are copied only after the new save is
    /// written: creating the new folder early makes the game think the name is taken, and it
    /// saves to "&lt;farm&gt;2_&lt;id&gt;" instead (seen live 2026-10-26). The old-keyed copies
    /// are deleted with the old save folder, once the new save is on disk.
    /// </summary>
    internal static class ModSaveDataCarryover
    {
        /// <summary>SMAPI's global mod data (<c>helper.Data.WriteGlobalData</c>) lives here, one
        /// folder per mod.</summary>
        private static readonly string[] GlobalModDataPath = { ".smapi", "mod-data" };

        /// <summary>One rewind's carry: what was copied, and what the post-save step needs.</summary>
        internal sealed class Carried
        {
            public SaveDataCarryResult Result { get; } = new();
            public SaveKeyRename Rename { get; init; }
            public string OldSaveFolder { get; init; }
            public string NewName { get; init; }
        }

        /// <summary>Copy every other mod's data keyed on the old save to the new key. Call right
        /// after the id changes, before loadForNewGame (the farm name is read from the master
        /// player, which loadForNewGame keeps).</summary>
        public static Carried Carry(
            IMonitor monitor, string oldSaveFolder, ulong oldId, IEnumerable<string> modFolders, string ownModFolder)
        {
            string oldName = !string.IsNullOrEmpty(oldSaveFolder) ? Path.GetFileName(oldSaveFolder) : null;
            // loadForNewGame clears the game's cached save name, and the next save names its folder
            // from the farm name again: <farm>_<id>, the same name SMAPI's SaveFolderName gives.
            string newName = SaveGame.FilterFileName(Game1.MasterPlayer.farmName.Value) + "_" + Game1.uniqueIDForThisGame;
            var carried = new Carried
            {
                Rename = new SaveKeyRename(oldName, oldId.ToString(), newName, Game1.uniqueIDForThisGame.ToString()),
                OldSaveFolder = oldSaveFolder,
                NewName = newName,
            };

            var roots = new List<string>(modFolders ?? Enumerable.Empty<string>())
            {
                Path.Combine(Constants.DataPath, Path.Combine(GlobalModDataPath)),
            };
            var excluded = new[] { ownModFolder, Constants.SavesPath };
            ModSaveDataCopier.CarryKeyedEntries(roots, carried.Rename, excluded, carried.Result);

            Report(monitor, carried.Result, $"from {oldName ?? oldId.ToString()} to {newName}");
            return carried;
        }

        /// <summary>After the new save is confirmed and before the old folder is deleted: copy the
        /// mod files kept inside the old save folder into the folder the save just wrote, then
        /// delete the old-keyed copies made by <see cref="Carry"/>.</summary>
        public static void FinishAfterSave(IMonitor monitor, Carried carried)
        {
            if (carried == null)
                return;

            string newSaveFolder = Constants.CurrentSavePath;
            if (!string.IsNullOrEmpty(newSaveFolder) && !string.IsNullOrEmpty(carried.OldSaveFolder))
            {
                var extras = new SaveDataCarryResult();
                ModSaveDataCopier.CarrySaveFolderExtras(carried.OldSaveFolder, newSaveFolder, carried.Rename, extras);
                if (extras.Copies.Count > 0 || extras.Errors.Count > 0)
                    Report(monitor, extras, $"from the old save folder into {Path.GetFileName(newSaveFolder)}");
            }

            if (carried.Result.CopiedSources.Count == 0)
                return;
            var errors = new List<string>();
            int deleted = ModSaveDataCopier.DeleteSources(carried.Result.CopiedSources, errors);
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

        private static void Report(IMonitor monitor, SaveDataCarryResult result, string what)
        {
            foreach (string copy in result.Copies)
                monitor.Log($"In-place reset: carried another mod's save data to the new save: {copy}", LogLevel.Trace);
            foreach (string error in result.Errors)
                monitor.Log($"In-place reset: could not carry another mod's save data ({error}). That mod may start this year with fresh settings.", LogLevel.Warn);
            monitor.Log($"In-place reset: carried {result.Copies.Count} other-mod save file(s) {what}.", LogLevel.Info);
        }
    }
}
