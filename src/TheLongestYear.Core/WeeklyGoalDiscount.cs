using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The theme week discount (spec 2026-09-29-theme-week-discount): the week's goal lines
/// ask for less while that week's theme is picked. The discount comes from the run's Stack size
/// dial (<see cref="DifficultyProfile.WeeklyGoalStackDiscount"/>).</summary>
public static class WeeklyGoalDiscount
{
    /// <summary>Lines asking for this many or fewer are left alone, and no discount goes below it.</summary>
    public const int Floor = 10;

    /// <summary>The discounted ask: <c>max(10, floor(original x (1 - discount)))</c> for a line
    /// asking for more than 10, the original otherwise. Always rounds down.</summary>
    public static int Stack(int original, double discount)
    {
        if (original <= Floor || discount <= 0.0)
            return original;
        int lowered = (int)Math.Floor(original * (1.0 - discount));
        return Math.Max(Floor, lowered);
    }

    /// <summary>One stack rewrite on one ingredient line of one bundle.</summary>
    public readonly record struct StackEdit(string Key, int IngredientIndex, int Stack);

    private const int IngredientFieldIndex = 2;
    private const int TokensPerIngredient = 3;

    /// <summary>The stack of ingredient line <paramref name="ingredientIndex"/> in a raw BundleData value, or null.</summary>
    public static int? StackAt(string value, int ingredientIndex)
    {
        string[]? tokens = IngredientTokens(value);
        int at = ingredientIndex * TokensPerIngredient;
        if (tokens == null || ingredientIndex < 0 || at + 1 >= tokens.Length) return null;
        return int.TryParse(tokens[at + 1], out int stack) ? stack : null;
    }

    /// <summary>The value with one line's stack set to <paramref name="stack"/>, every other byte
    /// kept, or null when the line is missing or already holds that stack.</summary>
    public static string? WithStack(string value, int ingredientIndex, int stack)
    {
        int? current = StackAt(value, ingredientIndex);
        if (current == null || current.Value == stack) return null;
        string[] fields = value.Split('/');
        string[] tokens = IngredientTokens(value)!;
        tokens[ingredientIndex * TokensPerIngredient + 1] = stack.ToString();
        fields[IngredientFieldIndex] = string.Join(" ", tokens);
        return string.Join("/", fields);
    }

    /// <summary>Lower every goal line not already discounted. Records the full ask in
    /// <see cref="BonusSlot.OriginalStack"/> and the new one in <see cref="BonusSlot.Stack"/>;
    /// returns the board writes to make. A slot whose line no longer names its item is skipped.</summary>
    public static IReadOnlyList<StackEdit> Apply(
        IList<BonusSlot> slots, IReadOnlyDictionary<string, string> board, double discount)
    {
        var edits = new List<StackEdit>();
        if (slots == null || board == null || discount <= 0.0) return edits;
        foreach (BonusSlot slot in slots)
        {
            if (slot.OriginalStack > 0) continue;
            string? key = KeyFor(board, slot.BundleIndex);
            if (key == null || !LineNames(board[key], slot.IngredientIndex, slot.ItemId)) continue;
            int? live = StackAt(board[key], slot.IngredientIndex);
            if (live == null) continue;
            int lowered = Stack(live.Value, discount);
            if (lowered == live.Value) continue;
            slot.OriginalStack = live.Value;
            slot.Stack = lowered;
            edits.Add(new StackEdit(key, slot.IngredientIndex, lowered));
        }
        return edits;
    }

    /// <summary>Put every discounted line back to its full ask, except a donated line (it stays
    /// done at the discounted stack) and a line whose live stack is no longer the discounted one
    /// (another mod changed the board underneath). Clears <see cref="BonusSlot.OriginalStack"/> on
    /// every discounted slot; returns the board writes to make.</summary>
    public static IReadOnlyList<StackEdit> Revert(
        IList<BonusSlot> slots, IReadOnlyDictionary<string, string> board, Func<BonusSlot, bool> isDonated)
    {
        if (isDonated == null) throw new ArgumentNullException(nameof(isDonated));
        var edits = new List<StackEdit>();
        if (slots == null) return edits;
        foreach (BonusSlot slot in slots)
        {
            if (slot.OriginalStack <= 0) continue;
            int original = slot.OriginalStack;
            slot.OriginalStack = 0;
            if (isDonated(slot) || board == null) continue;
            string? key = KeyFor(board, slot.BundleIndex);
            if (key == null || StackAt(board[key], slot.IngredientIndex) != slot.Stack) continue;
            slot.Stack = original;
            edits.Add(new StackEdit(key, slot.IngredientIndex, original));
        }
        return edits;
    }

