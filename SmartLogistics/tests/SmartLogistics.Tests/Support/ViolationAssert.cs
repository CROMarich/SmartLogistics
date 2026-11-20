using SmartLogistics.Core.Validation;

namespace SmartLogistics.Tests.Support;

/// <summary>Assertions on validator output that print every violation when they fail.</summary>
internal static class ViolationAssert
{
    public static string Describe(IEnumerable<PlanViolation> violations)
    {
        var lines = violations
            .Select(v => $"  {v.RuleId} truck={v.TruckId ?? "-"} step={v.StepIndex?.ToString() ?? "-"}: {v.Message}")
            .ToList();
        return lines.Count == 0 ? "  (no violations)" : string.Join(Environment.NewLine, lines);
    }

    public static void Empty(IReadOnlyList<PlanViolation> violations) =>
        Assert.True(violations.Count == 0, "Expected no violations, got:" + Environment.NewLine + Describe(violations));

    /// <summary>
    /// The required rule must be among the violations (one error may cause several); when given,
    /// one of those violations must also carry the expected truck and step index.
    /// </summary>
    public static void Contains(IReadOnlyList<PlanViolation> violations, string ruleId, string? truckId = null, int? stepIndex = null)
    {
        var expected = $"{ruleId}{(truckId is null ? "" : $" truck={truckId}")}{(stepIndex is null ? "" : $" step={stepIndex}")}";
        Assert.True(
            violations.Any(v => v.RuleId == ruleId
                && (truckId is null || v.TruckId == truckId)
                && (stepIndex is null || v.StepIndex == stepIndex)),
            $"Expected violation {expected}, got:" + Environment.NewLine + Describe(violations));
    }

    /// <summary>The rule must not be reported (optionally: not for this truck / not for another step).</summary>
    public static void DoesNotContain(IReadOnlyList<PlanViolation> violations, string ruleId, string? truckId = null)
    {
        Assert.False(
            violations.Any(v => v.RuleId == ruleId && (truckId is null || v.TruckId == truckId)),
            $"Did not expect {ruleId}{(truckId is null ? "" : $" for truck {truckId}")}, got:" + Environment.NewLine + Describe(violations));
    }
}
