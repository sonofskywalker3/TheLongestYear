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

    /// <summary>Forgets the dealt hands so the next run deals fresh ones.</summary>
    public void ResetFocus()
    {
        (FocusHands ??= new()).Clear();
        FocusDealtRun = NoFocusDeal;
    }

    public FocusHand? FocusHandFor(FocusTheme theme)
        => FocusHands?.FirstOrDefault(h => h.Theme == theme);
}
