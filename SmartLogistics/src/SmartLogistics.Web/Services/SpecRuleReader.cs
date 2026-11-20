using System.Text.RegularExpressions;

namespace SmartLogistics.Web.Services;

/// <summary>One row of the rules table in docs/spec.md ("| R11 | text | source |").</summary>
public sealed record SpecRule(string Id, int Number, string TextMarkdown, string Source);

/// <summary>Reads the rules table (R1..Rn) from docs/spec.md so the UI shows the spec text, not a copy of it.</summary>
public static partial class SpecRuleReader
{
    public static IReadOnlyList<SpecRule> Read(string specMarkdown) =>
        RuleRowPattern().Matches(specMarkdown)
            .Select(m => new SpecRule(
                "R" + m.Groups["number"].Value,
                int.Parse(m.Groups["number"].Value),
                m.Groups["text"].Value.Trim(),
                m.Groups["source"].Value.Trim()))
            .OrderBy(r => r.Number)
            .ToList();

    // A table row whose first cell is exactly "R<number>"; the rule text itself contains no "|".
    [GeneratedRegex(@"^\|\s*R(?<number>\d+)\s*\|(?<text>[^|]*)\|(?<source>[^|]*)\|\s*$", RegexOptions.Multiline)]
    private static partial Regex RuleRowPattern();
}
