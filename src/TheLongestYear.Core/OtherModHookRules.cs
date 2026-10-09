using System;

namespace TheLongestYear.Core;

/// <summary>Which methods a TLY transpiler may follow into when another mod has rewritten a game
/// method it patches. Another mod (ExtraAnimalConfig, Better Pigs) often swaps the vanilla call TLY
/// anchors on for a helper of its own that still asks the game the same question inside. TLY then
/// hooks that helper instead of failing. Only helpers that belong to another mod are followed: the
/// game, SMAPI, Harmony, the runtime and TLY itself never are.</summary>
public static class OtherModHookRules
{
    /// <summary>How many calls deep a search follows from the patched game method into another
    /// mod's helpers (Better Pigs: canGoOutside, then CanAnimalGoOutsideInThisWeather).</summary>
    public const int MaxHelperDepth = 3;

    private static readonly string[] NotModAssemblies =
    {
        "Stardew Valley", "StardewValley.GameData", "StardewModdingAPI", "MonoGame.Framework",
        "xTile", "Netcode", "0Harmony", "mscorlib", "netstandard", "Newtonsoft.Json",
        "Lidgren.Network", "GalaxyCSharp", "Steamworks.NET", "BmFont", "TextCopy",
    };

    private static readonly string[] NotModPrefixes =
    {
        "System.", "Microsoft.", "SMAPI.", "StardewModdingAPI.", "MonoMod.", "Mono.", "TheLongestYear",
    };

    /// <summary>True when an assembly with this name belongs to another mod, so its methods may be
    /// followed and hooked. False for the game, its libraries, SMAPI, Harmony, .NET and TLY.</summary>
    public static bool IsOtherModAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName)) return false;
        if (assemblyName == "System") return false;
        foreach (string name in NotModAssemblies)
            if (string.Equals(assemblyName, name, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (string prefix in NotModPrefixes)
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
