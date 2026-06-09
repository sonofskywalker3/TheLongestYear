using System;
using System.Collections.Generic;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Treats Community Center egg color-variants as interchangeable for weekly-quest credit and
    /// the selection bonus. SDV ships white/brown variants that share a DisplayName: 174/182 both
    /// "Large Egg" and 176/180 both "Egg". A weekly bonus item sampled as one color must be
    /// satisfiable by donating EITHER color — otherwise the player donates a "Large Egg" and the
    /// journal won't tick because it wanted the other shade (khauser13, Nexus 2026-06-08; user
    /// directive 2026-06-09: "it needs to accept EITHER egg, not a specific color"). Each variant
    /// folds to a canonical id so equality checks collapse the pair. Anything else passes through
    /// unchanged, so this only affects the two egg pairs.
    /// </summary>
    public static class CcItemEquivalence
    {
        // Variant id (bare) -> canonical id (bare). Only egg color-pairs; everything else is
        // identity. Canonical choice is arbitrary (the white id) — it just has to be stable.
        private static readonly Dictionary<string, string> CanonById = new(StringComparer.Ordinal)
        {
            ["182"] = "174", // Large Egg (brown) -> Large Egg (white)
            ["180"] = "176", // Egg (brown)       -> Egg (white)
        };

        /// <summary>Fold a qualified ("(O)182") or bare ("182") item id to its canonical bare id.</summary>
        public static string Canonical(string itemId)
        {
            string bare = Bare(itemId);
            return CanonById.TryGetValue(bare, out string? canon) ? canon : bare;
        }

        /// <summary>True if two item ids are the same item OR interchangeable egg color-variants.</summary>
        public static bool Matches(string a, string b) =>
            string.Equals(Canonical(a), Canonical(b), StringComparison.Ordinal);

        /// <summary>Strip a "(O)"/"(BC)"-style type prefix, leaving the bare id.</summary>
        private static string Bare(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;
            int close = itemId.IndexOf(')');
            return close >= 0 && itemId.StartsWith("(", StringComparison.Ordinal)
                ? itemId.Substring(close + 1)
                : itemId;
        }
    }
}
