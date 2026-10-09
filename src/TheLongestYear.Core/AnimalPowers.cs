using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What the next step of a baby animal's night is under Quick Growth.</summary>
public enum QuickGrowthStep { None, AgeOneDay, GrowFully }

/// <summary>
/// The animal powers' ids, numbers and pure rules (spec 2026-10-09 and its Rulings). The game-side
/// patches read these; nothing here touches game state. Data/FarmAnimals is never edited (the effort
/// model and the Herd Book read it), so every rule here works from the vanilla values it is given.
/// </summary>
public static class AnimalPowers
{
    public const string LuckyRabbits = "animal_rabbit_feet";
    public const string FineFeathers = "animal_duck_feathers";
    public const string BusyCoop = "animal_fast_produce_coop";
    public const string BusyBarn = "animal_fast_produce_barn";
    public const string TruffleNose = "animal_truffle_double";
    public const string SwiftHorse = "horse_swift";
    public const string HorseFlute = "horse_flute";
    public const string QuickGrowth = "animal_quick_growth";
    public const string FastHatch = "animal_fast_hatch";
    public const string WarmWelcomePrefix = "animal_warm_welcome_";
    public const string MorningRounds = "animal_morning_rounds";
    public const string SnugBarn = "animal_snug_barn";
    public const string LoyalPet = "pet_loyal";
    public const string KeepHorse = "early_horse";

    public const int WarmWelcomeTiers = 3;
    public const int FriendshipPerHeart = 200;

    /// <summary>Fine Feathers: extra chance a duck's egg is a Duck Feather instead (Ruling 5).</summary>
    public const double FineFeatherChance = 0.25;
    /// <summary>Truffle Nose: chance a dug truffle comes with a second one (Ruling 5).</summary>
    public const double TruffleDoubleChance = 0.25;
    /// <summary>Swift Horse: added to the riding speed sum (9.6 base, so about +10%, Ruling 5).</summary>
    public const double SwiftHorseSpeed = 1.0;
    /// <summary>Fast Hatch: incubator time multiplier.</summary>
    public const double IncubatorFactor = 0.5;
    /// <summary>Loyal Pet: daily gift chance floor (vanilla 0.2) and the friendship gifts start at
    /// (vanilla 1000, here 3 hearts), Ruling 5.</summary>
    public const float PetGiftChance = 0.4f;
    public const int PetGiftFriendship = 600;

    /// <summary>Vanilla's diagonal walking factor (Farmer.getMovementSpeed).</summary>
    private const float DiagonalFactor = 0.707f;
    private const int MaxHappiness = 255;

    public const string DuckType = "Duck";
    public const string RabbitType = "Rabbit";
    public const string GoatType = "Goat";
    public const string SheepType = "Sheep";
    public const string PigType = "Pig";
    public const string DuckEggId = "442";
    public const string DuckFeatherId = "444";
    public const string HorseFluteQid = "(O)911";
    public const string TruffleQid = "(O)430";

    /// <summary>Every row the powers add (the moved keeps are not listed).</summary>
    public static readonly IReadOnlyList<string> AllIds = new[]
    {
        MorningRounds, WarmWelcomePrefix + 1, WarmWelcomePrefix + 2, WarmWelcomePrefix + 3,
        QuickGrowth, FastHatch, SnugBarn, BusyCoop, BusyBarn, FineFeathers, LuckyRabbits, TruffleNose,
        SwiftHorse, HorseFlute, LoyalPet,
    };

    /// <summary>Busy Barnyard targets (Ruling 2): which row speeds which animal type, and to how many days.</summary>
    private static readonly IReadOnlyDictionary<string, (string Row, int Days)> ProduceTargets =
        new Dictionary<string, (string, int)>(StringComparer.Ordinal)
        {
            [DuckType] = (BusyCoop, 1),
            [RabbitType] = (BusyCoop, 2),
            [GoatType] = (BusyBarn, 1),
            [SheepType] = (BusyBarn, 1),
        };

