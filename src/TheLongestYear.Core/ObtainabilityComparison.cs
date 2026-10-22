using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

public enum CompareVerdict { Agree, NewEarlier, NewLater, LuckOnly, OnlyExisting, OnlyNew }

/// <summary><see cref="NewDependableKnown"/> is the dependable landing week counting only sources the
/// model could actually read: where it differs from <see cref="NewDependable"/>, the headline week rests
/// on an unresolved source (a recipe whose unlock is "none", say) and is a guess, not a fact.</summary>
public sealed record CompareRow(
    string ItemId, int? ExistingPacing, int? ExistingHard, string ExistingBasis, int? NewDependable,
    int? NewDependableKnown, int? NewAny, CompareVerdict Verdict);

/// <summary>Lines the blind obtainability model up against the existing item model (spec
/// 2026-09-14-item-obtainability, phase 1). The existing hard week is a fact ("the first week the item
/// can exist at all"), so it is compared with the new model's dependable-only landing week from day 1;
/// LuckOnly marks an item the existing model places but the new model reaches only through a chance
/// source (the traveling cart, a monster drop), never a dependable one.</summary>
public static class ObtainabilityComparison
{
    private static readonly CompareVerdict[] SectionOrder =
        {
            CompareVerdict.NewEarlier, CompareVerdict.NewLater, CompareVerdict.LuckOnly,
            CompareVerdict.OnlyExisting, CompareVerdict.OnlyNew, CompareVerdict.Agree,
        };

    /// <summary>Dependable sources the model actually read: an unresolved source is a guess, so the
    /// report says when the headline week rests on one (the Queen of Sauce dishes, whose unlock is
    /// "none", are the big block of these).</summary>
    private static readonly ObtainFilter DependableKnownOnly = ObtainFilter.DependableOnly with { IncludeUnresolved = false };

    private static readonly CompareVerdict[] DetailSections =
        { CompareVerdict.NewEarlier, CompareVerdict.NewLater, CompareVerdict.LuckOnly, CompareVerdict.OnlyExisting };

    public static IReadOnlyList<CompareRow> Compare(
        IEnumerable<string> existingIds, Func<string, bool> existingPlaced,
        Func<string, (int Pacing, int Hard, string Basis)> existingWeeks, ObtainabilityModel model)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in existingIds) ids.Add(BundleParsing.NormalizeItemId(id));
        foreach (string id in model.ItemIds) ids.Add(id);

        var rows = new List<CompareRow>();
        foreach (string id in ids)
        {
            // Placed is checked first: asking the existing model about an unplaced id records it as unknown.
            (int Pacing, int Hard, string Basis)? existing = existingPlaced(id) ? existingWeeks(id) : null;
            int? newAny = model.LandingWeekFromDay1(id, ObtainFilter.Any);
            int? newDep = model.LandingWeekFromDay1(id, ObtainFilter.DependableOnly);
            int? newDepKnown = model.LandingWeekFromDay1(id, DependableKnownOnly);
            if (existing == null && newAny == null) continue;
            CompareVerdict verdict = existing == null ? CompareVerdict.OnlyNew
                : newAny == null ? CompareVerdict.OnlyExisting
                : newDep == null ? CompareVerdict.LuckOnly
                : newDep == existing.Value.Hard ? CompareVerdict.Agree
                : newDep < existing.Value.Hard ? CompareVerdict.NewEarlier
                : CompareVerdict.NewLater;
            rows.Add(new CompareRow(id, existing?.Pacing, existing?.Hard, existing?.Basis ?? "", newDep, newDepKnown, newAny, verdict));
        }
        return rows;
    }

    public static string Render(
        IReadOnlyList<CompareRow> rows, ObtainabilityModel model, IReadOnlyList<string> unresolved,
        Func<string, string?> nameOf, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Item obtainability comparison").AppendLine();
        sb.AppendLine($"Mod version {version}. Existing weeks are the current item model's pacing and hard weeks. New weeks are the blind obtainability model's landing week starting from Spring 1: dependable sources only, then any source. The verdict compares the existing hard week with the new dependable week; LuckOnly means only a chance source (the cart, a drop) reaches it. Weeks mean: start from nothing on the day, when does it first land.").AppendLine();
        sb.AppendLine("| Verdict | Items |").AppendLine("|---|---|");
        foreach (CompareVerdict v in SectionOrder)
            sb.AppendLine($"| {v} | {rows.Count(r => r.Verdict == v)} |");
        sb.AppendLine($"| Dependable only through an unresolved source | {rows.Count(OnlyUnresolvedDependable)} |");
        sb.AppendLine($"| Unresolved sources | {unresolved.Count} |");

        foreach (CompareVerdict v in DetailSections)
        {
            List<CompareRow> section = rows.Where(r => r.Verdict == v).ToList();
            if (section.Count == 0) continue;
            sb.AppendLine().AppendLine($"## {v}").AppendLine();
            foreach (CompareRow r in section)
            {
                sb.AppendLine($"### {r.ItemId} {nameOf(r.ItemId) ?? "?"}");
                sb.AppendLine($"- existing pacing {Week(r.ExistingPacing)}, hard {Week(r.ExistingHard)}; existing basis: {(r.ExistingBasis.Length == 0 ? "none" : r.ExistingBasis)}");
                string dependable = OnlyUnresolvedDependable(r)
                    ? $"{Week(r.NewDependable)} (unresolved; known-source week {Week(r.NewDependableKnown)})"
                    : Week(r.NewDependable);
                sb.AppendLine($"- new dependable {dependable}, any {Week(r.NewAny)}");
                foreach (ObtainSource s in model.Sources(r.ItemId))
                    sb.AppendLine($"  - {ObtainabilityText.SourceLine(s)}");
            }
        }

        List<CompareRow> agree = rows.Where(r => r.Verdict == CompareVerdict.Agree).ToList();
        if (agree.Count > 0)
        {
            sb.AppendLine().AppendLine("## Agree").AppendLine();
            sb.AppendLine("| Item | Name | Week |").AppendLine("|---|---|---|");
            foreach (CompareRow r in agree)
                sb.AppendLine($"| {r.ItemId} | {nameOf(r.ItemId) ?? "?"} | {Week(r.NewDependable)} |");
        }

        List<CompareRow> onlyNew = rows.Where(r => r.Verdict == CompareVerdict.OnlyNew).ToList();
        if (onlyNew.Count > 0)
        {
            sb.AppendLine().AppendLine("## OnlyNew (names only)").AppendLine();
            foreach (CompareRow r in onlyNew)
                sb.AppendLine($"- {r.ItemId} {nameOf(r.ItemId) ?? "?"}");
        }

        sb.AppendLine().AppendLine($"## Unresolved sources ({unresolved.Count})").AppendLine();
        foreach (string u in unresolved) sb.AppendLine($"- {u}");
        return sb.ToString();
    }

    /// <summary>The headline dependable week comes from a source the model could not read.</summary>
    private static bool OnlyUnresolvedDependable(CompareRow row)
        => row.NewDependable != null && row.NewDependable != row.NewDependableKnown;

    private static string Week(int? week) => week?.ToString() ?? "none";
}
