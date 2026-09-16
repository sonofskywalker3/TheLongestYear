using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Items a loop can only give once, so a slot asking for two of them is impossible rather
/// than hard. One setting governs them all (<see cref="GameplayConfig.OncePerLoopAsksOne"/>,
/// Jeff, 2026-09-16): on, every one of them asks for one at every Stack size step; off, they
/// scale like anything else.
///
/// The list was checked against the exported game data on 2026-09-16, over the 724 objects the
/// pools can draw from, with every repeatable route subtracted (spawns, crops, machines, monsters,
/// geodes, recipes, unlimited shop rows):
/// <list type="bullet">
/// <item>The five legendary fish: catch limit of one per loop (<see cref="LegendaryFishRules"/>;
/// <see cref="CaughtFishReset"/> clears the record on the rewind).</item>
/// <item>The Alleyway Buffet and Mapping Cave Systems: one gift box each, and the bookseller only
/// restocks them from year 3. FayGabi's Hard Book bundle asked for two (Nexus, 2026-09-14).</item>
/// <item>The Golden Pumpkin: one chest in the Spirit's Eve maze per year. Luck can add more
/// (Mystery Boxes, Artifact Troves, the cart's random stock), but nothing a player can plan on.</item>
/// </list>
/// The other nine books restock every bookseller visit or daily at the Dwarf; weapons, hats and
/// rings never stack and are <see cref="UnstackableAsks"/>, an unconditional rule; rarecrows are
/// big craftables and never reach a board; the Pearl is excluded from every pool.</summary>
public static class OncePerLoopAsks
{
    private static readonly string[] HandListed =
    {
        "(O)Book_Trash",   // The Alleyway Buffet
        "(O)Book_Marlon",  // Mapping Cave Systems
        "(O)373",          // Golden Pumpkin
    };

    /// <summary>Every once-per-loop id, qualified.</summary>
    public static readonly IReadOnlySet<string> Ids =
        new HashSet<string>(LegendaryFishRules.Ids.Concat(HandListed), StringComparer.Ordinal);

    public static bool IsOncePerLoop(string? itemId)
        => itemId != null && Ids.Contains(BundleParsing.NormalizeItemId(itemId));

    /// <summary>One for a once-per-loop item while the setting is on; the stack unchanged otherwise.</summary>
    public static int ClampStack(string? itemId, int stack, bool enabled)
        => enabled && IsOncePerLoop(itemId) && stack > 1 ? 1 : stack;

    /// <summary>Repairs one live BundleData value written before this rule existed, or while the
    /// setting was off: every once-per-loop ask above one comes back as one, every other field
    /// survives byte for byte. Null when nothing needed changing.</summary>
    public static string? RepairBundleValue(string value)
        => BundleAskRewrite.LowerAsks(value, (id, stack) => ClampStack(id, stack, enabled: true));
}