    /// <summary>The produce interval Busy Barnyard gives this animal type, or null when no owned row covers it.</summary>
    public static int? ProduceTarget(string? animalType, Func<string, bool> has)
    {
        if (animalType == null || !ProduceTargets.TryGetValue(animalType, out var t)) return null;
        return has(t.Row) ? t.Days : null;
    }

    /// <summary>Days added to daysSinceLastLay for tonight's produce check, so the vanilla test
    /// <c>daysSinceLastLay >= DaysToProduce - bonus</c> passes at the target instead. Never negative.</summary>
    public static int ProduceDayOffset(string? animalType, int daysToProduce, Func<string, bool> has)
    {
        int? target = ProduceTarget(animalType, has);
        return target == null ? 0 : Math.Max(0, daysToProduce - target.Value);
    }

    /// <summary>Quick Growth's extra step for a night: vanilla moved the age from
    /// <paramref name="ageBefore"/> to <paramref name="ageAfterVanilla"/>; a baby that aged takes one more
    /// step the same way vanilla does (grow fully on the last baby day, else one day older).</summary>
    public static QuickGrowthStep QuickGrowthExtraStep(int ageBefore, int ageAfterVanilla, int daysToMature)
    {
        if (ageAfterVanilla <= ageBefore || ageAfterVanilla >= daysToMature) return QuickGrowthStep.None;
        return ageAfterVanilla == daysToMature - 1 ? QuickGrowthStep.GrowFully : QuickGrowthStep.AgeOneDay;
    }

    /// <summary>Fed nights from birth to adult under Quick Growth (vanilla's step plus the extra one).</summary>
    public static int QuickGrowthNights(int daysToMature)
    {
        int age = 0, nights = 0;
        while (age < daysToMature)
        {
            nights++;
            int before = age;
            age = age == daysToMature - 1 ? daysToMature : age + 1;
            switch (QuickGrowthExtraStep(before, age, daysToMature))
            {
                case QuickGrowthStep.GrowFully: age = daysToMature; break;
                case QuickGrowthStep.AgeOneDay: age++; break;
            }
        }
        return nights;
    }

    /// <summary>Warm Welcome: a new animal's friendship floor, the highest owned tier (0 when none).</summary>
    public static int WarmWelcomeFloor(Func<string, bool> has)
    {
        for (int tier = WarmWelcomeTiers; tier >= 1; tier--)
            if (has(WarmWelcomePrefix + tier)) return tier * FriendshipPerHeart;
        return 0;
    }

    /// <summary>Floor semantics: raise to the floor, never lower.</summary>
    public static int WelcomedFriendship(int current, int floor) => Math.Max(current, floor);

    /// <summary>Snug Barn: the Heater rule at any happiness, an indoor animal gains its drain.</summary>
    public static int SnugBarnHappiness(int before, int drain) => Math.Clamp(before + drain, 0, MaxHappiness);

    /// <summary>A day the animals cannot go out: rain (storm and green rain included) or Winter / TLY's snow day.</summary>
    public static bool IsStuckIndoorsDay(bool raining, bool winterOrSnowDay) => raining || winterOrSnowDay;

    /// <summary>Swift Horse's addition to getMovementSpeed, scaled like vanilla's riding sum.</summary>
    public static double SwiftHorseBonus(float movementMultiplier, int elapsedMs, bool diagonal)
        => SwiftHorseSpeed * movementMultiplier * elapsedMs * (diagonal ? DiagonalFactor : 1f);

    public static float GiftChance(float vanilla) => Math.Max(vanilla, PetGiftChance);

    public static int GiftThreshold(int vanilla) => Math.Min(vanilla, PetGiftFriendship);

    /// <summary>Horse Flute in the backpack at the loop start: needs Keep Horse (Ruling 6) and the row.</summary>
    public static bool GrantsHorseFlute(Func<string, bool> has) => has(KeepHorse) && has(HorseFlute);
}
