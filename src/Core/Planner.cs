using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummage.Core;

/// <summary>
/// Works out a grab before anything moves: which chest stacks give how much, and which inventory
/// slot takes each part.
/// </summary>
/// <remarks>
/// Every move is sized to land whole in its target slot, and no slot is the target of two moves
/// in one grab. A move into another player's chest is answered later, and the slot it fills
/// stays reserved until then, so a second move into the same slot would be turned away.
/// </remarks>
internal static class Planner
{
    public static Plan Make(IEnumerable<Want> wants, IEnumerable<Source> sources, Bag bag, bool leaveOne)
    {
        var chests = sources.OrderBy(s => s.Distance).ThenBy(s => s.Id).ToList();
        var slots = new SlotMap(bag);
        var moves = new List<Move>();
        var outcomes = new List<Outcome>();

        foreach (var want in Merge(wants))
        {
            int carried = want.CountCarried ? bag.Stacks.Where(want.Matches).Sum(s => s.Count) : 0;
            var outcome = new Outcome(want.Name, want.Count, carried, Math.Max(0, want.Count - carried));
            outcomes.Add(outcome);

            int need = outcome.Need;
            foreach (var chest in chests)
            {
                if (need == 0 || outcome.OutOfRoom)
                {
                    break;
                }

                need -= TakeFrom(chest, want, need, leaveOne, slots, moves, outcome);
            }
        }

        return new Plan(moves, outcomes);
    }

    /// <summary>Wants for the same item add up; the first one's terms apply.</summary>
    private static List<Want> Merge(IEnumerable<Want> wants)
    {
        var merged = new List<Want>();
        foreach (var want in wants)
        {
            int at = merged.FindIndex(w => string.Equals(w.Name, want.Name, StringComparison.Ordinal));
            if (at < 0)
            {
                merged.Add(want);
            }
            else
            {
                var first = merged[at];
                merged[at] = new Want(first.Name, first.Count + want.Count, first.CountCarried, Math.Max(first.MinWorldLevel, want.MinWorldLevel));
            }
        }

        return merged;
    }

    private static int TakeFrom(Source chest, Want want, int need, bool leaveOne, SlotMap slots,
        List<Move> moves, Outcome outcome)
    {
        var stacks = chest.Stacks.Where(want.Matches).ToList();
        int held = stacks.Sum(s => s.Count);
        if (held == 0)
        {
            return 0;
        }

        if (chest.Reach == Reach.Closed)
        {
            outcome.Unreachable += held;
            return 0;
        }

        var allowed = stacks.Where(s => chest.MayTake(s.Prefab)).ToList();
        int allowedCount = allowed.Sum(s => s.Count);
        outcome.NotAllowed += held - allowedCount;

        // Each chest keeps one unit of each item it holds, counted by the item, not by stack.
        int budget = Stock.Budget(chest, stacks, leaveOne);
        int wanted = Math.Min(need, allowedCount);
        if (wanted > budget)
        {
            outcome.KeptByLeaveOne += wanted - budget;
        }

        int take = Math.Min(need, budget);
        int took = 0;
        foreach (var stack in allowed)
        {
            int left = stack.Count;
            while (left > 0 && took < take)
            {
                if (!slots.TryFind(stack, out int toX, out int toY, out int room))
                {
                    outcome.OutOfRoom = true;
                    return took;
                }

                int amount = Math.Min(Math.Min(left, take - took), room);
                slots.Place(toX, toY, stack, amount);
                moves.Add(new Move(chest.Id, chest.Reach, stack.Name, stack.X, stack.Y, toX, toY, amount));
                left -= amount;
                took += amount;
                outcome.Planned += amount;
            }

            if (took == take)
            {
                break;
            }
        }

        return took;
    }

    /// <summary>The inventory as the grab fills it, with the slots this grab already targets.</summary>
    private sealed class SlotMap
    {
        private readonly Bag _bag;
        private readonly Dictionary<(int X, int Y), (ItemStack Kind, int Count)> _held = new();
        private readonly HashSet<(int X, int Y)> _targeted = new();

        public SlotMap(Bag bag)
        {
            _bag = bag;
            foreach (var stack in bag.Stacks)
            {
                _held[(stack.X, stack.Y)] = (stack, stack.Count);
            }
        }

        /// <summary>A part stack of the same kind with room comes first, then an empty slot.</summary>
        public bool TryFind(ItemStack item, out int x, out int y, out int room)
        {
            foreach (var slot in Order())
            {
                if (!_targeted.Contains(slot) && _held.TryGetValue(slot, out var held)
                    && held.Kind.StacksWith(item) && held.Count < item.MaxStack)
                {
                    (x, y, room) = (slot.X, slot.Y, item.MaxStack - held.Count);
                    return true;
                }
            }

            foreach (var slot in Order())
            {
                if (!_targeted.Contains(slot) && !_held.ContainsKey(slot) && _bag.IsUsable(slot.X, slot.Y))
                {
                    (x, y, room) = (slot.X, slot.Y, item.MaxStack);
                    return true;
                }
            }

            (x, y, room) = (-1, -1, 0);
            return false;
        }

        public void Place(int x, int y, ItemStack item, int amount)
        {
            var slot = (x, y);
            _targeted.Add(slot);
            _held[slot] = _held.TryGetValue(slot, out var held) ? (held.Kind, held.Count + amount) : (item, amount);
        }

        private IEnumerable<(int X, int Y)> Order()
        {
            for (int y = 0; y < _bag.Height; y++)
            {
                for (int x = 0; x < _bag.Width; x++)
                {
                    yield return (x, y);
                }
            }
        }
    }
}
