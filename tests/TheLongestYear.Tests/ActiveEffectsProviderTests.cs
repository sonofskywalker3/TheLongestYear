using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ActiveEffectsProviderTests
{
    [Fact]
    public void Initially_no_active_effects()
    {
        ActiveEffectsProvider.Clear();
        Assert.Null(ActiveEffectsProvider.BonusId);
        Assert.Null(ActiveEffectsProvider.LiabilityId);
    }

    [Fact]
    public void Set_stores_bonus_and_liability()
    {
        ActiveEffectsProvider.Set("forage_yield_up", "mines_closed");
        Assert.Equal("forage_yield_up", ActiveEffectsProvider.BonusId);
        Assert.Equal("mines_closed", ActiveEffectsProvider.LiabilityId);
        ActiveEffectsProvider.Clear();
    }

    [Fact]
    public void Clear_resets_to_null()
    {
        ActiveEffectsProvider.Set("crop_growth_up", "fish_bite_down");
        ActiveEffectsProvider.Clear();
        Assert.Null(ActiveEffectsProvider.BonusId);
        Assert.Null(ActiveEffectsProvider.LiabilityId);
    }

    [Fact]
    public void ActiveBonus_returns_true_for_current_id()
    {
        RunActivation.Activate();
        ActiveEffectsProvider.Set("fish_bite_up", "crop_growth_down");
        Assert.True(ActiveEffectsProvider.ActiveBonus("fish_bite_up"));
        Assert.False(ActiveEffectsProvider.ActiveBonus("mine_drops_up"));
        ActiveEffectsProvider.Clear();
        RunActivation.Deactivate();
    }

    [Fact]
    public void ActiveLiability_returns_true_for_current_id()
    {
        RunActivation.Activate();
        ActiveEffectsProvider.Set("mine_drops_up", "forage_off");
        Assert.True(ActiveEffectsProvider.ActiveLiability("forage_off"));
        Assert.False(ActiveEffectsProvider.ActiveLiability("all_sell_prices_down"));
        ActiveEffectsProvider.Clear();
        RunActivation.Deactivate();
    }

    // The dormant gate: even with a selection set, no effect is "active" while TLY is dormant
    // (a non-TLY save loaded with the mod installed). This is what keeps the gameplay patches
    // inert on saves the mod never started.
    [Fact]
    public void When_dormant_no_effect_is_active_even_if_set()
    {
        RunActivation.Deactivate();
        ActiveEffectsProvider.Set("fish_bite_up", "forage_off");
        Assert.False(ActiveEffectsProvider.ActiveBonus("fish_bite_up"));
        Assert.False(ActiveEffectsProvider.ActiveLiability("forage_off"));
        ActiveEffectsProvider.Clear();
    }
}
