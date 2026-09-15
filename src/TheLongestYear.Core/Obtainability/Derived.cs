using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How a chained source reads the item it is derived from (a seed, a sapling, a trade item,
/// a machine input, an ingredient, a pond fish, a geode).
/// <para>Ginger Island and year 2 sources are recorded but never counted by default (spec
/// 2026-09-14-obtainability-phase2, non-goals). Reading an input through the default filters alone
/// would lose an island-only or year 2-only input entirely; reading it with both included and then
/// flagging the result only when EVERY upstream source is flagged is worse still, because a mixed
/// input (a year 2 shop row beside a chance cart row) would let the year 2 row's landing day into the
/// default headline. So an input is read under all FOUR filter variants and each variant is derived
/// separately, carrying its own flags.</para>
/// <para><b>What this buys, and it is the property the tests pin:</b> for any filter F, the derived
/// item's table under F is what deriving from the input's own table under F gives. Each variant's
/// tables come from exactly that filter, the chain applied to them is monotone in the input table,
/// and a variant's sources are counted only when F includes the variant's flags. A year 2 row can
/// therefore never reach an answer that excludes year 2.</para></summary>
public static class Derived
{
    /// <summary>One filter variant of an input: its two tables, and the flags that name it.</summary>
    public sealed record Variant(bool GingerIsland, bool YearTwo, DayTable Dependable, DayTable Any)
    {
        public ObtainConditions Flag(ObtainConditions conditions)
            => conditions with
            {
                GingerIsland = conditions.GingerIsland || GingerIsland,
                YearTwo = conditions.YearTwo || YearTwo,
            };

        public bool SameTables(Variant other) => Dependable.Equals(other.Dependable) && Any.Equals(other.Any);

        public bool IsEmpty => Any.IsEmpty && Dependable.IsEmpty;
    }

    /// <summary>An input read under every filter variant that says something new. The plain variant
    /// (no island, no year 2) is always first and is the fallback when a variant is missing.</summary>
    public sealed record Input(IReadOnlyList<Variant> Variants)
    {
        private const bool Plain = false;

        public static readonly Input Nothing = Of(DayTable.None);

        /// <summary>Needs nothing (a machine rule with no input): lands the day it starts.</summary>
        public static readonly Input Free = Of(DayTable.Always);

        private static Input Of(DayTable both) => new(new[] { new Variant(Plain, Plain, both, both) });

        public Variant PlainVariant => Variants[0];

        public bool IsEmpty => Variants.All(v => v.Any.IsEmpty);

        /// <summary>The variant with these exact flags, or the plain one when this input has nothing
        /// of its own to say about them.</summary>
        public Variant For(bool gingerIsland, bool yearTwo)
            => Variants.FirstOrDefault(v => v.GingerIsland == gingerIsland && v.YearTwo == yearTwo) ?? PlainVariant;

        /// <summary>One derived source group per variant: <paramref name="chain"/> is the same
        /// start-to-landing maths for each, applied to that variant's tables, with its flags ORed onto
        /// the conditions. <paramref name="luck"/> forces the dependable half away (a random output, a
        /// chance roll), exactly as the single-table callers used to.</summary>
        public IEnumerable<ObtainSource> Emit(
            SourceKind kind, Func<DayTable, DayTable> chain, ObtainConditions conditions, string detail,
            IReadOnlyList<SetupStep>? setup = null, bool luck = false)
        {
            foreach (Variant v in Variants)
                foreach (ObtainSource source in SourcePair.Of(
                    kind, luck ? DayTable.None : chain(v.Dependable), chain(v.Any), v.Flag(conditions), detail, setup))
                    yield return source;
        }

        /// <summary>Either input will do (a machine that takes any of several items): variant by
        /// variant, the sooner landing.</summary>
        public Input Either(Input other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return Combine(other, (a, b) => (a.Dependable.Earliest(b.Dependable), a.Any.Earliest(b.Any)));
        }

        /// <summary>Both inputs are needed (a recipe's ingredients): variant by variant, the later
        /// landing, never when either side never lands.</summary>
        public Input Both(Input other)
            => Combine(other, (a, b) => (a.Dependable.Latest(b.Dependable), a.Any.Latest(b.Any)));

        private Input Combine(Input other, Func<Variant, Variant, (DayTable Dependable, DayTable Any)> f)
        {
            var keys = new List<(bool Island, bool YearTwo)> { (false, false) };
            foreach (Variant v in Variants.Concat(other.Variants))
                if (!keys.Contains((v.GingerIsland, v.YearTwo))) keys.Add((v.GingerIsland, v.YearTwo));
            var built = new List<Variant>();
            foreach ((bool island, bool yearTwo) in keys)
            {
                (DayTable dependable, DayTable any) = f(For(island, yearTwo), other.For(island, yearTwo));
                built.Add(new Variant(island, yearTwo, dependable, any));
            }
            return new Input(Trim(built));
        }

        /// <summary>Drops every variant that says exactly what a lesser variant already said, so an
        /// input with no island or year 2 sources keeps only its plain variant.</summary>
        private static IReadOnlyList<Variant> Trim(IReadOnlyList<Variant> all)
        {
            var kept = new List<Variant> { all[0] };
            foreach (Variant v in all.Skip(1))
                if (!kept.Any(k => LessThan(k, v) && k.SameTables(v)))
                    kept.Add(v);
            return kept;
        }

        /// <summary>A variant is lesser when it needs no flag the other does not need.</summary>
        private static bool LessThan(Variant lesser, Variant v)
            => (!lesser.GingerIsland || v.GingerIsland) && (!lesser.YearTwo || v.YearTwo)
               && (lesser.GingerIsland != v.GingerIsland || lesser.YearTwo != v.YearTwo);

        internal static Input FromVariants(IReadOnlyList<Variant> all) => new(Trim(all));
    }

    /// <summary>One input id read out of the previous pass's model, under all four filter variants.</summary>
    public static Input Of(ObtainabilityModel snapshot, string itemId)
    {
        Variant Read(bool island, bool yearTwo) => new(island, yearTwo,
            snapshot.Table(itemId, ObtainFilter.DependableOnly with { IncludeGingerIsland = island, IncludeYearTwo = yearTwo }),
            snapshot.Table(itemId, ObtainFilter.Any with { IncludeGingerIsland = island, IncludeYearTwo = yearTwo }));
        return Input.FromVariants(new[] { Read(false, false), Read(true, false), Read(false, true), Read(true, true) });
    }

    /// <summary>The sooner of several inputs (see <see cref="Input.Either"/>).</summary>
    public static Input Sooner(IEnumerable<Input> inputs)
        => inputs.Aggregate(Input.Nothing, (a, b) => a.Either(b));
}
