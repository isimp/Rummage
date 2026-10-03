using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rummage.Core;

/// <summary>The lines the player reads after a grab: what was taken, and why anything is missing.</summary>
internal static class Report
{
    /// <param name="label">What the grab was for.</param>
    /// <param name="plan">The grab as planned, for what was wanted and why items were missing.</param>
    /// <param name="taken">What was actually handed over, by item name.</param>
    /// <param name="skipped">Planned takes that could not be made when it came to it.</param>
    /// <param name="shown">The item's name as the player reads it.</param>
    public static IReadOnlyList<string> Lines(string label, Plan plan, IReadOnlyDictionary<string, int> taken, int skipped, Func<string, string> shown)
    {
        var lines = new List<string>();
        if (plan.Outcomes.All(o => o.Need == 0))
        {
            lines.Add($"{label}: you carry everything already");
            return lines;
        }

        var took = plan.Outcomes
            .Where(o => taken.TryGetValue(o.Name, out int n) && n > 0)
            .Select(o => Count(taken[o.Name], shown(o.Name)))
            .ToList();
        lines.Add(took.Count == 0 ? $"{label}: took nothing" : $"{label}: took {string.Join(", ", took)}");

        foreach (var outcome in plan.Outcomes.Where(o => o.Short > 0))
        {
            lines.Add($"{Count(outcome.Short, shown(outcome.Name))} short: {string.Join("; ", Reasons(outcome))}");
        }

        if (skipped > 0)
        {
            lines.Add(skipped == 1
                ? "1 take was not made because the chest changed meanwhile; press again"
                : $"{Count(skipped, "takes")} were not made because the chests changed meanwhile; press again");
        }

        return lines;
    }

    private static IEnumerable<string> Reasons(Outcome outcome)
    {
        int explained = 0;
        if (outcome.KeptByLeaveOne > 0)
        {
            explained += outcome.KeptByLeaveOne;
            yield return $"{Number(outcome.KeptByLeaveOne)} kept back, one in every chest";
        }

        if (outcome.NotAllowed > 0)
        {
            explained += outcome.NotAllowed;
            yield return $"{Number(outcome.NotAllowed)} kept in by chest rules";
        }

        if (outcome.Unreachable > 0)
        {
            explained += outcome.Unreachable;
            yield return $"{Number(outcome.Unreachable)} in a chest out of reach";
        }

        if (outcome.AwaitsAnswer)
        {
            yield return "the rest once the other chest has answered; press again";
        }
        else if (outcome.OutOfRoom)
        {
            yield return "no room left in your inventory";
        }
        else if (explained == 0)
        {
            yield return "not in chests nearby";
        }
        else if (explained < outcome.Short)
        {
            yield return "the rest is not in chests nearby";
        }
    }

    private static string Count(int count, string what) => $"{Number(count)} {what}";

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
