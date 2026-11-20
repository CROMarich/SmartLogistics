using Microsoft.AspNetCore.Html;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

public sealed record RuleRowViewModel(string Id, IHtmlContent Text, string Source, bool CheckedByValidator);

public sealed record ViolationRowViewModel(string RuleId, string Truck, string Step, string Message);

/// <summary>Live demonstration: the validator run on a fixed example plan for the real scenario.</summary>
public sealed record ValidatorDemoViewModel(
    string Title,
    string Description,
    bool ValidatorAvailable,
    string Summary,
    string SummaryCssClass,
    IReadOnlyList<ViolationRowViewModel> Violations);

/// <summary>The "Pravila i provjere" page.</summary>
public sealed record RulesPageViewModel(
    PageHeaderViewModel Header,
    string? Error,
    IReadOnlyList<StatTileViewModel> Stats,
    ValidatorDemoViewModel? Demo,
    IReadOnlyList<RuleRowViewModel> Rules);

public static class RulesViewModelFactory
{
    private static readonly PageHeaderViewModel Header = new(
        "Projekt",
        "Pravila i provjere",
        "Pravila R1..Rn iz docs/spec.md i koja od njih automatski provjerava validator plana (Core/Validation/PlanValidator).");

    public static RulesPageViewModel Create(ProjectDocumentStore documents, ScenarioLoadResult scenario)
    {
        var specDocument = documents.Find("spec");
        var spec = specDocument is null ? null : documents.Read(specDocument);
        if (spec is null)
        {
            return new RulesPageViewModel(Header, "Datoteka docs/spec.md ne postoji.", [], null, []);
        }

        var checkedIds = PlanValidator.CheckedRuleIds.ToHashSet(StringComparer.Ordinal);
        var rules = SpecRuleReader.Read(spec.Markdown);
        var rows = rules
            .Select(r => new RuleRowViewModel(r.Id, new HtmlString(MarkdownRenderer.RenderInline(r.TextMarkdown)), r.Source, checkedIds.Contains(r.Id)))
            .ToList();

        var checkedCount = rows.Count(r => r.CheckedByValidator);
        var fromDecisions = rules.Count(r => r.Source.StartsWith("odluka", StringComparison.Ordinal));

        var stats = new List<StatTileViewModel>
        {
            new("Pravila u specifikaciji", Format.Integer(rows.Count), $"{fromDecisions} iz potvrđenih odluka", "list", "tone-accent"),
            new("Provjerava validator", Format.Integer(checkedCount),
                checkedCount == 0 ? "validator još nije implementiran" : $"{rows.Count - checkedCount} se ne provjerava na razini plana",
                "check", checkedCount == 0 ? "tone-warning" : "tone-success"),
        };

        var demo = scenario.Scenario is null ? null : CreateEmptyPlanDemo(scenario.Scenario);
        return new RulesPageViewModel(Header, scenario.Error, stats, demo, rows);
    }

    // An empty plan (no routes, no infeasible orders) for the real scenario: every order is
    // unaccounted for, so a working validator must report it (spec R23).
    private static ValidatorDemoViewModel CreateEmptyPlanDemo(Scenario scenario)
    {
        const string title = "Primjer: prazan plan";
        var description = $"Plan bez ruta i bez neizvedivih naloga za scenarij iz data/scenario.json ({scenario.Orders.Count} naloga). " +
                          "Ispravan validator mora prijaviti svaki nalog koji nije ni isplaniran ni označen kao neizvediv.";

        IReadOnlyList<PlanViolation> violations;
        try
        {
            violations = PlanValidator.Validate(new Plan([], [], 0), scenario, PlanningOptions.Default);
        }
        catch (NotImplementedException)
        {
            return new ValidatorDemoViewModel(title, description, ValidatorAvailable: false,
                "Validator još nije implementiran.", "callout is-warning", []);
        }

        var rows = violations
            .Select(v => new ViolationRowViewModel(
                v.RuleId,
                v.TruckId ?? "–",
                v.StepIndex is { } step ? (step + 1).ToString() : "–",
                v.Message))
            .ToList();

        var summary = rows.Count == 0
            ? "Validator nije pronašao nijedan prekršaj – za prazan plan to bi bila greška."
            : $"Validator je pronašao {rows.Count} {Format.Plural(rows.Count, "prekršaj", "prekršaja", "prekršaja")}.";

        return new ValidatorDemoViewModel(title, description, ValidatorAvailable: true, summary,
            rows.Count == 0 ? "callout is-danger" : "callout is-success", rows);
    }
}
