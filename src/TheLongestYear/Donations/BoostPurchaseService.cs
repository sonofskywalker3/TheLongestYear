using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Mod-side wrapper around <see cref="BoostPurchase.TryBuy"/>, the boost twin of
    /// <see cref="UpgradePurchaseService"/>: it supplies the live facts (Core never reads game
    /// state), applies the immediate part of a bought boost (weather write, XP grant, elevator
    /// floor, buffs), logs the outcome, plays the purchase sound on success and shows a HUD
    /// message when the player cannot afford it.
    /// <para>
    /// Persistence follows the same rule as upgrade purchases: the spent JP and the run record live
    /// in <see cref="MetaStore.State"/> / <see cref="MetaStore.Run"/> and are committed by the
    /// game's own Saving event. Nothing is written eagerly, which keeps the anti-save-scum
    /// invariant documented on <see cref="MetaStore.Save"/> intact.
    /// </para>
    /// </summary>
    internal sealed class BoostPurchaseService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly BoostEffectsService _effects;

        public BoostPurchaseService(IMonitor monitor, MetaStore store, BoostEffectsService effects)
        {
            _monitor = monitor;
            _store = store;
            _effects = effects;
        }

        public BoostContext Context(int skill = -1) => BoostContextBuilder.Build(_store.Run, skill);

        /// <summary>Attempt to buy a boost today. Returns the rule's result so the menu can refresh its rows.</summary>
        public BoostPurchase.Result TryBuy(BoostId id, int skill = -1)
        {
            BoostContext ctx = Context(skill);
            BoostPurchase.Result result = BoostPurchase.TryBuy(_store.State, _store.Run, id, ctx);
            if (result == BoostPurchase.Result.Success)
                ApplyImmediate(id, ctx);
            Report(id, ctx, result);
            return result;
        }

        /// <summary>The part of a boost that is game state rather than a run flag (spec 2.3, 2.6, 2.9, 2.10).</summary>
        private void ApplyImmediate(BoostId id, BoostContext ctx)
        {
            Farmer p = Game1.player;
            switch (id)
            {
                case BoostId.RainDance:
                    BoostEffectsService.WriteTomorrow(BoostPurchase.Rain, _monitor);
                    break;
                case BoostId.StormCall:
                    BoostEffectsService.WriteTomorrow(BoostPurchase.Storm, _monitor);
                    break;
                case BoostId.CrashCourse:
                {
                    // Full width of the target level on top of current XP (ruling 9): at 80/100
                    // buying level 1 lands at 180. getBaseExperienceForLevel(0) is -1, hence the guard.
                    int current = ctx.SkillLevels[ctx.Skill];
                    int width = current == 0
                        ? Farmer.getBaseExperienceForLevel(1)
                        : Farmer.getBaseExperienceForLevel(current + 1) - Farmer.getBaseExperienceForLevel(current);
                    p.gainExperience(ctx.Skill, width);
                    _monitor.Log($"Crash Course: +{width} XP in skill {ctx.Skill} (level {current} to {current + 1} at bedtime).", LogLevel.Info);
                    break;
                }
                case BoostId.ElevatorPass:
                {
                    int landing = BoostPricing.ElevatorLanding(ctx.MineFloor);
                    Game1.netWorldState.Value.LowestMineLevel = landing;
                    Game1.netWorldState.Value.LowestMineLevelForOrder = landing;
                    p.deepestMineLevel = System.Math.Max(p.deepestMineLevel, landing);
                    _monitor.Log($"Elevator Pass: elevator now reaches floor {landing} (was {ctx.MineFloor}).", LogLevel.Info);
                    break;
                }
                case BoostId.QuickFeet:
                case BoostId.IronLungs:
                    _effects?.ApplyDailyBuffs();
                    break;
            }
        }

        private void Report(BoostId id, BoostContext ctx, BoostPurchase.Result result)
        {
            long cost = BoostPricing.CostOf(BoostCatalog.Get(id), _store.Run, ctx);
            switch (result)
            {
                case BoostPurchase.Result.Success:
                    Game1.playSound("purchase");
                    _monitor.Log($"Boost bought: {id} (day {ctx.DayOfYear}). JP remaining: {_store.State.JunimoPoints}.", LogLevel.Info);
                    break;
                case BoostPurchase.Result.NotEnoughJp:
                    Game1.playSound("cancel");
                    Game1.addHUDMessage(new HUDMessage(
                        Strings.Get("boost.not-enough-jp", new Dictionary<string, string>
                        {
                            ["cost"] = cost.ToString(),
                            ["have"] = _store.State.JunimoPoints.ToString(),
                        }),
                        HUDMessage.error_type));
                    _monitor.Log($"Boost {id} not bought: costs {cost} JP, you have {_store.State.JunimoPoints}.", LogLevel.Info);
                    break;
                case BoostPurchase.Result.AlreadyActive:
                    Game1.playSound("cancel");
                    _monitor.Log($"Boost {id} not bought: already active (day {ctx.DayOfYear}).", LogLevel.Info);
                    break;
                case BoostPurchase.Result.NotAvailable:
                    Game1.playSound("cancel");
                    _monitor.Log($"Boost {id} not bought: not available on day {ctx.DayOfYear}.", LogLevel.Info);
                    break;
            }
        }
    }
}
