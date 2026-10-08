using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Every qualified item id the UNMODDED game defines ("(O)24", "(BC)10", "(H)27"...),
/// baked from the game's own Content folder by <c>tools/vanilla-ids</c> (the id list lives in
/// VanillaItemIds.Generated.cs; regenerate it after a game update).
///
/// TLY Custom boards (BundleSource Engine) ask only for these items and give only these items as
/// rewards (spec 2026-10-08-custom-board-vanilla-only). A prefix rule cannot tell vanilla from
/// modded: SMAPI mod items are usually "Author.Mod_Item", but unprefixed mod ids exist, and 1.6's
/// own string ids (Moss, Goby, the jellies, the books) carry no number. A baked list is the only
/// test that is deterministic, unaffected by any mod's asset edits, and testable.</summary>
public static partial class VanillaItemIds
{
    /// <summary>The baked id set, ordinal.</summary>
    public static readonly IReadOnlySet<string> All;

    // A static constructor rather than a field initializer: the ids live in the generated half of
    // this partial class, and field initializers in different files run in no defined order.
    static VanillaItemIds()
    {
        All = new HashSet<string>(_ids, StringComparer.Ordinal);
    }

    /// <summary>Whether a bundle ingredient ref names a vanilla item. A bare id is read as an
    /// object ("24" is "(O)24"), the same way the game reads a Data/Bundles ingredient.</summary>
    public static bool Contains(string itemRef)
        => !string.IsNullOrEmpty(itemRef) && All.Contains(BundleParsing.NormalizeItemId(itemRef));
}

/// <summary>The unmodded game's standard board (Data/Bundles), baked by <c>tools/vanilla-ids</c>
/// into VanillaBundleBoard.Generated.cs. A TLY Custom board falls back to it where another bundle
/// mod's template leaves a position with no vanilla bundle to draw.</summary>
public static partial class VanillaBundleBoard
{
    /// <summary>"Room/index" to the slash-delimited bundle string, ordinal.</summary>
    public static readonly IReadOnlyDictionary<string, string> Standard;

    static VanillaBundleBoard()
    {
        var board = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in _entries)
            board[key] = value;
        Standard = board;
    }
}
