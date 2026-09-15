using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>A data section failed to read, so no model is published: a model with a missing section
/// looks complete and would mislead any consumer (spec 2026-09-14-obtainability-phase2 section 2).</summary>
public sealed class ObtainabilityReadException : Exception
{
    public IReadOnlyList<string> FailedSections { get; }

    public ObtainabilityReadException(IReadOnlyList<string> failedSections)
        : base("Obtainability: no model published, these sections failed to read: " + string.Join(", ", failedSections))
        => FailedSections = failedSections;
}
