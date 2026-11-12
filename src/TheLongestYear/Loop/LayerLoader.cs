using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Core.Lexicon;

namespace TheLongestYear.Loop
{
    /// <summary>The supported-mod layers found for this game: the composed set, and the active layer
    /// files it was composed from (what <c>tly_lexicon write &lt;UniqueID&gt;</c> measures against).</summary>
    internal sealed record LoadedLayers(
        LayerSet Set, IReadOnlyList<LayerInputs> Inputs, IReadOnlyList<LayerAnswers> Answers, Func<string, string> InstalledVersionOf)
    {
        /// <summary>Which installed supported mod adds each item (<see cref="RecipeSources.ItemOwners"/>),
        /// for the recipe book's source tabs. Read from every layer whose mod is loaded, even one checked
        /// against another version: a mod update rarely changes which items are its own.</summary>
        public IReadOnlyDictionary<string, string> ItemOwners { get; init; } = new Dictionary<string, string>();
    }

    /// <summary>The SMAPI side of the supported-mod layers: reads every file under
    /// <c>assets/lexicon/layers/</c> whose mods are all installed (<see cref="LayerFile.ReadFolder"/>), keeps the layers whose mods
    /// are all loaded, drops the generated answers checked against another installed version
    /// (<see cref="LayerSet.Active"/> decides and WARNs), and composes the rest over
    /// the shipped vanilla answers (<see cref="LayerSet.Compose"/>). Only the mod registry calls live here.</summary>
    internal static class LayerLoader
    {
        /// <summary>The layers for this game. Anything that goes wrong reading or composing them means
        /// no layers (vanilla only), with one WARN naming the exception: a broken layer must never
        /// cost the session its answers.</summary>
        public static LoadedLayers Load(string modFolder, AnswerSet vanilla, IModRegistry registry, Action<string> warn)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            Func<string, bool> isLoaded = registry.IsLoaded;
            Func<string, string> installedVersionOf = id => registry.Get(id)?.Manifest.Version?.ToString();
            try
            {
                var (allInputs, allAnswers) = LayerFile.ReadFolder(LayerFile.Folder(modFolder), warn, isLoaded);
                var (inputs, answers) = LayerSet.Active(allInputs, allAnswers, isLoaded, installedVersionOf, warn);
                return new LoadedLayers(LayerSet.Compose(vanilla, inputs, answers, warn), inputs, answers, installedVersionOf)
                {
                    ItemOwners = RecipeSources.ItemOwners(
                        allAnswers.Where(a => a.Mods.Count == 1 && isLoaded(a.Mods[0]))
                            .Select(a => (a.Mods[0], a.Deltas.Values.SelectMany(v => v.Obtain.Keys).Distinct())),
                        VanillaItemIds.All),
                };
            }
            catch (Exception e)   // any failure: no layers, logged, never a null model
            {
                warn($"Lexicon layers could not be loaded ({e.GetType().Name}: {e.Message}); using the vanilla answers only.");
                return None(vanilla, installedVersionOf);
            }
        }

        private static LoadedLayers None(AnswerSet vanilla, Func<string, string> installedVersionOf)
            => new(LayerSet.Compose(vanilla, Array.Empty<LayerInputs>(), Array.Empty<LayerAnswers>(), _ => { }),
                Array.Empty<LayerInputs>(), Array.Empty<LayerAnswers>(), installedVersionOf);
    }
}
