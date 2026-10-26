using System;
using System.Collections.Generic;
using System.IO;

namespace TheLongestYear.Core;

/// <summary>What one <see cref="ModSaveDataCopier"/> pass did.</summary>
public sealed class SaveDataCarryResult
{
    /// <summary>Mod files and folders keyed on the old save that were copied; deleted once the
    /// new save is confirmed on disk.</summary>
    public List<string> CopiedSources { get; } = new();

    /// <summary>"source -> target" for every copy made, for the log.</summary>
    public List<string> Copies { get; } = new();

    /// <summary>Copies that failed, for the log. A failure only loses that one mod's data.</summary>
    public List<string> Errors { get; } = new();
}

/// <summary>Carries other mods' per-save data from the old save key to the new one across the
/// rewind. Plain file system work (no game types), so the tests run it on temp folders.</summary>
public static class ModSaveDataCopier
{
    /// <summary>Walk each root (a mod's folder, SMAPI's global mod-data folder) and copy every file
    /// or folder whose name is keyed on the old save to the same name keyed on the new save, next
    /// to it. A keyed folder is copied whole and not searched further. An existing target is left
    /// alone (that mod already wrote the new save's data). Paths under
    /// <paramref name="excluded"/> are skipped.</summary>
    public static void CarryKeyedEntries(
        IEnumerable<string> roots, SaveKeyRename rename, IEnumerable<string> excluded, SaveDataCarryResult result)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in excluded)
        {
            if (!string.IsNullOrEmpty(path))
                skip.Add(Normalize(path));
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;
            Walk(Normalize(root), rename, skip, seen, result);
        }
    }

    /// <summary>Copy everything in the old save folder that the game did not write itself (files
    /// a mod keeps beside the save) into the new save folder, renaming names keyed on the old
    /// save. The game writes its own files on the next save; the old folder is deleted after
    /// that save, so without this the mod files would go with it.</summary>
    public static void CarrySaveFolderExtras(
        string oldSaveFolder, string newSaveFolder, SaveKeyRename rename, SaveDataCarryResult result)
    {
        if (string.IsNullOrEmpty(oldSaveFolder) || string.IsNullOrEmpty(newSaveFolder) || !Directory.Exists(oldSaveFolder))
            return;
        if (string.Equals(Normalize(oldSaveFolder), Normalize(newSaveFolder), StringComparison.OrdinalIgnoreCase))
            return;

        foreach (string entry in SafeEntries(oldSaveFolder, result))
        {
            string name = Path.GetFileName(entry);
            bool isDir = Directory.Exists(entry);
            if (!isDir && SaveKeyRename.IsVanillaSaveFile(name, rename.OldId))
                continue;
            string target = Path.Combine(newSaveFolder, rename.Rename(name) ?? name);
            Copy(entry, target, isDir, result, recordSource: false);
        }
    }

    /// <summary>Delete the copied sources once the new save is confirmed. Returns how many went.</summary>
    public static int DeleteSources(IEnumerable<string> sources, ICollection<string> errors)
    {
        int deleted = 0;
        foreach (string path in sources)
        {
            try
            {
                if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); deleted++; }
                else if (File.Exists(path)) { File.Delete(path); deleted++; }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }
        return deleted;
    }

    private static void Walk(string dir, SaveKeyRename rename, HashSet<string> skip, HashSet<string> seen, SaveDataCarryResult result)
    {
        if (skip.Contains(dir) || !seen.Add(dir))
            return;
        foreach (string entry in SafeEntries(dir, result))
        {
            string full = Normalize(entry);
            if (skip.Contains(full))
                continue;
            bool isDir = Directory.Exists(full);
            if (isDir && IsLink(full))
                continue;

            string? renamed = rename.Rename(Path.GetFileName(full));
            if (renamed != null)
            {
                Copy(full, Path.Combine(dir, renamed), isDir, result, recordSource: true);
                continue;
            }
            if (isDir)
                Walk(full, rename, skip, seen, result);
        }
    }

    private static void Copy(string source, string target, bool isDir, SaveDataCarryResult result, bool recordSource)
    {
        if (File.Exists(target) || Directory.Exists(target))
            return;
        try
        {
            if (isDir) CopyDirectory(source, target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
            result.Copies.Add($"{source} -> {target}");
            if (recordSource)
                result.CopiedSources.Add(source);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            result.Errors.Add($"{source}: {ex.Message}");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(source))
        {
            if (!IsLink(dir))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    private static IEnumerable<string> SafeEntries(string dir, SaveDataCarryResult result)
    {
        try
        {
            return Directory.GetFileSystemEntries(dir);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            result.Errors.Add($"{dir}: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool IsLink(string dir)
        => (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0;

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
