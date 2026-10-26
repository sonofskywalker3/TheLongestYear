using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One slot of a board: bundle position in the board list, slot position in that bundle.</summary>
public readonly record struct BoardSlotRef(int Bundle, int Slot);

/// <summary>The board-wide "no item asked twice" rule as a matching problem (Nexus report,
/// TLY 1.0.0: Common Mushroom in both Spring Foraging and Fall Foraging).
///
/// The fills avoid what earlier bundles asked, but that can only ever be a preference: a fill whose
/// pool the earlier bundles have already eaten into gives the avoid set up and draws repeats, and
/// the authored bundles are composed before any of it, blind to each other (Mineral and
/// Rockhound's both draw from the minerals pool). A repeat is almost never forced, though. Spring
/// Foraging taking the Common Mushroom leaves Fall Foraging short, but Spring Foraging had a
/// Daffodil it could have asked for instead. So this treats the finished board as an assignment
/// of items to slots, every movable slot carrying the list of items it may ask for instead, and
/// resolves every repeat with the shortest chain of swaps that frees an item for it (an augmenting
/// path, Kuhn's algorithm with iterative deepening). A repeat survives only when no chain within
/// <see cref="MaxChainLength"/> swaps exists.
///
/// Pure on ids: the caller says which slots may move and what each may take.</summary>
public static class BoardNoRepeats
{
    /// <summary>Longest swap chain tried for one repeat. Three moves covers "take the other
    /// bundle's item and give it a free one" with a bundle in between; a board that needs more is
    /// a board whose pools really are exhausted.</summary>
    public const int MaxChainLength = 3;

    /// <summary>Every concrete id asked by more than one slot, in first-appearance order. Null
    /// entries (money, category slots) are not items.</summary>
    public static IReadOnlyList<string> Repeats(IReadOnlyList<IReadOnlyList<string?>> board)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var repeated = new List<string>();
        foreach (IReadOnlyList<string?> bundle in board)
            foreach (string? id in bundle)
                if (id != null && !seen.Add(id) && !repeated.Contains(id))
                    repeated.Add(id);
        return repeated;
    }

    /// <summary>The swaps that leave every item on <paramref name="board"/> asked once, as
    /// slot -> new id. <paramref name="options"/> gives a movable slot's alternatives in preference
    /// order (null or empty = the slot never moves). Fixed slots keep their items first; among
    /// movable slots the earlier one on the board keeps a contested item unless moving it is the
    /// only way out. Deterministic for the same board and options.
    /// <paramref name="stillRepeated"/> lists the ids no chain could separate.</summary>
    public static IReadOnlyDictionary<BoardSlotRef, string> Resolve(
        IReadOnlyList<IReadOnlyList<string?>> board,
        Func<BoardSlotRef, IReadOnlyList<string>?> options,
        out IReadOnlyList<string> stillRepeated)
    {
        var current = new Dictionary<BoardSlotRef, string>();
        var movable = new Dictionary<BoardSlotRef, IReadOnlyList<string>>();
        var order = new List<BoardSlotRef>();
        for (int b = 0; b < board.Count; b++)
            for (int s = 0; s < board[b].Count; s++)
            {
                string? id = board[b][s];
                if (id == null) continue;
                var slot = new BoardSlotRef(b, s);
                current[slot] = id;
                order.Add(slot);
            }

        IReadOnlyList<string> OptionsOf(BoardSlotRef slot)
        {
            if (movable.TryGetValue(slot, out IReadOnlyList<string>? known)) return known;
            IReadOnlyList<string> got = options(slot) ?? Array.Empty<string>();
            movable[slot] = got;
            return got;
        }

        // Ownership: fixed slots first (they cannot give an item up), then movable ones in board order.
        var owner = new Dictionary<string, BoardSlotRef>(StringComparer.Ordinal);
        var unplaced = new List<BoardSlotRef>();
        foreach (bool fixedPass in new[] { true, false })
            foreach (BoardSlotRef slot in order)
            {
                bool isFixed = OptionsOf(slot).Count == 0;
                if (isFixed != fixedPass) continue;
                if (owner.ContainsKey(current[slot])) unplaced.Add(slot);
                else owner[current[slot]] = slot;
            }

        if (unplaced.Count == 0)
        {
            stillRepeated = Array.Empty<string>();
            return new Dictionary<BoardSlotRef, string>();
        }

        var assigned = new Dictionary<BoardSlotRef, string>();

        // Moves `slot` onto an item no one holds, freeing whatever it held; a deeper call first
        // moves the holder of the item it wants. `held` is the item the slot gives up (null for a
        // slot that holds nothing yet: a repeat waiting for a place).
        bool TryPlace(BoardSlotRef slot, string? held, int depth, HashSet<BoardSlotRef> visited)
        {
            IReadOnlyList<string> wants = OptionsOf(slot);
            foreach (string id in wants)
            {
                if (owner.ContainsKey(id)) continue;
                Take(slot, held, id);
                return true;
            }
            if (depth <= 1) return false;
            foreach (string id in wants)
            {
                if (!owner.TryGetValue(id, out BoardSlotRef holder) || holder == slot) continue;
                if (OptionsOf(holder).Count == 0 || !visited.Add(holder)) continue;
                if (!TryPlace(holder, id, depth - 1, visited)) continue;
                Take(slot, held, id);
                return true;
            }
            return false;
        }

        void Take(BoardSlotRef slot, string? held, string id)
        {
            if (held != null && owner.TryGetValue(held, out BoardSlotRef h) && h == slot)
                owner.Remove(held);
            owner[id] = slot;
            current[slot] = id;
            assigned[slot] = id;
        }

        var left = new List<string>();
        foreach (BoardSlotRef slot in unplaced)
        {
            string wanted = current[slot];
            bool placed = false;
            for (int depth = 1; depth <= MaxChainLength && !placed; depth++)
            {
                // A repeat may also keep its item and move the slot that holds it instead.
                if (TryPlace(slot, held: null, depth, new HashSet<BoardSlotRef> { slot }))
                {
                    placed = true;
                    break;
                }
                if (owner.TryGetValue(wanted, out BoardSlotRef holder) && OptionsOf(holder).Count > 0
                    && TryPlace(holder, wanted, depth, new HashSet<BoardSlotRef> { slot, holder }))
                {
                    owner[wanted] = slot;
                    current[slot] = wanted;
                    assigned.Remove(slot);
                    placed = true;
                }
            }
            if (!placed && !left.Contains(wanted)) left.Add(wanted);
        }

        // A slot that ended on the item it started with did not move.
        var result = new Dictionary<BoardSlotRef, string>();
        foreach (KeyValuePair<BoardSlotRef, string> move in assigned)
            if (!string.Equals(board[move.Key.Bundle][move.Key.Slot], move.Value, StringComparison.Ordinal))
                result[move.Key] = move.Value;
        stillRepeated = left;
        return result;
    }
}
