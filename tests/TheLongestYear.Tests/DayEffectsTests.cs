using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class DayEffectsTests
{
    [Fact]
    public void Has_is_false_while_the_run_is_inactive()
    {
        RunActivation.Deactivate();
        DayEffects.Set(WildcardSchedule.SnowDay);
        Assert.False(DayEffects.Has(WildcardSchedule.SnowDay));
        DayEffects.Clear();
    }

    [Fact]
    public void Has_is_true_for_the_set_twist_only_while_active()
    {
        RunActivation.Activate();
        try
        {
            DayEffects.Set(WildcardSchedule.SnowDay);
            Assert.True(DayEffects.Has(WildcardSchedule.SnowDay));
            Assert.False(DayEffects.Has(WildcardSchedule.SellDown));
            Assert.Equal(WildcardSchedule.SnowDay, DayEffects.Today);
        }
        finally { DayEffects.Clear(); RunActivation.Deactivate(); }
    }

    [Fact]
    public void Clear_and_a_null_set_drop_the_effect()
    {
        RunActivation.Activate();
        try
        {
            DayEffects.Set(WildcardSchedule.FastBites);
            DayEffects.Clear();
            Assert.False(DayEffects.Has(WildcardSchedule.FastBites));
            Assert.Null(DayEffects.Today);
            DayEffects.Set(WildcardSchedule.FastBites);
            DayEffects.Set(null);
            Assert.Null(DayEffects.Today);
        }
        finally { DayEffects.Clear(); RunActivation.Deactivate(); }
    }
}
