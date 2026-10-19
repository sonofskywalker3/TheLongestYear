using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One upgrade ladder a focus card can be about. <c>Rungs[0]</c> is rung 1; each entry is
/// a verbatim catalog upgrade id.</summary>
public sealed record FocusChain(string Id, FocusTheme Theme, IReadOnlyList<string> Rungs);
