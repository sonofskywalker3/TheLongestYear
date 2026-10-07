using System;

namespace TheLongestYear.Core;

/// <summary>The numbers behind the good and bad wildcard twists (spec section 8). Each rule takes
/// "is the twist on today" and returns the unchanged input when it is not, so a patch that calls
/// it behaves exactly as before on every other day. Wildcard effects stack with theme effects.</summary>
public static class WildcardEffects
{
    /// <summary>double_forage: extra items per forage pickup (guaranteed, unlike the 20% theme roll).</summary>
    public const int ForageExtra = 1;

    /// <summary>fast_bites / slow_bites: one more 30% step on the bite wait, like the theme's.</summary>
    public const float FastBiteFactor = 0.70f;
    public const float SlowBiteFactor = 1.30f;

    /// <summary>shop_sale: percent off gold prices, added to the Shop Discount chain and Haggler.</summary>
    public const int ShopSalePercent = 25;

    /// <summary>sell_down: sell prices at 75%, on top of the theme's halving when both apply.</summary>
    public const double SellDownFactor = 0.75;

    /// <summary>max_luck: vanilla's daily-luck ceiling, the same value Fortune's Favor writes.</summary>
    public const double MaxLuck = 0.10;

    /// <summary>energy_drain: every energy loss during the day is 1.5x.</summary>
    public const float EnergyDrainFactor = 1.5f;

    /// <summary>The waking day, 6am to 2am; outside it (and during the night's day update) energy
    /// changes are the game's sleep and pass-out bookkeeping and are never scaled.</summary>
    public const int DayStartTime = 600;
    public const int PassOutTime = 2600;

    public static int ForageExtraFor(bool doubleForage) => doubleForage ? ForageExtra : 0;

    public static float BiteFactor(bool fastBites, bool slowBites)
        => (fastBites ? FastBiteFactor : 1f) * (slowBites ? SlowBiteFactor : 1f);

    public static int ShopPercent(int basePercent, bool shopSale)
        => shopSale ? basePercent + ShopSalePercent : basePercent;

    /// <summary>Sell price after the sell_down twist. Non-positive prices are left alone and a
    /// positive price never drops below 1g (the same floor as the theme's halving).</summary>
    public static int SellPrice(int price, bool sellDown)
    {
        if (!sellDown || price <= 0) return price;
        return Math.Max(1, (int)(price * SellDownFactor));
    }

    public static double Luck(double luck, bool maxLuck) => maxLuck ? MaxLuck : luck;

    /// <summary>The stamina value to write. Only a decrease during the waking day, outside the
    /// night's day update, is scaled; every gain and every other write passes through.</summary>
    public static float ScaleStamina(float current, float next, bool energyDrain, int timeOfDay, bool nightUpdate)
    {
        if (!energyDrain || nightUpdate) return next;
        if (timeOfDay < DayStartTime || timeOfDay >= PassOutTime) return next;
        if (next >= current) return next;
        return current - (current - next) * EnergyDrainFactor;
    }
}
