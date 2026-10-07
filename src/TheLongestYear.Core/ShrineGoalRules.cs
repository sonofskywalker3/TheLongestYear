using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The decision rules around random shrine donation goals (Randomizer, spec section 7):
/// which items may become one, how many a list asks, when an item credits one, what it pays and
/// how the goals join their theme list's progress.</summary>
public static class ShrineGoalRules
{
    /// <summary>Every item id and category ref with a slot on the live board.</summary>
    public sealed record BoardRefs(IReadOnlySet<string> ItemIds, IReadOnlySet<int> Categories);

    /// <summary>Reads the board's item ids (qualified) and category refs from raw BundleData.</summary>
    public static BoardRefs ReadBoard(IEnumerable<KeyValuePair<string, string>>? bundleData)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var categories = new HashSet<int>();
        foreach (KeyValuePair<string, string> kv in bundleData ?? Array.Empty<KeyValuePair<string, string>>())
        {
            if (kv.Key == null || kv.Value == null) continue;
            foreach (BundleIngredient ing in BundleParsing.Parse(kv.Key, kv.Value).Ingredients)
            {
                if (BundleParsing.IsCategoryRef(ing.ItemRef))
                {
                    if (int.TryParse(ing.ItemRef, out int cat)) categories.Add(cat);
                }
                else
                {
                    ids.Add(BundleParsing.NormalizeItemId(ing.ItemRef));
                }
            }
        }
        return new BoardRefs(ids, categories);
    }

    /// <summary>The theme's items that may become a shrine goal: not excluded from the pools, not a
    /// legendary fish, without a slot on the board (by id or by a category ref the item falls in)
    /// and not in <paramref name="alreadyTaken"/> (the other list's goals). Qualified, sorted.</summary>
    public static IReadOnlyList<string> Candidates(IEnumerable<string> themeIds, IReadOnlySet<string>? excludedIds,
        BoardRefs board, Func<string, int?> categoryOf, IEnumerable<string>? alreadyTaken = null)
    {
        var taken = new HashSet<string>((alreadyTaken ?? Array.Empty<string>()).Select(BundleParsing.NormalizeItemId), StringComparer.Ordinal);
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string raw in themeIds ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string id = BundleParsing.NormalizeItemId(raw);
            if (excludedIds != null && (excludedIds.Contains(id) || excludedIds.Contains(raw))) continue;
            if (LegendaryFishRules.IsLegendary(id)) continue;
            if (board.ItemIds.Contains(id) || taken.Contains(id)) continue;
            int? category = categoryOf?.Invoke(id);
            if (category.HasValue && board.Categories.Contains(category.Value)) continue;
            result.Add(id);
        }
        return result.ToList();
    }

    /// <summary>How many shrine goals a list gets: the gap between its CC goals and the target.</summary>
    public static int CountFor(DifficultyStep requiredSlots, int ccGoals)
        => Math.Max(0, ShrineGoalSampler.TargetFor(requiredSlots) - Math.Max(0, ccGoals));

    /// <summary>Salt of the stack rolls (one stream per list, drawn in pick order).</summary>
    public const int StackSalt = 0x6A20;

    /// <summary>The goals for picked ids: each stack from <see cref="ShrineStack.For"/> with the
    /// item's basis and clamps, then lowered by the theme week discount like a board line.</summary>
    public static List<ShrineGoal> Build(IReadOnlyList<string> pickedIds, int listIndex, DifficultyProfile profile,
        Random rng, Func<string, double?> basisOf, Func<string, int, int> clamps, double weekDiscount)
    {
        var goals = new List<ShrineGoal>();
        foreach (string id in pickedIds)
        {
            int stack = ShrineStack.For(basisOf(id), profile, rng, s => clamps(id, s));
            goals.Add(new ShrineGoal
            {
                ItemId = id,
                Stack = WeeklyGoalDiscount.Stack(stack, weekDiscount),
                ListIndex = listIndex,
            });
        }
        return goals;
    }

    /// <summary>An item credits a goal only when it is the goal's item (any quality), there is
    /// enough of it in the one stack, and the goal is still open.</summary>
    public static bool CanCredit(ShrineGoal? goal, string? qualifiedItemId, int quality, int itemStack)
        => goal != null
           && !goal.Deposited
           && !string.IsNullOrEmpty(qualifiedItemId)
           && string.Equals(BundleParsing.NormalizeItemId(goal.ItemId), BundleParsing.NormalizeItemId(qualifiedItemId), StringComparison.Ordinal)
           && quality >= 0
           && itemStack >= goal.Stack;

    /// <summary>The JP a shrine donation pays before boosts: what a CC donation of the same stack
    /// pays (per item x stack), times the goal-slot bonus and the list's card multiplier.</summary>
    public static long DonationJp(long perItem, int stack, double selectionBonus, double listMultiplier)
        => (long)Math.Round(perItem * (double)stack * selectionBonus * listMultiplier, MidpointRounding.AwayFromZero);

    /// <summary>The goals of one list.</summary>
    public static IReadOnlyList<ShrineGoal> OfList(IEnumerable<ShrineGoal>? goals, int listIndex)
        => (goals ?? Array.Empty<ShrineGoal>()).Where(g => g != null && g.ListIndex == listIndex).ToList();

    /// <summary>A list's progress across both goal kinds: a shrine goal is done when deposited.</summary>
    public static (int Done, int Total) Tally(int ccDone, int ccTotal, IReadOnlyList<ShrineGoal> listGoals)
    {
        int shrineDone = listGoals.Count(g => g.Deposited);
        return (ccDone + shrineDone, ccTotal + listGoals.Count);
    }

    /// <summary>Marks every deposited, unpaid goal as paid and returns how many it marked.</summary>
    public static int MarkPaid(IReadOnlyList<ShrineGoal> listGoals)
    {
        int marked = 0;
        foreach (ShrineGoal g in listGoals)
        {
            if (!g.Deposited || g.Paid) continue;
            g.Paid = true;
            marked++;
        }
        return marked;
    }

    /// <summary>The empty-pool lift applies only when the list has neither kind of goal.</summary>
    public static bool ListIsEmpty(int ccGoals, IReadOnlyList<ShrineGoal> listGoals)
        => ccGoals == 0 && listGoals.Count == 0;
}
