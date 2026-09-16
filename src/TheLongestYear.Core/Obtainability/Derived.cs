using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How a chained source reads the item it is derived from (a seed, a sapling, a trade item,
/// a machine input, an ingredient, a pond fish, a geode).
/// <para>Ginger Island, year 2 and owned-only sources are recorded but never counted by default
/// (spec 2026-09-14-obtainability-phase2, non-goals; owned-only per the 2026-09-16 animal ruling).
/// Reading an input through the default filters alone would lose an island-only, year 2-only or
/// owned-only input entirely; reading it with all three included and then flagging the result only
/// when EVERY upstream source is flagged is worse still, because a mixed input (a year 2 shop row
/// beside a chance cart row) would let the year 2 row's landing day into the default headline. So an
/// input is read under all EIGHT filter variants (island x year 2 x owned-only) and each variant is
/// derived separately, carrying its own flags.</para>
/// <para><b>What this buys, and it is the property the tests pin:</b> for any filter F, the derived
/// item's table under F is what deriving from the input's own table under F gives. Each variant's
/// tables come from exactly that filter, the chain applied to them is monotone in the input table,
/// and a variant's sources are counted only when F includes the variant's flags. A year 2 row, or an
/// owned-only row (a not-sold animal's produce), can therefore never reach an answer that excludes
/// it.</para>
/// <para><b>Undelayed tables.</b> Every variant also carries the input's tables from before the mine
/// travel (<see cref="MineDepth"/>): the same Earliest, read over each source's
/// <see cref="ObtainSource.UndelayedLands"/> where it has one. The chain is applied to both, so a
/// derived source's <see cref="ObtainSource.UndelayedLands"/> is exactly what the model would have
/// said for it with no travel anywhere, and the same property holds for the undelayed side.</para></summary>
public static class Derived
{
    /// <summary>A dependable table and an any table, read the same way.</summary>
    public sealed record Tables(DayTable Dependable, DayTable Any)
    {
        public static readonly Tables None = new(DayTable.None, DayTable.None);

        public bool IsEmpty => Any.IsEmpty && Dependable.IsEmpty;

        public Tables Earliest(Tables other) => new(Dependable.Earliest(other.Dependable), Any.Earliest(other.Any));

        public Tables Latest(Tables other) => new(Dependable.Latest(other.Dependable), Any.Latest(other.Any));
    }

    /// <summary>One filter variant of an input: its tables with and without the mine travel, and the
    /// flags that name it.</summary>
    public sealed record Variant(bool GingerIsland, bool YearTwo, bool OwnedOnly, Tables Delayed, Tables Undelayed)
    {
        public ObtainConditions Flag(ObtainConditions conditions)
            => conditions with
            {
                GingerIsland = conditions.GingerIsland || GingerIsland,
                YearTwo = conditions.YearTwo || YearTwo,
                OwnedOnly = conditions.OwnedOnly || OwnedOnly,
            };

        public bool SameTables(Variant other) => Delayed.Equals(other.Delayed) && Undelayed.Equals(other.Undelayed);

        public bool IsEmpty => Delayed.IsEmpty && Undelayed.IsEmpty;

        /// <summary>True when this variant needs no flag <paramref name="other"/> does not need.</summary>
        public bool Within(Variant other) => Within(other.GingerIsland, other.YearTwo, other.OwnedOnly);

        public bool Within(bool gingerIsland, bool yearTwo, bool ownedOnly)
            => (!GingerIsland || gingerIsland) && (!YearTwo || yearTwo) && (!OwnedOnly || ownedOnly);

        public bool Named(bool gingerIsland, bool yearTwo, bool ownedOnly)
            => GingerIsland == gingerIsland && YearTwo == yearTwo && OwnedOnly == ownedOnly;
    }

    /// <summary>Every flag combination, lesser before greater (a variant's flags are a bit subset of
    /// any later one's only if they come first), which <see cref="Input"/>'s trim relies on.</summary>
    private static readonly (bool Island, bool YearTwo, bool OwnedOnly)[] AllFlags =
    {
        (false, false, false), (false, false, true), (false, true, false), (false, true, true),
        (true, false, false), (true, false, true), (true, true, false), (true, true, true),
    };

    /// <summary>An input read under every filter variant that says something new. The plain variant
    /// (no island, no year 2, not owned-only) is always first.</summary>
    public sealed record Input(IReadOnlyList<Variant> Variants)
    {
        private const bool Plain = false;

        public static readonly Input Nothing = Of(DayTable.None);

        /// <summary>Needs nothing (a machine rule with no input): lands the day it starts.</summary>
        public static readonly Input Free = Of(DayTable.Always);

        /// <summary>Which items this input stands for, as groups: any member of a group serves, every
        /// group is needed. Stamped onto every source <see cref="Emit"/> yields so a consumer can ask
        /// the same obtainability question of the input as of the output.
        /// <para><b>Known limitation:</b> <see cref="Either"/> flattens into ONE group, so a
        /// conjunction nested inside an alternative (either this pair or that pair) becomes a plain
        /// disjunction of all four ids, which is weaker than the truth: a consumer may accept a route
        /// because one id of a needed pair is obtainable. No current caller builds that shape
        /// (Both is only used for a recipe's ingredient list, never under Either), and the loss is on
        /// the permissive side, so it is recorded rather than modelled.</para></summary>
        public IReadOnlyList<IReadOnlyList<string>> Groups { get; init; } = Array.Empty<IReadOnlyList<string>>();

        private static Input Of(DayTable both)
        {
            var tables = new Tables(both, both);
            return new(new[] { new Variant(Plain, Plain, Plain, tables, tables) });
        }

        public Variant PlainVariant => Variants[0];

        public bool IsEmpty => Variants.All(v => v.Delayed.Any.IsEmpty && v.Undelayed.Any.IsEmpty);

        /// <summary>The input under exactly these flags. A combination the trim dropped said nothing a
        /// lesser one had not, so it is rebuilt as the Earliest over every kept variant whose flags it
        /// includes: a filter with more flags counts every source a lesser one does, so each of those is
        /// no sooner than the dropped variant, and the one it was dropped for equals it.</summary>
        public Variant For(bool gingerIsland, bool yearTwo, bool ownedOnly)
        {
            Variant? exact = Variants.FirstOrDefault(v => v.Named(gingerIsland, yearTwo, ownedOnly));
            if (exact != null) return exact;
            Tables delayed = Tables.None;
            Tables undelayed = Tables.None;
            foreach (Variant v in Variants.Where(v => v.Within(gingerIsland, yearTwo, ownedOnly)))
            {
                delayed = delayed.Earliest(v.Delayed);
                undelayed = undelayed.Earliest(v.Undelayed);
            }
            return new Variant(gingerIsland, yearTwo, ownedOnly, delayed, undelayed);
        }

        /// <summary>One derived source group per variant: <paramref name="chain"/> is the same
        /// start-to-landing maths for each, applied to that variant's tables (with and without the mine
        /// travel), with its flags ORed onto the conditions. <paramref name="luck"/> forces the
        /// dependable half away (a random output, a chance roll), exactly as the single-table callers
        /// used to.</summary>
        public IEnumerable<ObtainSource> Emit(
            SourceKind kind, Func<DayTable, DayTable> chain, ObtainConditions conditions, string detail,
            IReadOnlyList<SetupStep>? setup = null, bool luck = false)
        {
            foreach (Variant v in Variants)
                foreach (ObtainSource source in SourcePair.Of(
                    kind,
                    luck ? DayTable.None : chain(v.Delayed.Dependable), chain(v.Delayed.Any),
                    luck ? DayTable.None : chain(v.Undelayed.Dependable), chain(v.Undelayed.Any),
                    v.Flag(conditions), detail, setup))
                    yield return source with { Inputs = Groups };
        }

        /// <summary>Either input will do (a machine that takes any of several items): variant by
        /// variant, the sooner landing, and the two sides' ids merged into one group.</summary>
        public Input Either(Input other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return Combine(other, (a, b) => a.Earliest(b), Merge(Groups, other.Groups));
        }

        /// <summary>Both inputs are needed (a recipe's ingredients): variant by variant, the later
        /// landing, never when either side never lands, and both sides' groups kept side by side.</summary>
        public Input Both(Input other)
            => Combine(other, (a, b) => a.Latest(b), Groups.Concat(other.Groups).ToList());

        /// <summary>One group holding every id of both sides (see the limitation on <see cref="Groups"/>).</summary>
        private static IReadOnlyList<IReadOnlyList<string>> Merge(
            IReadOnlyList<IReadOnlyList<string>> left, IReadOnlyList<IReadOnlyList<string>> right)
        {
            var ids = left.Concat(right).SelectMany(g => g).Distinct(StringComparer.Ordinal).ToList();
            return ids.Count == 0 ? Array.Empty<IReadOnlyList<string>>() : new IReadOnlyList<string>[] { ids };
        }

        /// <summary>Combines the two sides under every flag combination, not only the ones either side
        /// kept: an island-only side joined with a year 2-only side says something new under island
        /// plus year 2 that neither says alone. The trim drops what adds nothing.</summary>
        private Input Combine(
            Input other, Func<Tables, Tables, Tables> f, IReadOnlyList<IReadOnlyList<string>> groups)
        {
            var built = new List<Variant>();
            foreach ((bool island, bool yearTwo, bool ownedOnly) in AllFlags)
            {
                Variant a = For(island, yearTwo, ownedOnly);
                Variant b = other.For(island, yearTwo, ownedOnly);
                built.Add(new Variant(island, yearTwo, ownedOnly, f(a.Delayed, b.Delayed), f(a.Undelayed, b.Undelayed)));
            }
            return new Input(Trim(built)) { Groups = groups };
        }

        /// <summary>Drops every variant that says exactly what a lesser variant already said, so an
        /// input with no island, year 2 or owned-only sources keeps only its plain variant.</summary>
        private static IReadOnlyList<Variant> Trim(IReadOnlyList<Variant> all)
        {
            var kept = new List<Variant> { all[0] };
            foreach (Variant v in all.Skip(1))
                if (!kept.Any(k => k.Within(v) && k.SameTables(v)))
                    kept.Add(v);
            return kept;
        }

        internal static Input FromVariants(IReadOnlyList<Variant> all, IReadOnlyList<IReadOnlyList<string>> groups)
            => new(Trim(all)) { Groups = groups };
    }

    /// <summary>One input id read out of the previous pass's model, under all eight filter variants
    /// (island x year 2 x owned-only), with and without the mine travel.</summary>
    public static Input Of(ObtainabilityModel snapshot, string itemId)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        var variants = new List<Variant>();
        foreach ((bool island, bool yearTwo, bool owned) in AllFlags)
        {
            ObtainFilter dependable = ObtainFilter.DependableOnly with
            {
                IncludeGingerIsland = island, IncludeYearTwo = yearTwo, IncludeOwnedOnly = owned,
            };
            ObtainFilter any = ObtainFilter.Any with
            {
                IncludeGingerIsland = island, IncludeYearTwo = yearTwo, IncludeOwnedOnly = owned,
            };
            variants.Add(new Variant(island, yearTwo, owned,
                new Tables(snapshot.Table(itemId, dependable), snapshot.Table(itemId, any)),
                new Tables(snapshot.UndelayedTable(itemId, dependable), snapshot.UndelayedTable(itemId, any))));
        }
        return Input.FromVariants(variants, new IReadOnlyList<string>[] { new[] { itemId } });
    }

    /// <summary>The sooner of several inputs (see <see cref="Input.Either"/>).</summary>
    public static Input Sooner(IEnumerable<Input> inputs)
        => inputs.Aggregate(Input.Nothing, (a, b) => a.Either(b));
}
