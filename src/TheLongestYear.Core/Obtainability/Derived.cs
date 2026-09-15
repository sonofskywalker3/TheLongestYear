using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How a chained source reads the item it is derived from (a seed, a sapling, a trade item,
/// a machine input, an ingredient, a pond fish, a geode).
/// <para>Ginger Island and year 2 sources are recorded but never counted by default (spec
/// 2026-09-14-obtainability-phase2, non-goals). Reading an input through the default filters would
/// therefore make an island-only or year-2-only input look like NO input at all, and the derived
/// source would vanish instead of being recorded with the flag. So every derivation reads its input
/// with both included and ORs the input's flags onto the derived source's conditions.</para></summary>
public static class Derived
{
    public static readonly ObtainFilter AnyFlagged =
        ObtainFilter.Any with { IncludeGingerIsland = true, IncludeYearTwo = true };

    public static readonly ObtainFilter DependableFlagged =
        ObtainFilter.DependableOnly with { IncludeGingerIsland = true, IncludeYearTwo = true };

    /// <summary>An input's two tables plus the flags the derived source inherits: the input is island
    /// gated only when EVERY source it has is island gated, and year 2 only when every one is.</summary>
    public sealed record Input(DayTable Dependable, DayTable Any, bool GingerIsland, bool YearTwo)
    {
        /// <summary>No source at all: a derivation from this can never land.</summary>
        public static readonly Input Nothing = new(DayTable.None, DayTable.None, false, false);

        /// <summary>Needs nothing (a machine rule with no input): lands the day it starts.</summary>
        public static readonly Input Free = new(DayTable.Always, DayTable.Always, false, false);

        public bool IsEmpty => Any.IsEmpty;

        /// <summary>ORs the input's flags onto a derived source's conditions.</summary>
        public ObtainConditions Flag(ObtainConditions conditions)
            => conditions with
            {
                GingerIsland = conditions.GingerIsland || GingerIsland,
                YearTwo = conditions.YearTwo || YearTwo,
            };

        /// <summary>Either input will do (a machine that takes any of several items): the sooner
        /// landing, and the flag only when both sides carry it.</summary>
        public Input Either(Input other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return new Input(
                Dependable.Earliest(other.Dependable), Any.Earliest(other.Any),
                GingerIsland && other.GingerIsland, YearTwo && other.YearTwo);
        }

        /// <summary>Both inputs are needed (a recipe's ingredients): the later landing, and the flag
        /// as soon as either side carries it.</summary>
        public Input Both(Input other)
            => new(Dependable.Latest(other.Dependable), Any.Latest(other.Any),
                GingerIsland || other.GingerIsland, YearTwo || other.YearTwo);
    }

    /// <summary>One input id read out of the previous pass's model.</summary>
    public static Input Of(ObtainabilityModel snapshot, string itemId)
    {
        List<ObtainSource> sources = snapshot.Sources(itemId).Where(AnyFlagged.Accepts).ToList();
        if (sources.Count == 0) return Input.Nothing;
        return new Input(
            snapshot.Table(itemId, DependableFlagged), snapshot.Table(itemId, AnyFlagged),
            sources.All(s => s.Conditions.GingerIsland), sources.All(s => s.Conditions.YearTwo));
    }

    /// <summary>The sooner of several inputs (see <see cref="Input.Either"/>).</summary>
    public static Input Sooner(IEnumerable<Input> inputs)
        => inputs.Aggregate(Input.Nothing, (a, b) => a.Either(b));
}
