using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Fades whatever is drawn in front of the thief's chest or his path for the length of
    /// the scene (designer, 2026-10-08: the chest was behind a tree and he never saw it). Trees,
    /// fruit trees and buildings fade the way vanilla fades them when the farmer stands behind them,
    /// through their own alpha. A bush has no alpha in vanilla, so <see cref="BushTintPatch"/> tints
    /// its draw while it is on the list. Which things fade is <see cref="SceneSeeThrough"/>.
    ///
    /// Everything is put back at full opacity when the scene ends, however it ends.</summary>
    internal sealed class ScenePeekThrough
    {
        /// <summary>Building.alpha is protected.</summary>
        private static readonly FieldInfo BuildingAlpha = AccessTools.Field(typeof(Building), "alpha");

        /// <summary>Bushes being faded, and their current opacity, read by the draw patch.</summary>
        internal static readonly Dictionary<Bush, float> FadedBushes = new();

        private readonly List<Tree> _trees = new();
        private readonly List<FruitTree> _fruitTrees = new();
        private readonly List<Building> _buildings = new();
        private readonly List<Bush> _bushes = new();

        /// <summary>How many things are faded, for the staging log.</summary>
        public int Count => _trees.Count + _fruitTrees.Count + _buildings.Count + _bushes.Count;

        /// <summary>Find everything on <paramref name="where"/> that hides a watched tile.</summary>
        public ScenePeekThrough(GameLocation where, IReadOnlyCollection<(int X, int Y)> watched)
        {
            if (where == null || watched == null || watched.Count == 0) return;
            foreach (KeyValuePair<Vector2, TerrainFeature> pair in where.terrainFeatures.Pairs)
            {
                int x = (int)pair.Key.X, y = (int)pair.Key.Y;
                switch (pair.Value)
                {
                    case Tree tree when !tree.stump.Value && tree.growthStage.Value >= Tree.treeStage && SceneSeeThrough.Hides(SceneSeeThrough.Tree(x, y), watched):
                        _trees.Add(tree);
                        break;
                    case FruitTree fruit when !fruit.stump.Value && fruit.growthStage.Value >= FruitTree.treeStage && SceneSeeThrough.Hides(SceneSeeThrough.FruitTree(x, y), watched):
                        _fruitTrees.Add(fruit);
                        break;
                    case Bush bush when SceneSeeThrough.Hides(SceneSeeThrough.Bush(x, y, ExtraTilesWide(bush)), watched):
                        _bushes.Add(bush);
                        break;
                }
            }
            foreach (LargeTerrainFeature feature in where.largeTerrainFeatures)
            {
                if (feature is not Bush bush) continue;
                Vector2 tile = bush.Tile;
                if (SceneSeeThrough.Hides(SceneSeeThrough.Bush((int)tile.X, (int)tile.Y, ExtraTilesWide(bush)), watched))
                    _bushes.Add(bush);
            }
            if (BuildingAlpha != null)
            {
                foreach (Building building in where.buildings)
                {
                    if (!building.fadeWhenPlayerIsBehind.Value) continue;
                    Rectangle source = building.getSourceRectForMenu() ?? building.getSourceRect();
                    TileBox box = SceneSeeThrough.Building(building.tileX.Value, building.tileY.Value, building.tilesWide.Value, building.tilesHigh.Value, source.Height / 16);
                    if (SceneSeeThrough.Hides(box, watched)) _buildings.Add(building);
                }
            }
            foreach (Bush bush in _bushes) FadedBushes[bush] = 1f;
        }

        /// <summary>A bush's width past its first tile (Bush.getEffectiveSize, which is private:
        /// the tea bush draws as a small one, the walnut bush as a medium one).</summary>
        private static int ExtraTilesWide(Bush bush) => bush.size.Value switch
        {
            Bush.greenTeaBush => 0,
            Bush.walnutBush => 1,
            int size => size,
        };

        /// <summary>Every tick of the scene, after the map's own update: one step further toward
        /// faded, the step vanilla takes for a tree.</summary>
        public void Tick()
        {
            foreach (Tree tree in _trees) tree.alpha = SceneSeeThrough.FadeToward(tree.alpha);
            foreach (FruitTree fruit in _fruitTrees) fruit.alpha = SceneSeeThrough.FadeToward(fruit.alpha);
            foreach (Building building in _buildings)
                BuildingAlpha.SetValue(building, SceneSeeThrough.FadeToward((float)BuildingAlpha.GetValue(building)));
            foreach (Bush bush in _bushes)
                FadedBushes[bush] = SceneSeeThrough.FadeToward(FadedBushes.TryGetValue(bush, out float a) ? a : 1f);
        }

        /// <summary>Test scaffolding (<c>tly_sabotage fixture</c>): take away every tree, fruit tree
        /// and bush standing in front of the chest at <paramref name="tile"/> or its lid, so the
        /// fixture chest can always be seen. A building is left where it is. Returns how many went.</summary>
        public static int ClearInFrontOf(GameLocation where, (int X, int Y) tile)
        {
            if (where == null) return 0;
            var peek = new ScenePeekThrough(where, SceneSeeThrough.Watched(tile, null));
            int removed = 0;
            foreach (Tree tree in peek._trees) removed += where.terrainFeatures.Remove(tree.Tile) ? 1 : 0;
            foreach (FruitTree fruit in peek._fruitTrees) removed += where.terrainFeatures.Remove(fruit.Tile) ? 1 : 0;
            foreach (Bush bush in peek._bushes)
            {
                if (where.largeTerrainFeatures.Remove(bush)) removed++;
                else if (where.terrainFeatures.Remove(bush.Tile)) removed++;
            }
            peek.Restore();
            return removed;
        }

        /// <summary>Everything back at full opacity.</summary>
        public void Restore()
        {
            foreach (Tree tree in _trees) tree.alpha = 1f;
            foreach (FruitTree fruit in _fruitTrees) fruit.alpha = 1f;
            foreach (Building building in _buildings) BuildingAlpha?.SetValue(building, 1f);
            foreach (Bush bush in _bushes) FadedBushes.Remove(bush);
            _trees.Clear();
            _fruitTrees.Clear();
            _buildings.Clear();
            _bushes.Clear();
        }
    }

    /// <summary>Bush.draw paints with <c>Color.White</c> and has no alpha of its own. Every
    /// <c>Color.White</c> in it is swapped for <see cref="Tint"/>, which is still white for every
    /// bush the thief scene is not fading, so outside a scene nothing changes.</summary>
    [HarmonyPatch(typeof(Bush), nameof(Bush.draw), new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) })]
    internal static class BushTintPatch
    {
        private static readonly MethodInfo White = AccessTools.PropertyGetter(typeof(Color), nameof(Color.White));
        private static readonly MethodInfo TintMethod = AccessTools.Method(typeof(BushTintPatch), nameof(Tint));

        internal static Color Tint(Bush bush)
            => ScenePeekThrough.FadedBushes.Count > 0 && ScenePeekThrough.FadedBushes.TryGetValue(bush, out float alpha)
                ? Color.White * alpha
                : Color.White;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction code in instructions)
            {
                if (code.Calls(White))
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0);
                    load.labels.AddRange(code.labels);
                    load.blocks.AddRange(code.blocks);
                    yield return load;
                    yield return new CodeInstruction(OpCodes.Call, TintMethod);
                    continue;
                }
                yield return code;
            }
        }
    }
}
