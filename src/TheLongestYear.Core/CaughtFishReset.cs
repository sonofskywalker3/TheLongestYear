using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core
{
    /// <summary>The rewind must let a legendary be caught again: the game blocks a repeat through
    /// CatchLimit against player.fishCaught (GameLocation.cs:13831) and the reset never touched that
    /// record. Only catch-limited ids are cleared; the collection tab keeps everything else.</summary>
    public static class CaughtFishReset
    {
        public static IReadOnlyList<string> IdsToClear(IEnumerable<string> catchLimitedIds, IEnumerable<string> caughtIds)
        {
            var limited = new HashSet<string>(catchLimitedIds, System.StringComparer.Ordinal);
            return caughtIds.Where(limited.Contains).ToList();
        }
    }
}
