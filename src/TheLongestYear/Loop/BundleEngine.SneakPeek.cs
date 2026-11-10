using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class BundleEngine
    {
        /// <summary>The dishes whose recipes were banked in the Cookbook when this board was built
        /// (<see cref="Core.MetaState.BoardBankedDishes"/>). A generation input like
        /// <see cref="Banishes"/>: every construction site passes the board's stamp, never the live
        /// Cookbook, so a reload re-derives the board the reset wrote. Null = none banked.</summary>
        public IReadOnlyCollection<string> BankedDishes
        {
            get => _bankedDishes;
            set { _bankedDishes = value; _boardAvailability = null; }
        }
        private IReadOnlyCollection<string> _bankedDishes;

        private const string CookingRecipesAsset = "Data/CookingRecipes";

        /// <summary>The dish ids the Cookbook's banked recipes make, for the
        /// <see cref="Core.MetaState.BoardBankedDishes"/> stamp. Read only where a NEW board is
        /// built: the stamp, not this, is what every later reproduction of the board passes.</summary>
        public static List<string> BankedDishIdsFor(IEnumerable<string> cookbookRecipes)
            => Core.SneakPeekDishes.DishIdsFor(
                cookbookRecipes, StardewValley.Game1.content.Load<Dictionary<string, string>>(CookingRecipesAsset));

        /// <summary>The Sneak-Peek-only dishes this board leaves out of its item pools (Jeff,
        /// 2026-11-10; <see cref="Core.SneakPeekDishes"/>). Joining them to the excluded ids before
        /// the pools are built takes them out of every board path at once: rolled recipes
        /// (Children's sweets included), Home Cook's Feast and the no-repeat swaps.</summary>
        private IReadOnlySet<string> SneakPeekExclusions()
        {
            Core.DifficultyStep step = Availability?.Step ?? Core.DifficultyStep.Normal;
            IReadOnlySet<string> excluded = Core.SneakPeekDishes.ExcludedFor(step, Availability, BankedDishes);
            if (excluded.Count > 0)
                _monitor?.Log(
                    $"BundleEngine: {excluded.Count} Sneak Peek dish(es) kept off this {step} board " +
                    $"({BankedDishes?.Count ?? 0} banked recipe dish(es) allowed): {string.Join(", ", excluded.OrderBy(id => id, StringComparer.Ordinal))}.",
                    LogLevel.Trace);
            return excluded;
        }

        /// <summary>Guard, not the rule: a template kept with its vanilla slots never passes through
        /// the pools, so it could still ask for an excluded dish. Say so rather than letting it pass
        /// quietly; nothing is changed.</summary>
        private void WarnExcludedDishAsks(IReadOnlyList<BundleSpec> board, IReadOnlySet<string> excluded)
        {
            if (excluded.Count == 0) return;
            foreach (BundleSpec bundle in board.Where(b => !PassThroughRooms.Contains(b.Room)))
            foreach (BundleSlotSpec slot in bundle.Slots)
            {
                if (slot.ItemId == MoneySlotId || BundleParsing.IsCategoryRef(slot.ItemId)) continue;
                string id = BundleParsing.NormalizeItemId(slot.ItemId);
                if (id != null && excluded.Contains(id))
                    _monitor?.Log(
                        $"BundleEngine: '{bundle.Room}/{bundle.Name}' still asks for {id}, a Sneak Peek dish this board leaves out (kept template slots).",
                        LogLevel.Warn);
            }
        }
    }
}
