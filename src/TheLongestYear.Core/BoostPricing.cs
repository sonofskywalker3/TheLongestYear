using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Computed prices for the "20% twin" rows (spec 1.5, 1.6; ruling 10: earnable progress
/// only). Crash Course and Elevator Pass are priced off the keep row they twin.</summary>
public static class BoostPricing
{
    public const int MaxCrashCoursePerSkill = 2;
    public const int MaxSkillLevel = 10;
    public const int DeepestElevatorFloor = 120;
    private const double TwinFraction = 0.2;

    public static long CostOf(BoostDefinition def, RunState run, BoostContext ctx) => def.Id switch
    {
        BoostId.CrashCourse => ctx.Skill >= 0 && ctx.Skill < ctx.SkillLevels.Count
            ? CrashCourseCost(ctx.SkillLevels[ctx.Skill] + 1, run.SkillLevelsBoughtTotal)
            : 0,
        BoostId.ElevatorPass => ElevatorPassCost(ElevatorLanding(ctx.MineFloor)),
        _ => def.Cost,
    };

    /// <summary>0.2 x keepCost(target) x 3^(boughtSoFar): 10 / 60 / 90 for the ruling's worked example.</summary>
    public static long CrashCourseCost(int targetLevel, int boughtSoFar)
        => (long)Math.Round(TwinFraction * UpgradeCatalogGenerators.SkillKeepCost(targetLevel) * Math.Pow(3, boughtSoFar),
            MidpointRounding.AwayFromZero);

    public static bool CrashCourseAvailable(RunState run, BoostContext ctx)
    {
        if (ctx.Skill < 0 || ctx.Skill >= ctx.SkillLevels.Count) return false;
        if (run.SkillLevelsBoughtThisLoop.GetValueOrDefault(ctx.Skill) >= MaxCrashCoursePerSkill) return false;
        return ctx.SkillLevels[ctx.Skill] + 1 < MaxSkillLevel;
    }

    /// <summary>The next multiple of 10 above the current deepest elevator stop (35 lands 40, 40 lands 50).</summary>
    public static int ElevatorLanding(int floor) => Math.Min(DeepestElevatorFloor, (floor / 10 + 1) * 10);

    public static long ElevatorPassCost(int landing)
        => (long)Math.Round(TwinFraction * UpgradeCatalogGenerators.ElevatorKeepCost(landing), MidpointRounding.AwayFromZero);

    /// <summary>Not before the mine is entered (floor 0) and not at the bottom.</summary>
    public static bool ElevatorPassAvailable(int floor) => floor > 0 && floor < DeepestElevatorFloor;
}
