using System;
using System.Diagnostics;
using TheLongestYear.Core.Obtainability;
using Xunit;
using Xunit.Abstractions;

namespace TheLongestYear.Tests;

/// <summary>A cache miss rebuilds the model on the player's machine, so the cold vanilla build has to
/// stay fast. The PC target is under 1 s; the budget is three times that so a slow machine passes.</summary>
public class ObtainabilityBenchTests
{
    private const int Runs = 3;
    private const long BudgetMs = 3000;

    private readonly ITestOutputHelper _output;

    public ObtainabilityBenchTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Bench")]
    public void ColdBuildUnderBudget()
    {
        ObtainabilityInputs inputs = VanillaInputsFixture.Load();
        long best = long.MaxValue;
        for (int i = 0; i < Runs; i++)
        {
            var watch = Stopwatch.StartNew();
            ObtainabilityBuilder.Build(inputs);
            best = Math.Min(best, watch.ElapsedMilliseconds);
        }
        _output.WriteLine($"best of {Runs} cold builds: {best} ms");
        Assert.True(best < BudgetMs, $"best of {Runs} cold builds took {best} ms (budget {BudgetMs} ms)");
    }
}
