using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>Pierre's year-2 seeds (Garlic, Red Cabbage, Artichoke), and which of them a year-1
/// run can actually get hold of.
///
/// Only the SHOP listing is year-gated: Data/Shops puts <c>YEAR 2</c> on all three at Pierre and
/// the Night Market. Any route that is not a shop ignores that condition, and the three do not
/// have the same routes (verified against Data/Crops, Data/Shops and the decompiled
/// <c>Crop.getRandomLowGradeCropForThisSeason</c> on 2026-09-10):
///
/// - <b>Garlic (476)</b> has no year-1 route at all. Spring's Mixed Seeds roll is
///   <c>Next(472, 476)</c>, which stops one short of it, and no cart entry lists it.
/// - <b>Red Cabbage (485)</b> is guaranteed from the Traveling Cart once in year 1
///   (<c>VisitsUntilY1Guarantee 0 0</c>), on saves that arm it. That line sits after the cart's
///   10 random objects, so this mod's slot cap hid it until 1.0.25; it now takes a shown slot
///   (CartSlotArrangement). Any other cart visit offers it only by the Year 2 slot's luck.
/// - <b>Artichoke (489)</b> comes out of Fall's Mixed Seeds roll, <c>Next(487, 491)</c>, at 25%
///   per seed in any year, free and untouched by this mod. It is therefore NEVER excluded here.
///
/// So the two crops with no free random route each get a Cultivation upgrade that adds one, and
/// Artichoke gets none because it already has the best route of the three. A board or weekly
/// theme must not ask for a crop the player has no way to grow (Jeff, 2026-08-25: a Garlic
/// weekly goal on run 1 is unwinnable by construction).
///
/// Pure rule; the owned-upgrade set is the same at reset time and on reload because upgrades are
/// only bought at the shrine before a reset, so generation stays deterministic.</summary>
public static class YearTwoCrops
{
    public const string PierreUpgrade = "pierre_year2_seeds";
    public const string GarlicUpgrade = "cult_garlic";
    public const string RedCabbageUpgrade = "cult_red_cabbage";

    public const string Garlic = "(O)248";
    public const string RedCabbage = "(O)266";
    public const string Artichoke = "(O)274";

    /// <summary>The most any one ask may want of the three crops while <see cref="PierreUpgrade"/>
    /// is not owned, by the Stack Size step. Without the Pierre buy, Mixed Seeds are the only
    /// route, and the shop-seed basis of 99 let a Fall board ask for 49 Artichokes (JazzDaxx,
    /// Nexus; Jeff, 2026-10-21). The band roll still runs; a roll under the cap stands.</summary>
    public const int AskCapEasy = 2, AskCapNormal = 3, AskCapHard = 4, AskCapExtreme = 5;

    public static readonly IReadOnlySet<string> CropIds =
        new HashSet<string>(StringComparer.Ordinal) { Garlic, RedCabbage, Artichoke };

    /// <summary>The seeds behind those three crops. Their "YEAR 2" shop lines are TLY's own
    /// business (the upgrades above, and <see cref="ExcludedFor"/> on Easy), so source reachability
    /// does not read the year lock on them. Every other year-2 line, vanilla or modded, is closed.</summary>
    public static readonly IReadOnlySet<string> TlyUnlockedSeedIds =
        new HashSet<string>(StringComparer.Ordinal) { "(O)476", "(O)485", "(O)489" };

