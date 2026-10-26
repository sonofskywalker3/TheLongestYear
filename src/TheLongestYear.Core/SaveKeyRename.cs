using System;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>Maps a name keyed on the old save to the same name keyed on the new one. The rewind
/// gives the world a new <c>uniqueIDForThisGame</c>, so the save folder becomes
/// <c>&lt;farmer&gt;_&lt;newId&gt;</c>. Other mods name their per-save files after the folder
/// (SMAPI's <c>Constants.SaveFolderName</c>) or the id, so each such name is carried to the new
/// key: a name holding the old folder name gets the new folder name; failing that, a name holding
/// the old id as a whole number gets the new id.</summary>
public sealed class SaveKeyRename
{
    /// <summary>A bare id shorter than this is too likely to be some other number in a file name
    /// (a custom game seed can be "1"), so a short id only matches right after an underscore.</summary>
    public const int MinBareIdLength = 8;

    /// <summary>The vanilla save writes its temp files with this suffix.</summary>
    public const string VanillaTempSuffix = "_STARDEWVALLEYSAVETMP";

    private static readonly string[] VanillaInfoNames = { "SaveGameInfo", "SaveGameInfo_old" };

    /// <summary>Per-save files that record the old world rather than the player's choices. The
    /// rewind rebuilds the world, so these start fresh with it instead of being carried. Farm Type
    /// Manager's "&lt;save&gt;_SaveData.save" lists the objects it spawned, which it removes or
    /// puts back by tile; carried, it would put last year's spawns into the new Spring 1. (Its
    /// "&lt;save&gt;.json" farm settings are carried.)</summary>
    private static readonly string[] WorldStateSuffixes = { "_SaveData.save" };

    /// <summary>True for a file that records the old world (see <see cref="WorldStateSuffixes"/>).</summary>
    public static bool IsWorldState(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        foreach (string suffix in WorldStateSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private readonly string _oldFolder;
    private readonly string _newFolder;
    private readonly string _newId;
    private readonly Regex _oldIdToken;

    public string OldId { get; }

    public SaveKeyRename(string? oldFolderName, string oldId, string? newFolderName, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId))
            throw new ArgumentException("Both ids are required.");
        _oldFolder = oldFolderName ?? "";
        _newFolder = newFolderName ?? "";
        OldId = oldId;
        _newId = newId;
        string lead = oldId.Length >= MinBareIdLength ? @"(?<!\d)" : @"(?<=_)";
        _oldIdToken = new Regex(lead + Regex.Escape(oldId) + @"(?!\d)", RegexOptions.CultureInvariant);
    }

    /// <summary>The name keyed on the new save, or null when <paramref name="name"/> is not keyed
    /// on the old one.</summary>
    public string? Rename(string? name)
    {
        if (string.IsNullOrEmpty(name) || OldId == _newId)
            return null;
        if (_oldFolder.Length > 0 && _newFolder.Length > 0 && name.Contains(_oldFolder, StringComparison.Ordinal))
            return name.Replace(_oldFolder, _newFolder, StringComparison.Ordinal);
        if (_oldIdToken.IsMatch(name))
            return _oldIdToken.Replace(name, _newId);
        return null;
    }

    /// <summary>True for the files the game itself writes into a save folder: the main file
    /// (<c>&lt;farmer&gt;_&lt;id&gt;</c>, letters and digits before the id), its <c>_old</c>
    /// copy, SaveGameInfo and its <c>_old</c> copy, and the save's temp files. Anything else in
    /// the folder belongs to a mod.</summary>
    public static bool IsVanillaSaveFile(string? fileName, string? saveId)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        if (Array.IndexOf(VanillaInfoNames, fileName) >= 0)
            return true;
        if (fileName.EndsWith(VanillaTempSuffix, StringComparison.Ordinal))
            return true;
        if (string.IsNullOrEmpty(saveId))
            return false;

        string stem = fileName.EndsWith("_old", StringComparison.Ordinal) ? fileName[..^4] : fileName;
        string idTail = "_" + saveId;
        if (!stem.EndsWith(idTail, StringComparison.Ordinal))
            return false;
        string farmer = stem[..^idTail.Length];
        foreach (char c in farmer)
        {
            if (!char.IsLetterOrDigit(c))
                return false;
        }
        return true;
    }
}
