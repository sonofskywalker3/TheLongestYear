using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public sealed partial class RunState
{
    private const int NoFocusDeal = -1;

    /// <summary>This run's three dealt focus hands, in theme order (empty until dealt).</summary>
    public List<FocusHand> FocusHands { get; set; } = new();

    /// <summary>The run number the hands were dealt for; anything else means a deal is due.</summary>
    public int FocusDealtRun { get; set; } = NoFocusDeal;

    /// <summary>The farmhouse level a bought house card is bringing overnight (1 kitchen, 3
    /// basement), or -1 when none is on its way.</summary>
    public int FocusHouseTarget { get; set; } = NoFocusHouseTarget;

    public const int NoFocusHouseTarget = -1;

    /// <summary>Forgets the dealt hands so the next run deals fresh ones.</summary>
    public void ResetFocus()
    {
        (FocusHands ??= new()).Clear();
        FocusDealtRun = NoFocusDeal;
        FocusHouseTarget = NoFocusHouseTarget;
    }

    public FocusHand? FocusHandFor(FocusTheme theme)
        => FocusHands?.FirstOrDefault(h => h.Theme == theme);
}
