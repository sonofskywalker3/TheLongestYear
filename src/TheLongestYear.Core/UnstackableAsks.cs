using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Items that never stack, so a bundle slot may never ask for more than one of them.
/// The game only accepts a deposit whose stack covers the whole ask (Bundle.canAcceptThisItem:
/// ingredient.stack &lt;= item.Stack), and a hat, a weapon or a ring is always a stack of one.
///
/// Nexus bug report, 2026-09-14: the Stack size dial on Hard turned Gil's Trophies' Skeleton Mask
/// x1 into x2, which no player can ever deposit, and it blocked the season. Gil gives each
/// trophy once per loop anyway, so one is the only ask that makes sense.</summary>
public static class UnstackableAsks
{
    private const string ObjectQualifier = "(O)";

    /// <summary>Every vanilla ring. Rings are Objects by id but never stack, and a Ring instance is
    /// not an Object at runtime. Gil's trophy rings are here too, via <see cref="AuthoredBundleCatalog.GilTrophies"/>.
    /// An Amethyst Ring x2 on a Hard Dye bundle (Nexus, 2026-09-14) is why this list exists.</summary>
    public static readonly System.Collections.Generic.IReadOnlySet<string> VanillaRingIds = new System.Collections.Generic.HashSet<string>(
        new[] { "516", "517", "518", "519", "520", "521", "522", "523", "524", "525", "526", "527", "528",
                "529", "530", "531", "532", "533", "534", "810", "811", "839", "859", "860", "862", "863",
                "887", "888" }.Select(id => ObjectQualifier + id),
        StringComparer.Ordinal);

    /// <summary>True for any qualified non-Object item ((H) hats, (W) weapons and the like) and for
    /// Gil's trophy rings, which are Objects that still never stack.</summary>
    public static bool IsUnstackable(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemId[0] != '(')
            return false;
        return !itemId.StartsWith(ObjectQualifier, StringComparison.Ordinal)
               || AuthoredBundleCatalog.GilTrophies.Contains(itemId)
               || VanillaRingIds.Contains(itemId);
    }

    /// <summary>One for an unstackable item, the stack unchanged for anything else.</summary>
    public static int ClampStack(string? itemId, int stack)
        => IsUnstackable(itemId) && stack > 1 ? 1 : stack;

    /// <summary>Repairs one live BundleData value written before this rule existed: every
    /// unstackable ask above one comes back as one, every other field survives byte for byte.
    /// Null when nothing needed changing.</summary>
    public static string? RepairBundleValue(string value)
        => BundleAskRewrite.LowerAsks(value, ClampStack);
}
