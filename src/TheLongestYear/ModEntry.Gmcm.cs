using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Register every config option with Generic Mod Config Menu, when it is installed.</summary>
        private void RegisterGmcm()
        {
            var gmcm =this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                this.Monitor.Log("GMCM not installed — config edits via config.json only.", LogLevel.Trace);
                return;
            }

            DifficultyLever difficultyLever = null;
            gmcm.Register(this.ModManifest,
                reset: () =>
                {
                    difficultyLever?.Clear();
                    _config = new GameplayConfig();
                },
                save: () =>
                {
                    difficultyLever?.Clear();
                    this.Helper.WriteConfig(_config);
                });
            difficultyLever = new DifficultyLever(this.Helper, gmcm, this.ModManifest, this.Monitor);

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.section"));
            gmcm.AddParagraph(this.ModManifest,
                () => Strings.Get("gmcm.master-blurb"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Enabled,
                setValue: v => _config.Enabled = v,
                name: () => Strings.Get("gmcm.enabled.name"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.ShowJpHud,
                setValue: v => _config.ShowJpHud = v,
                name: () => Strings.Get("gmcm.jp-hud.name"),
                tooltip: () => Strings.Get("gmcm.jp-hud.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.LimitTravelingCartStock,
                setValue: v => { _config.LimitTravelingCartStock = v; CartSlotLimitPatch.Enabled = v; },
                name: () => Strings.Get("gmcm.cart-limit.name"),
                tooltip: () => Strings.Get("gmcm.cart-limit.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.ResendBetterStartGift,
                setValue: v => _config.ResendBetterStartGift = v,
                name: () => Strings.Get("gmcm.resend-better-start.name"),
                tooltip: () => Strings.Get("gmcm.resend-better-start.tooltip"));

            gmcm.AddTextOption(this.ModManifest,
                // One setting, three choices. With a save loaded it reads and writes THAT save's
                // choice; the config is shared by every save and only holds the default the new-game
                // dropdown starts on. An older config may say the legacy "Vanilla",
                // which names no layout, so show it as Normal.
                getValue: () =>
                {
                    if (Context.IsWorldReady && _metaLoaded)
                    {
                        MetaState state = _meta.State;
                        return BundleSourceNames.ForSave(state.ChosenBundleSource, state.BundleSource, state.VanillaBundleType);
                    }
                    string stored = BundleSourceNames.Normalize(_config.BundleSource);
                    return stored == BundleSourceNames.LegacyVanilla ? BundleSourceNames.Normal : stored;
                },
                setValue: v =>
                {
                    if (Context.IsWorldReady && _metaLoaded)
                        _meta.State.ChosenBundleSource = BundleSourceNames.Normalize(v);
                    else
                        _config.BundleSource = BundleSourceNames.Normalize(v);
                },
                name: () => Strings.Get("gmcm.bundle-source.name"),
                tooltip: () => Strings.Get("gmcm.bundle-source.tooltip"),
                allowedValues: BundleSourceNames.All,
                formatAllowedValue: FormatBundleSource);

            gmcm.AddBoolOption(this.ModManifest,
                // Per save, like Bundle source: with a save loaded it reads and writes THAT save's
                // choice (applied at its next loop); on the title screen it sets what a new game
                // starts with (spec 2026-10-08 addendum 1).
                getValue: () => Context.IsWorldReady && _metaLoaded
                    ? _meta.State.ModItemsChosen()
                    : _config.AllowModItemsInCustomBundles,
                setValue: v =>
                {
                    if (Context.IsWorldReady && _metaLoaded)
                        _meta.State.AllowModItemsInCustomBundles = v;
                    else
                        _config.AllowModItemsInCustomBundles = v;
                },
                name: () => Strings.Get("gmcm.allow-mod-items.name"),
                tooltip: () => Strings.Get("gmcm.allow-mod-items.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.AutoDetectReplayableUnlockCutscenes,
                setValue: v => _config.AutoDetectReplayableUnlockCutscenes = v,
                name: () => Strings.Get("gmcm.auto-detect.name"),
                tooltip: () => Strings.Get("gmcm.auto-detect.tooltip"));

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.features.section"));
            gmcm.AddParagraph(this.ModManifest, () => Strings.Get("gmcm.features.blurb"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.FestivalTimeFlows,
                setValue: v => { _config.FestivalTimeFlows = v; TheLongestYear.Loop.FestivalTimeFlow.Enabled = v; },
                name: () => Strings.Get("gmcm.festival-time.name"),
                tooltip: () => Strings.Get("gmcm.festival-time.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.FestivalMainEventOncePerDay,
                setValue: v => { _config.FestivalMainEventOncePerDay = v; TheLongestYear.Loop.FestivalMainEventOncePatch.Enabled = v; },
                name: () => Strings.Get("gmcm.festival-once.name"),
                tooltip: () => Strings.Get("gmcm.festival-once.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableDejaVuDialogue,
                setValue: v => { _config.EnableDejaVuDialogue = v; TheLongestYear.Loop.DejaVuDialoguePatch.Enabled = v; },
                name: () => Strings.Get("gmcm.dejavu.name"),
                tooltip: () => Strings.Get("gmcm.dejavu.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableDejaVuFestivalMemories,
                setValue: v => _config.EnableDejaVuFestivalMemories = v,
                name: () => Strings.Get("gmcm.festmem.name"),
                tooltip: () => Strings.Get("gmcm.festmem.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableNonObjectDonations,
                setValue: v => _config.EnableNonObjectDonations = v,
                name: () => Strings.Get("gmcm.non-object.name"),
                tooltip: () => Strings.Get("gmcm.non-object.tooltip"));

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.SelectionBonusMultiplier,
                setValue: v => _config.SelectionBonusMultiplier = v,
                name: () => Strings.Get("gmcm.bonus-mult.name"),
                tooltip: () => Strings.Get("gmcm.bonus-mult.tooltip"),
                min: 1f, max: 3f, interval: 0.1f);

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.StartingMoney,
                setValue: v => _config.StartingMoney = v,
                name: () => Strings.Get("gmcm.starting-money.name"),
                tooltip: () => Strings.Get("gmcm.starting-money.tooltip"),
                min: 0, max: 5000, interval: 100);

            // ---- Difficulty modifiers (spec 2026-08-26) ----
            // Ten independent dials. Everything defaults to Normal, which is the shipping balance,
            // and a change lands at the NEXT reset because WorldResetService stamps the resolved
            // profile onto the save and every consumer reads that stamp. The overall lever above
            // them only sets all ten at once (DifficultyLever); nothing reads it for gameplay.
            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.difficulty.section"));
            gmcm.AddParagraph(this.ModManifest, () => Strings.Get("gmcm.difficulty.blurb"));

            gmcm.AddTextOption(this.ModManifest,
                getValue: () => (difficultyLever.Pending ?? _config.Difficulty.Overall).ToString(),
                setValue: v => _config.Difficulty.Overall = DifficultySteps.Parse(v),
                name: () => Strings.Get("gmcm.difficulty.overall.name"),
                tooltip: () => Strings.Get("gmcm.difficulty.overall.tooltip"),
                allowedValues: DifficultySteps.AllNames,
                formatAllowedValue: FormatDifficultyStep,
                fieldId: DifficultyLever.FieldId);

            void AddDifficultyOption(
                Func<DifficultyStep> get, Action<DifficultyStep> set,
                Func<string> name, Func<string> tooltip)
            {
                gmcm.AddTextOption(this.ModManifest,
                    getValue: () => (difficultyLever.Pending ?? get()).ToString(),
                    setValue: v => set(DifficultySteps.Parse(v)),
                    name: name,
                    tooltip: tooltip,
                    allowedValues: DifficultySteps.AllNames,
                    formatAllowedValue: FormatDifficultyStep);
            }

            AddDifficultyOption(
                () => _config.Difficulty.StackSize, v => _config.Difficulty.StackSize = v,
                () => Strings.Get("gmcm.difficulty.stack-size.name"),
                () => Strings.Get("gmcm.difficulty.stack-size.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.OncePerLoopAsksOne,
                setValue: v => _config.OncePerLoopAsksOne = v,
                name: () => Strings.Get("gmcm.difficulty.once-per-loop.name"),
                tooltip: () => Strings.Get("gmcm.difficulty.once-per-loop.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.QualityAsks, v => _config.Difficulty.QualityAsks = v,
                () => Strings.Get("gmcm.difficulty.quality-asks.name"),
                () => Strings.Get("gmcm.difficulty.quality-asks.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.RequiredSlots, v => _config.Difficulty.RequiredSlots = v,
                () => Strings.Get("gmcm.difficulty.required-slots.name"),
                () => Strings.Get("gmcm.difficulty.required-slots.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.ItemRarity, v => _config.Difficulty.ItemRarity = v,
                () => Strings.Get("gmcm.difficulty.item-rarity.name"),
                () => Strings.Get("gmcm.difficulty.item-rarity.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.JpEarned, v => _config.Difficulty.JpEarned = v,
                () => Strings.Get("gmcm.difficulty.jp-earned.name"),
                () => Strings.Get("gmcm.difficulty.jp-earned.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.ShrinePrices, v => _config.Difficulty.ShrinePrices = v,
                () => Strings.Get("gmcm.difficulty.shrine-prices.name"),
                () => Strings.Get("gmcm.difficulty.shrine-prices.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.StartingGold, v => _config.Difficulty.StartingGold = v,
                () => Strings.Get("gmcm.difficulty.starting-gold.name"),
                () => Strings.Get("gmcm.difficulty.starting-gold.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.CartSlots, v => _config.Difficulty.CartSlots = v,
                () => Strings.Get("gmcm.difficulty.cart-slots.name"),
                () => Strings.Get("gmcm.difficulty.cart-slots.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.HoldPrices, v => _config.Difficulty.HoldPrices = v,
                () => Strings.Get("gmcm.difficulty.hold-prices.name"),
                () => Strings.Get("gmcm.difficulty.hold-prices.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.BundleCount, v => _config.Difficulty.BundleCount = v,
                () => Strings.Get("gmcm.difficulty.bundle-count.name"),
                () => Strings.Get("gmcm.difficulty.bundle-count.tooltip"));

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.randomizer.section"));
            gmcm.AddParagraph(this.ModManifest, () => Strings.Get("gmcm.randomizer.blurb"));

            gmcm.AddTextOption(this.ModManifest,
                getValue: () => _config.Randomizer.Rerolls.ToString(),
                setValue: v => _config.Randomizer.Rerolls = Enum.TryParse(v, out RerollMode m) && Enum.IsDefined(typeof(RerollMode), m) ? m : RerollMode.Off,
                name: () => Strings.Get("gmcm.randomizer.rerolls.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.rerolls.tooltip"),
                allowedValues: new[] { "Off", "CostsJp", "Free" },
                formatAllowedValue: FormatRerollMode);
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomThemeItems,
                setValue: v => _config.Randomizer.RandomThemeItems = v,
                name: () => Strings.Get("gmcm.randomizer.random-theme-items.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-theme-items.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomPairings,
                setValue: v => _config.Randomizer.RandomPairings = v,
                name: () => Strings.Get("gmcm.randomizer.random-pairings.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-pairings.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomMultiplier,
                setValue: v => _config.Randomizer.RandomMultiplier = v,
                name: () => Strings.Get("gmcm.randomizer.random-multiplier.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-multiplier.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.MysteryCard,
                setValue: v => _config.Randomizer.MysteryCard = v,
                name: () => Strings.Get("gmcm.randomizer.mystery-card.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.mystery-card.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomBundleRewards,
                setValue: v => _config.Randomizer.RandomBundleRewards = v,
                name: () => Strings.Get("gmcm.randomizer.random-bundle-rewards.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-bundle-rewards.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomCartDays,
                setValue: v => _config.Randomizer.RandomCartDays = v,
                name: () => Strings.Get("gmcm.randomizer.random-cart-days.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-cart-days.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.DoubleThemeWeek,
                setValue: v => _config.Randomizer.DoubleThemeWeek = v,
                name: () => Strings.Get("gmcm.randomizer.double-theme-week.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.double-theme-week.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.WildcardDays,
                setValue: v => _config.Randomizer.WildcardDays = v,
                name: () => Strings.Get("gmcm.randomizer.wildcard-days.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.wildcard-days.tooltip"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Randomizer.RandomShrineDonations,
                setValue: v => _config.Randomizer.RandomShrineDonations = v,
                name: () => Strings.Get("gmcm.randomizer.random-shrine-donations.name"),
                tooltip: () => Strings.Get("gmcm.randomizer.random-shrine-donations.tooltip"));

            this.Monitor.Log("Registered GMCM options.", LogLevel.Info);
        }

        /// <summary>Localised label for a bundle-source choice. Written as literal
        /// <see cref="Strings.Get"/> calls rather than an interpolated key so the i18n guard's
        /// source scan can prove every key is reachable.</summary>
        private static string FormatBundleSource(string rawValue)
        {
            if (string.Equals(rawValue, BundleSourceNames.Normal, StringComparison.OrdinalIgnoreCase))
                return Strings.Get("gmcm.bundle-source.normal");
            if (string.Equals(rawValue, BundleSourceNames.Remixed, StringComparison.OrdinalIgnoreCase))
                return Strings.Get("gmcm.bundle-source.remixed");
            return Strings.Get("gmcm.bundle-source.engine");
        }

        private static string FormatRerollMode(string rawValue) => Enum.TryParse(rawValue, out RerollMode m)
            ? m switch
            {
                RerollMode.CostsJp => Strings.Get("gmcm.randomizer.rerolls.costs-jp"),
                RerollMode.Free => Strings.Get("gmcm.randomizer.rerolls.free"),
                _ => Strings.Get("gmcm.randomizer.rerolls.off"),
            }
            : Strings.Get("gmcm.randomizer.rerolls.off");

        /// <summary>Localised label for a difficulty step in the GMCM dropdown. Written as four
        /// literal <see cref="Strings.Get"/> calls rather than an interpolated key so the i18n
        /// guard's source scan can prove all four keys are reachable.</summary>
        private static string FormatDifficultyStep(string rawValue) => DifficultySteps.Parse(rawValue) switch
        {
            DifficultyStep.Easy => Strings.Get("gmcm.difficulty.step.easy"),
            DifficultyStep.Hard => Strings.Get("gmcm.difficulty.step.hard"),
            DifficultyStep.Extreme => Strings.Get("gmcm.difficulty.step.extreme"),
            _ => Strings.Get("gmcm.difficulty.step.normal"),
        };
    }
}
