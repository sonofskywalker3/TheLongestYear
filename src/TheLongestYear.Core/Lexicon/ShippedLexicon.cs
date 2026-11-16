using System.IO;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The answer file shipped with the mod: the unmodded game's answers, built in-game by
/// <c>tly_lexicon write vanilla</c> and read at save load.</summary>
public static class ShippedLexicon
{
    /// <summary>Where the vanilla file sits under the mod folder.</summary>
    public const string VanillaRelativePath = "assets/lexicon/vanilla.json";

    public static string VanillaPath(string modFolder)
        => Path.Combine(modFolder, "assets", "lexicon", "vanilla.json");

    public static string VanillaIdsPath(string modFolder)
        => Path.Combine(modFolder, "assets", "lexicon", "vanilla.ids.json");

    /// <summary>The unmodded game's known ids (<see cref="KnownIds"/>), written in-game by
    /// <c>tly_lexicon ids vanilla</c>; null when the file is absent.</summary>
    public static System.Collections.Generic.IReadOnlyList<string>? LoadVanillaIds(string modFolder)
    {
        string path = VanillaIdsPath(modFolder);
        return File.Exists(path) ? LayerFile.ReadIds(File.ReadAllText(path)) : null;
    }

    /// <summary>The shipped vanilla set, or null when the file (or the folder) is absent. A corrupt
    /// file throws <see cref="System.Text.Json.JsonException"/>; the caller decides what that means.</summary>
    public static AnswerSet? LoadVanilla(string modFolder)
    {
        string path = VanillaPath(modFolder);
        return File.Exists(path) ? LexiconJson.Read(File.ReadAllText(path)) : null;
    }
}