    /// <summary>Qualified ids to keep out of every pool for a player with these upgrades, on this
    /// difficulty step. Only Easy still refuses year-2 crops outright once the pools carry them at
    /// weight 1 (spec 2026-08-28-obtainable-board, section 3); Normal and above ask for them like
    /// any other addition. Artichoke is never excluded — see the class remarks.</summary>
    public static IReadOnlySet<string> ExcludedFor(Func<string, bool> hasUpgrade, DifficultyStep step)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (step != DifficultyStep.Easy)
            return excluded;
        bool pierre = hasUpgrade(PierreUpgrade);
        if (!pierre && !hasUpgrade(GarlicUpgrade))
            excluded.Add(Garlic);
        if (!pierre && !hasUpgrade(RedCabbageUpgrade))
            excluded.Add(RedCabbage);
        return excluded;
    }

    /// <summary>The crop ask cap for a Stack Size step (resolved into
    /// <see cref="DifficultyProfile.YearTwoCropAskCap"/>).</summary>
    public static int AskCapFor(DifficultyStep stackSize) => stackSize switch
    {
        DifficultyStep.Easy => AskCapEasy,
        DifficultyStep.Hard => AskCapHard,
        DifficultyStep.Extreme => AskCapExtreme,
        _ => AskCapNormal,
    };

    /// <summary>True when the cap applies: a save is in hand and it does not own
    /// <see cref="PierreUpgrade"/>. A cultivation upgrade adds a Mixed Seeds route, not a shop, so
    /// it does not lift the cap. A null <paramref name="hasUpgrade"/> caps nothing.</summary>
    public static bool IsCapped(Func<string, bool>? hasUpgrade)
        => hasUpgrade != null && !hasUpgrade(PierreUpgrade);

    /// <summary>A count ask held to the cap: <paramref name="cap"/> for one of the three crops when
    /// <see cref="IsCapped"/>, and for a dish the model lists in
    /// <see cref="ItemAvailabilityModel.YearTwoCropDishUnits"/> the cap over the crops one dish
    /// takes, never below one. Anything else keeps its stack.</summary>
    public static int ClampAsk(string? itemId, int stack, int cap, Func<string, bool>? hasUpgrade, ItemAvailabilityModel? model)
    {
        if (itemId == null) return stack;
        string id = BundleParsing.NormalizeItemId(itemId);
        if (CropIds.Contains(id))
            return IsCapped(hasUpgrade) ? Math.Min(stack, cap) : stack;
        return model != null && model.YearTwoCropDishUnits.TryGetValue(id, out int units)
            ? Math.Min(stack, PerUnit(cap, units))
            : stack;
    }

    /// <summary>The cap for something made from <paramref name="units"/> of one of the crops.</summary>
    public static int PerUnit(int cap, int units) => Math.Max(1, cap / Math.Max(1, units));

    /// <summary><see cref="ClampAsk"/> over every slot of a finished bundle. Returns the same
    /// reference when no slot changed.</summary>
    public static BundleSpec ClampBundle(BundleSpec spec, int cap, Func<string, bool>? hasUpgrade, ItemAvailabilityModel? model)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        List<BundleSlotSpec>? clamped = null;
        for (int i = 0; i < spec.Slots.Count; i++)
        {
            BundleSlotSpec slot = spec.Slots[i];
            int stack = ClampAsk(slot.ItemId, slot.Stack, cap, hasUpgrade, model);
            if (stack == slot.Stack) continue;
            clamped ??= spec.Slots.ToList();
            clamped[i] = slot with { Stack = stack };
        }
        return clamped == null ? spec : spec with { Slots = clamped };
    }

    /// <summary>Dishes every recipe of which takes one of the three crops by id, to the fewest of
    /// them any one recipe takes. Empty when the cap does not apply. A category ingredient (any
    /// vegetable) never counts: something else fills it.</summary>
    public static IReadOnlyDictionary<string, int> DishUnits(EffortData data, Func<string, bool>? hasUpgrade)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var units = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!IsCapped(hasUpgrade)) return units;
        foreach (IGrouping<string, RawCookingRecipe> dish in data.CookingRecipes.GroupBy(r => r.OutputItemId, StringComparer.Ordinal))
        {
            int? fewest = null;
            foreach (RawCookingRecipe recipe in dish)
            {
                int most = 0;
                for (int i = 0; i < recipe.IngredientIds.Count; i++)
                    if (CropIds.Contains(BundleParsing.NormalizeItemId(recipe.IngredientIds[i])))
                        most = Math.Max(most, recipe.CountOf(i));
                if (most == 0) { fewest = null; break; }
                fewest = fewest == null ? most : Math.Min(fewest.Value, most);
            }
            if (fewest is int n)
                units[BundleParsing.NormalizeItemId(dish.Key)] = n;
        }
        return units;
    }
}