    /// <summary>What the planning hub shows for a theme before it is picked: copies of the sampled
    /// slots with the discounted ask. A line this week's pick already lowered on the board (in
    /// <paramref name="currentWeek"/> with an <see cref="BonusSlot.OriginalStack"/>) starts from its
    /// full ask, so reopening the hub mid-week never lowers it twice.</summary>
    public static List<BonusSlot> Preview(
        IEnumerable<BonusSlot> sample, IEnumerable<BonusSlot> currentWeek, double discount)
    {
        var fullAsk = new Dictionary<(int, int), int>();
        foreach (BonusSlot s in currentWeek ?? Array.Empty<BonusSlot>())
            if (s.OriginalStack > 0)
                fullAsk[(s.BundleIndex, s.IngredientIndex)] = s.OriginalStack;

        var shown = new List<BonusSlot>();
        foreach (BonusSlot s in sample ?? Array.Empty<BonusSlot>())
        {
            int full = fullAsk.TryGetValue((s.BundleIndex, s.IngredientIndex), out int original) ? original : s.Stack;
            shown.Add(new BonusSlot
            {
                BundleIndex = s.BundleIndex,
                IngredientIndex = s.IngredientIndex,
                ItemId = s.ItemId,
                Stack = Stack(full, discount),
                Quality = s.Quality,
                BundleName = s.BundleName,
                Deposited = s.Deposited,
                Due = s.Due,
                Paid = s.Paid,
                Stretch = s.Stretch,
                RouteTag = s.RouteTag,
            });
        }
        return shown;
    }

    /// <summary>The new values of the keys <paramref name="edits"/> change in <paramref name="board"/>
    /// (live BundleData or the stored engine board). Keys the board lacks are ignored.</summary>
    public static Dictionary<string, string> ApplyEdits(
        IReadOnlyDictionary<string, string> board, IEnumerable<StackEdit> edits)
    {
        var updates = new Dictionary<string, string>(StringComparer.Ordinal);
        if (board == null || edits == null) return updates;
        foreach (StackEdit edit in edits)
        {
            string? current = updates.TryGetValue(edit.Key, out string? pending) ? pending
                : board.TryGetValue(edit.Key, out string? value) ? value : null;
            if (current == null) continue;
            string? rewritten = WithStack(current, edit.IngredientIndex, edit.Stack);
            if (rewritten != null) updates[edit.Key] = rewritten;
        }
        return updates;
    }

    private static string? KeyFor(IReadOnlyDictionary<string, string> board, int bundleIndex)
    {
        foreach (string key in board.Keys)
        {
            int slash = key.IndexOf('/');
            if (slash >= 0 && int.TryParse(key.Substring(slash + 1), out int index) && index == bundleIndex)
                return key;
        }
        return null;
    }

    private static bool LineNames(string value, int ingredientIndex, string itemId)
    {
        string[]? tokens = IngredientTokens(value);
        int at = ingredientIndex * TokensPerIngredient;
        if (tokens == null || at >= tokens.Length) return false;
        return string.Equals(BundleParsing.NormalizeItemId(tokens[at]), BundleParsing.NormalizeItemId(itemId), StringComparison.Ordinal);
    }

    private static string[]? IngredientTokens(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        string[] fields = value.Split('/');
        if (fields.Length <= IngredientFieldIndex) return null;
        string[] tokens = fields[IngredientFieldIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= TokensPerIngredient && tokens.Length % TokensPerIngredient == 0 ? tokens : null;
    }
}
