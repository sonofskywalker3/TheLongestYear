using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary><c>tly_lexicon ids vanilla</c> and <c>tly_lexicon ids &lt;UniqueID&gt;[+&lt;UniqueID&gt;]</c>:
    /// write the known ids (<see cref="KnownIds"/>) of the live game data. <c>vanilla</c> writes every
    /// id (run it on an unmodded game); a mod target writes the ids that are not vanilla's and not
    /// another installed layer's (run it with only those mods and their frameworks installed). A file
    /// that exists is added to, never shrunk, so each save variant can add its own rows: delete the
    /// file to start it over. The ids are read at game start, so restart before mapping the mod.</summary>
    internal static class LexiconIdsCommand
    {
        public const string Verb = "ids";
        private const string VanillaTarget = "vanilla";

        public static void Run(IMonitor monitor, string modDirectory, string target, Func<string, bool> isLoaded)
        {
            if (KnownData.Assets.Count == 0)
            {
                monitor.Log("tly_lexicon ids: nothing has been read yet; load a save first.", LogLevel.Warn);
                return;
            }
            bool vanilla = string.Equals(target, VanillaTarget, StringComparison.OrdinalIgnoreCase);
            string folder = LayerFile.Folder(modDirectory);
            string path = vanilla
                ? ShippedLexicon.VanillaIdsPath(modDirectory)
                : Path.Combine(folder, LayerFile.IdsFileName(target.Split(LayerFile.Joiner, StringSplitOptions.RemoveEmptyEntries)));

            var below = new HashSet<string>(StringComparer.Ordinal);
            if (!vanilla)
            {
                IReadOnlyList<string> vanillaIds = ShippedLexicon.LoadVanillaIds(modDirectory);
                if (vanillaIds == null)
                {
                    monitor.Log("tly_lexicon ids: no vanilla ids file yet; run 'tly_lexicon ids vanilla' on an unmodded game first.", LogLevel.Warn);
                    return;
                }
                below.UnionWith(vanillaIds);
                if (Directory.Exists(folder))
                    foreach (string other in Directory.GetFiles(folder, "*" + LayerFile.IdsSuffix))
                        if (!string.Equals(Path.GetFullPath(other), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
                            && LayerFile.ModsInFileName(Path.GetFileName(other)).All(isLoaded))
                            below.UnionWith(LayerFile.ReadIds(File.ReadAllText(other)));
            }

            var ids = new HashSet<string>(KnownData.LiveIds().Where(id => !below.Contains(id)), StringComparer.Ordinal);
            int live = ids.Count;
            if (File.Exists(path)) ids.UnionWith(LayerFile.ReadIds(File.ReadAllText(path)));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, LayerFile.WriteIds(ids));
            monitor.Log(
                $"tly_lexicon ids: wrote {path} ({ids.Count:N0} ids; {live:N0} in this game, {KnownData.Assets.Count} assets). Restart the game to use them.",
                LogLevel.Info);
        }
    }
}
