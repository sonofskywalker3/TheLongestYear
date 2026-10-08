// Generates the baked vanilla content TLY Custom boards are limited to (spec
// docs/superpowers/specs/2026-10-08-custom-board-vanilla-only-design.md):
//
//   VanillaItemIds.Generated.cs     every qualified item id the unmodded game defines, for every
//                                   item type a bundle can ask for or give as a reward.
//   VanillaBundleBoard.Generated.cs the unmodded Data/Bundles (the standard board), used when a
//                                   bundle mod's template leaves a board position with nothing.
//
// Reads the game's own Content folder through a bare MonoGame ContentManager: no SMAPI runs, so
// no mod's asset edit can reach what this reads. Re-run after every game update:
//
//   dotnet run --project tools/vanilla-ids -- "<game folder>" src/TheLongestYear.Core
//
// Output is sorted ordinally, so a re-run on the same game version produces no diff.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using StardewValley.GameData;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Pants;
using StardewValley.GameData.Shirts;
using StardewValley.GameData.Tools;
using StardewValley.GameData.Weapons;

const string DefaultGame = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley";
const int IdsPerLine = 8;

string game = args.Length > 0 ? args[0] : DefaultGame;
string outDir = args.Length > 1 ? args[1] : Path.Combine("src", "TheLongestYear.Core");
var content = new ContentManager(new GameServiceContainer(), Path.Combine(game, "Content"));

// Qualifier -> the ids that type's data asset defines. Every type ItemRegistry knows that a
// bundle slot or a bundle reward string can name ("O", "R", "BL" -> (O); "BO", "BBL" -> (BC);
// "F"; "H"; "W"; "B"; "C" -> (S)/(P)), plus tools and trinkets so no item type is left out.
var types = new (string Prefix, string Asset, IEnumerable<string> Ids)[]
{
    ("(O)", "Data/Objects", content.Load<Dictionary<string, ObjectData>>("Data/Objects").Keys),
    ("(BC)", "Data/BigCraftables", content.Load<Dictionary<string, BigCraftableData>>("Data/BigCraftables").Keys),
    ("(F)", "Data/Furniture", content.Load<Dictionary<string, string>>("Data/Furniture").Keys),
    ("(H)", "Data/hats", content.Load<Dictionary<string, string>>("Data/hats").Keys),
    ("(W)", "Data/Weapons", content.Load<Dictionary<string, WeaponData>>("Data/Weapons").Keys),
    ("(B)", "Data/Boots", content.Load<Dictionary<string, string>>("Data/Boots").Keys),
    ("(S)", "Data/Shirts", content.Load<Dictionary<string, ShirtData>>("Data/Shirts").Keys),
    ("(P)", "Data/Pants", content.Load<Dictionary<string, PantsData>>("Data/Pants").Keys),
    ("(T)", "Data/Tools", content.Load<Dictionary<string, ToolData>>("Data/Tools").Keys),
    ("(TR)", "Data/Trinkets", content.Load<Dictionary<string, TrinketData>>("Data/Trinkets").Keys),
};

string version = ReadGameVersion(game);
var ids = new SortedSet<string>(StringComparer.Ordinal);
var counts = new List<string>();
foreach ((string prefix, string asset, IEnumerable<string> keys) in types)
{
    int before = ids.Count;
    foreach (string key in keys)
        ids.Add(prefix + key);
    counts.Add($"{prefix} {ids.Count - before} ({asset})");
}

var sb = new StringBuilder();
AppendHeader(sb, version, "the qualified id of every item the unmodded game defines.");
sb.AppendLine("// Counts: " + string.Join(", ", counts) + $"; {ids.Count} in all.");
sb.AppendLine("using System;");
sb.AppendLine("using System.Collections.Generic;");
sb.AppendLine();
sb.AppendLine("namespace TheLongestYear.Core;");
sb.AppendLine();
sb.AppendLine("public static partial class VanillaItemIds");
sb.AppendLine("{");
sb.AppendLine($"    /// <summary>The game version these ids were read from.</summary>");
sb.AppendLine($"    public const string GameVersion = \"{version}\";");
sb.AppendLine();
sb.AppendLine("    private static readonly string[] _ids =");
sb.AppendLine("    {");
foreach (string[] line in ids.Chunk(IdsPerLine))
    sb.AppendLine("        " + string.Join(" ", line.Select(id => Literal(id) + ",")));
sb.AppendLine("    };");
sb.AppendLine("}");
Write(Path.Combine(outDir, "VanillaItemIds.Generated.cs"), sb);

var board = content.Load<Dictionary<string, string>>("Data/Bundles");
sb.Clear();
AppendHeader(sb, version, "the unmodded Data/Bundles (the game's standard board), in English.");
sb.AppendLine("using System;");
sb.AppendLine("using System.Collections.Generic;");
sb.AppendLine();
sb.AppendLine("namespace TheLongestYear.Core;");
sb.AppendLine();
sb.AppendLine("public static partial class VanillaBundleBoard");
sb.AppendLine("{");
sb.AppendLine("    private static readonly (string Key, string Value)[] _entries =");
sb.AppendLine("    {");
foreach (KeyValuePair<string, string> entry in board.OrderBy(e => e.Key, StringComparer.Ordinal))
    sb.AppendLine($"        ({Literal(entry.Key)}, {Literal(entry.Value)}),");
sb.AppendLine("    };");
sb.AppendLine("}");
Write(Path.Combine(outDir, "VanillaBundleBoard.Generated.cs"), sb);

Console.WriteLine($"Stardew Valley {version}: {ids.Count} item ids ({string.Join(", ", counts)}); {board.Count} bundles.");

static void AppendHeader(StringBuilder sb, string version, string what)
{
    sb.AppendLine("// <auto-generated>");
    sb.AppendLine($"// GENERATED from Stardew Valley {version}'s unmodded Content folder: {what}");
    sb.AppendLine("// Do not edit by hand. Regenerate after a game update with:");
    sb.AppendLine("//   dotnet run --project tools/vanilla-ids -- \"<game folder>\" src/TheLongestYear.Core");
    sb.AppendLine("// </auto-generated>");
}

static string Literal(string text)
    => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

static void Write(string path, StringBuilder sb)
{
    File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"));
    Console.WriteLine("wrote " + path);
}

static string ReadGameVersion(string game)
{
    // "Stardew Valley/1.6.15.24356" in the deps file names the build this content shipped with.
    string deps = Path.Combine(game, "Stardew Valley.deps.json");
    if (!File.Exists(deps)) return "unknown";
    const string Marker = "\"Stardew Valley/";
    string text = File.ReadAllText(deps);
    int start = text.IndexOf(Marker, StringComparison.Ordinal);
    if (start < 0) return "unknown";
    start += Marker.Length;
    int end = text.IndexOf('"', start);
    return end < 0 ? "unknown" : text.Substring(start, end - start);
}
