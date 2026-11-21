using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels.Transport;

namespace SmartLogistics.Web.ViewModels;

// View models of the "Testni scenariji" page (/Scenariji). Built only by ScenarioLabViewModelFactory from ScenarioLab runs;
// every value is formatted here, the page only prints. The plan parts reuse the Transport view models (PlanMapper).

/// <summary>Computed verdict of a case: every expectation met or not.</summary>
/// <param name="Text">"Očekivano ✓" or "Neočekivano ✗".</param>
/// <param name="CssClass">"sl-badge sl-badge-ok" or "sl-badge sl-badge-danger".</param>
/// <param name="Icon">Icon name for <c>Icons.Get</c>: "check" or "alert".</param>
/// <param name="Mark">"✓" or "✗" (compact form, e.g. in the defect choice).</param>
public sealed record LabVerdictViewModel(bool IsExpected, string Text, string CssClass, string Icon, string Mark);

/// <summary>One expectation of the test definition with its result.</summary>
/// <param name="Code">Short label, e.g. "K1", "R11", "0 prekršaja".</param>
/// <param name="Mark">"✓" or "✗".</param>
/// <param name="MarkTitle">"ispunjeno" or "nije ispunjeno" (tooltip of the mark).</param>
/// <param name="CssClass">"sl-lab-check" (+ " sl-lab-check-failed").</param>
/// <param name="CodeCssClass">Badge classes of the code.</param>
public sealed record LabCheckViewModel(string Code, string Text, bool IsMet, string Mark, string MarkTitle, string CssClass, string CodeCssClass);

/// <summary>A badge for what actually happened: "K1 · N11", "R10 ×4", "0 prekršaja".</summary>
/// <param name="Title">Tooltip, e.g. "očekivano pravilo" or "posljedica kvara".</param>
public sealed record LabOutcomeViewModel(string Text, string CssClass, string Title);

/// <summary>One row of the "Stvarni rezultat" table.</summary>
public sealed record LabFactViewModel(string Label, string Value);

/// <summary>One case in the overview grid.</summary>
/// <param name="CategoryCssClass">Badge classes of the category ("Osnova", "K1" …, "Validator").</param>
/// <param name="ExpectedCodes">What the test definition expects, e.g. ["K1"] or the rules of all defects.</param>
/// <param name="VerdictDetail">"9 / 9 kvarova" for the broken-plan case, otherwise null.</param>
/// <param name="CssClass">"sl-lab-case" (+ " sl-lab-case-active" for the selected case).</param>
/// <param name="Route">Query values of the link (<c>slucaj</c>).</param>
public sealed record LabCaseCardViewModel(
    string Number,
    string Title,
    string Category,
    string CategoryCssClass,
    string Summary,
    IReadOnlyList<string> ExpectedCodes,
    LabVerdictViewModel Verdict,
    string? VerdictDetail,
    bool IsSelected,
    string CssClass,
    IReadOnlyDictionary<string, string> Route);

/// <summary>One defect of the broken-plan case (segmented choice).</summary>
/// <param name="ExpectedText">"R11, R13".</param>
/// <param name="Tooltip">"Zamijenjeni preuzimanje i isporuka istog naloga. Očekivano: R6."</param>
/// <param name="CssClass">"sl-segmented-item" (+ " sl-segmented-item-active").</param>
/// <param name="Route">Query values of the link (<c>slucaj=kvar&amp;kvar=…</c>).</param>
public sealed record LabDefectOptionViewModel(
    string Title,
    string Summary,
    string ExpectedText,
    string Tooltip,
    LabVerdictViewModel Verdict,
    bool IsSelected,
    string CssClass,
    IReadOnlyDictionary<string, string> Route);

/// <summary>The selected case: what changed, what is expected, what happened, and the plan itself.</summary>
/// <param name="Heading">"Slučaj 3 · K1 – stroj teži od svih kamiona".</param>
/// <param name="DefectTitle">Title of the selected defect (broken-plan case), otherwise null.</param>
/// <param name="VerdictCallout">Callout at the top of the page with the verdict and the unmet expectations.</param>
/// <param name="Changes">What was changed compared with the task data (Croatian, numbers computed from the data).</param>
/// <param name="Actual">Key results: km per truck and total, planned orders, infeasible orders, validator result.</param>
/// <param name="Outcomes">Categories and rules that actually occurred, as badges.</param>
/// <param name="Defects">Defects to choose from (broken-plan case only).</param>
/// <param name="Error">Why the case has no plan (e.g. the defect does not apply), or null.</param>
public sealed record LabCaseDetailViewModel(
    string Heading,
    string? DefectTitle,
    LabVerdictViewModel Verdict,
    PlanStatusViewModel VerdictCallout,
    IReadOnlyList<string> Changes,
    IReadOnlyList<LabCheckViewModel> Checks,
    IReadOnlyList<LabFactViewModel> Actual,
    IReadOnlyList<LabOutcomeViewModel> Outcomes,
    IReadOnlyList<LabDefectOptionViewModel> Defects,
    string? Error,
    PlanViewModel? Plan,
    PlanSummaryViewModel? Summary,
    PlanWarningsViewModel Warnings);

/// <summary>The "Testni scenariji" page.</summary>
/// <param name="Error">Why the task data could not be loaded, or null.</param>
/// <param name="SelectionWarning">Set when the query named an unknown case or defect (the default is shown), or null.</param>
/// <param name="SourceNote">Where the data comes from and that the current scenario is not touched.</param>
public sealed record ScenarioLabPageViewModel(
    PageHeaderViewModel Header,
    string? Error,
    string? SelectionWarning,
    string SourceNote,
    IReadOnlyList<StatTileViewModel> Stats,
    IReadOnlyList<LabCaseCardViewModel> Cases,
    LabCaseDetailViewModel? Selected);

/// <summary>Builds the "Testni scenariji" page from <see cref="ScenarioLab"/> runs (computed on demand, cached by the lab).</summary>
public static class ScenarioLabViewModelFactory
{
    public const string CaseParameter = "slucaj";
    public const string DefectParameter = "kvar";

    private const string ExpectedText = "Očekivano ✓";
    private const string UnexpectedText = "Neočekivano ✗";

    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Testni scenariji",
        "Rubni slučajevi nad izvornim podacima zadatka: kako na njih reagiraju egzaktni planer i neovisni validator. Svaki slučaj ima poveznicu koja se može podijeliti.");

    /// <param name="caseKey">Query value <c>slucaj</c>; missing → the first case.</param>
    /// <param name="defectKey">Query value <c>kvar</c>; used by the broken-plan case, missing → the first defect.</param>
    /// <param name="store">The app's current scenario, only to say that it is not used (never changed).</param>
    public static ScenarioLabPageViewModel Create(ScenarioLab lab, string? caseKey, string? defectKey, ScenarioSnapshot store)
    {
        ArgumentNullException.ThrowIfNull(lab);
        ArgumentNullException.ThrowIfNull(store);

        var storeNote = store.IsModified
            ? $" Trenutni scenarij aplikacije ({store.ChangeCount} {Format.Plural(store.ChangeCount, "izmjena", "izmjene", "izmjena")} naloga) ovdje se ne koristi i ostaje netaknut."
            : " Trenutni scenarij aplikacije (Ulazni podaci, Nalozi) ovdje se ne koristi i ostaje netaknut.";
        var sourceNote = $"Svaki slučaj gradi se iz izvornih podataka {lab.Source} uz jednu izmjenu; plan računa egzaktni planer ({PlannerCatalog.Exact.Name}), a provjerava ga neovisni validator.{storeNote}";

        if (lab.LoadError is not null)
        {
            return new ScenarioLabPageViewModel(Header, lab.LoadError, null, sourceNote, [], [], null);
        }

        var warnings = new List<string>();
        var selectedCase = lab.FindCase(caseKey);
        if (selectedCase is null)
        {
            if (!string.IsNullOrWhiteSpace(caseKey))
            {
                warnings.Add($"Nepoznat slučaj \"{caseKey.Trim()}\" – prikazan je slučaj 1.");
            }

            selectedCase = lab.Cases[0];
        }

        LabDefect? selectedDefect = null;
        if (selectedCase.Kind == LabCaseKind.BrokenPlan)
        {
            selectedDefect = lab.FindDefect(defectKey);
            if (selectedDefect is null)
            {
                if (!string.IsNullOrWhiteSpace(defectKey))
                {
                    warnings.Add($"Nepoznat kvar \"{defectKey.Trim()}\" – prikazan je kvar \"{lab.Defects[0].Title}\".");
                }

                selectedDefect = lab.Defects[0];
            }
        }

        var cards = lab.Cases.Select(c => Card(lab, c, c == selectedCase)).ToList();
        var allRuns = lab.RunAll();
        var selectedRun = lab.Run(selectedCase, selectedDefect);
        var detail = Detail(lab, selectedRun);

        return new ScenarioLabPageViewModel(
            Header,
            null,
            warnings.Count == 0 ? null : string.Join(" ", warnings),
            sourceNote,
            Stats(lab, allRuns, selectedRun),
            cards,
            detail);
    }

    /// <summary>"Očekivano ✓" (ok badge) or "Neočekivano ✗" (danger badge).</summary>
    public static LabVerdictViewModel Verdict(bool isExpected) => isExpected
        ? new LabVerdictViewModel(true, ExpectedText, "sl-badge sl-badge-ok", "check", "✓")
        : new LabVerdictViewModel(false, UnexpectedText, "sl-badge sl-badge-danger", "alert", "✗");

    // ---------- overview ----------

    private static LabCaseCardViewModel Card(ScenarioLab lab, LabCase labCase, bool isSelected)
    {
        string? detail = null;
        IReadOnlyList<string> expected = labCase.ExpectedCodes;
        bool isExpected;
        if (labCase.Kind == LabCaseKind.BrokenPlan)
        {
            var runs = lab.Defects.Select(d => lab.Run(labCase, d)).ToList();
            var met = runs.Count(r => r.IsExpected);
            isExpected = met == runs.Count;
            detail = $"{met} / {runs.Count} {Format.Plural(runs.Count, "kvar", "kvara", "kvarova")}";
            expected = lab.Defects.SelectMany(d => d.ExpectedRuleIds).Distinct(StringComparer.Ordinal).ToList();
        }
        else
        {
            isExpected = lab.Run(labCase).IsExpected;
        }

        return new LabCaseCardViewModel(
            labCase.Number,
            labCase.Title,
            labCase.Category,
            CategoryCss(labCase.Category),
            labCase.Summary,
            expected,
            Verdict(isExpected),
            detail,
            isSelected,
            isSelected ? "sl-lab-case sl-lab-case-active" : "sl-lab-case",
            new Dictionary<string, string> { [CaseParameter] = labCase.Key });
    }

    private static string CategoryCss(string category) => category switch
    {
        "K4" => "sl-badge sl-badge-danger",
        "K1" or "K2" or "K3" => "sl-badge sl-badge-warning",
        "Validator" => "sl-badge sl-badge-neutral",
        _ => "sl-badge sl-badge-accent",
    };

    private static IReadOnlyList<StatTileViewModel> Stats(ScenarioLab lab, IReadOnlyList<LabRun> runs, LabRun selected)
    {
        var met = runs.Count(r => r.IsExpected);
        var planner = lab.Cases.Count(c => c.Kind == LabCaseKind.Planner);
        var rules = runs.Where(r => r.Defect is not null).SelectMany(r => r.Defect!.ExpectedRuleIds).Distinct(StringComparer.Ordinal).Count();
        var stats = new List<StatTileViewModel>
        {
            new("Slučajevi", Format.Integer(lab.Cases.Count),
                $"{planner} nad planerom, 1 s {lab.Defects.Count} {Format.Plural(lab.Defects.Count, "kvarom", "kvara", "kvarova")} plana", "layers", "sl-stat-tile-accent"),
            new("Očekivano", $"{met} / {runs.Count}",
                met == runs.Count ? "svi slučajevi i kvarovi reagiraju kako je definirano" : $"{runs.Count - met} neočekivano – vidi oznake ✗", "check",
                met == runs.Count ? "sl-stat-tile-ok" : "sl-stat-tile-warning"),
            new("Pravila validatora", Format.Integer(rules), "provjerena namjernim kvarovima plana", "scale", "sl-stat-tile-neutral"),
        };

        if (selected.Plan is { } plan)
        {
            var planned = plan.Routes.Sum(r => r.OrderCount);
            var violations = selected.Violations.Count;
            stats.Add(new StatTileViewModel("Odabrani slučaj", Format.Km(plan.TotalKm),
                $"isplanirano {planned} / {selected.Scenario.Orders.Count} · {ViolationsText(violations)}", "route",
                selected.IsExpected ? "sl-stat-tile-ok" : "sl-stat-tile-warning"));
        }

        return stats;
    }

    // ---------- selected case ----------

    private static LabCaseDetailViewModel Detail(ScenarioLab lab, LabRun run)
    {
        var verdict = Verdict(run.IsExpected);
        var heading = $"Slučaj {run.Case.Number} · {run.Case.Title}";
        var checks = run.Checks.Select(c => new LabCheckViewModel(
            c.Code,
            c.Text,
            c.IsMet,
            c.IsMet ? "✓" : "✗",
            c.IsMet ? "ispunjeno" : "nije ispunjeno",
            c.IsMet ? "sl-lab-check" : "sl-lab-check sl-lab-check-failed",
            c.IsMet ? "sl-badge sl-badge-ok" : "sl-badge sl-badge-danger")).ToList();

        var defects = run.Case.Kind == LabCaseKind.BrokenPlan
            ? lab.Defects.Select(d =>
            {
                var selected = d == run.Defect;
                var rules = string.Join(", ", d.ExpectedRuleIds);
                return new LabDefectOptionViewModel(
                    d.Title,
                    d.Summary,
                    rules,
                    $"{d.Summary} Očekivano: {rules}.",
                    Verdict(lab.Run(run.Case, d).IsExpected),
                    selected,
                    selected ? "sl-segmented-item sl-segmented-item-active" : "sl-segmented-item",
                    new Dictionary<string, string> { [CaseParameter] = run.Case.Key, [DefectParameter] = d.Key });
            }).ToList()
            : [];

        var noWarnings = new PlanWarningsViewModel(null, [], []);
        if (run.Plan is null)
        {
            return new LabCaseDetailViewModel(heading, run.Defect?.Title, verdict,
                new PlanStatusViewModel($"{verdict.Text} – {run.Error}", "sl-callout sl-callout-danger", "alert"),
                run.Changes, checks, [], [], defects, run.Error ?? "Plan nije izračunat.", null, null, noWarnings);
        }

        var plan = PlanMapper.ToPlan(run.Plan, run.Scenario, run.Options, run.Violations,
            run.Defect is null ? run.Elapsed : null);
        var infeasible = plan.InfeasibleOrders.Count;
        var warnings = new PlanWarningsViewModel(
            infeasible == 0 ? null : $"{infeasible} {Format.Plural(infeasible, "nalog nije izvediv", "naloga nisu izvediva", "naloga nije izvedivo")}",
            plan.InfeasibleOrders,
            run.InputWarnings);

        return new LabCaseDetailViewModel(
            heading,
            run.Defect?.Title,
            verdict,
            VerdictCallout(run, verdict),
            run.Changes,
            checks,
            Actual(run, plan),
            Outcomes(run),
            defects,
            null,
            plan,
            Summary(run, plan),
            warnings);
    }

    private static PlanStatusViewModel VerdictCallout(LabRun run, LabVerdictViewModel verdict)
    {
        var name = run.Defect is null ? $"Slučaj {run.Case.Number}" : $"Slučaj {run.Case.Number} (kvar \"{run.Defect.Title}\")";
        var met = run.Checks.Count(c => c.IsMet);
        if (run.IsExpected)
        {
            return new PlanStatusViewModel(
                $"{name}: {verdict.Text} – ispunjeno je svih {met} {Format.Plural(met, "očekivanje", "očekivanja", "očekivanja")} testne definicije.",
                "sl-callout sl-callout-ok", "check");
        }

        var failed = string.Join("; ", run.Checks.Where(c => !c.IsMet).Select(c => c.Text));
        return new PlanStatusViewModel(
            $"{name}: {verdict.Text} – ispunjeno {met} od {run.Checks.Count}; nije ispunjeno: {failed}.",
            "sl-callout sl-callout-danger", "alert");
    }

    private static IReadOnlyList<LabFactViewModel> Actual(LabRun run, PlanViewModel plan)
    {
        var facts = new List<LabFactViewModel> { new("Ukupno", plan.TotalKm) };
        facts.AddRange(plan.Trucks.Select(t => new LabFactViewModel(
            t.Title,
            t.IsUnused ? $"{t.Km} · neiskorišten" : $"{t.Km} · {t.OrdersText} · {t.WorkdayText}")));
        facts.Add(new LabFactViewModel("Isplanirani nalozi", plan.OrdersText));
        facts.Add(new LabFactViewModel("Neizvedivi nalozi", plan.InfeasibleOrders.Count == 0
            ? "nema"
            : string.Join("; ", plan.InfeasibleOrders.Select(i => $"{i.OrderId} {i.ReasonCode} ({i.ReasonText})"))));
        var rules = RuleCounts(run.Violations);
        facts.Add(new LabFactViewModel("Validator", run.Violations.Count == 0
            ? "0 prekršaja"
            : $"{ViolationsText(run.Violations.Count)}: {string.Join(", ", rules.Select(r => r.Count > 1 ? $"{r.RuleId} ×{r.Count}" : r.RuleId))}"));
        facts.Add(new LabFactViewModel("Radni dan", plan.WorkdayModeText));
        return facts;
    }

    private static IReadOnlyList<LabOutcomeViewModel> Outcomes(LabRun run)
    {
        var outcomes = run.Plan!.InfeasibleOrders
            .Select(i =>
            {
                var code = PlanMapper.ReasonCode(i.Reason);
                return new LabOutcomeViewModel($"{code} · {i.Order.Id}",
                    i.Reason == InfeasibilityReason.InvalidOrderData ? "sl-badge sl-badge-danger" : "sl-badge sl-badge-warning",
                    $"nalog {i.Order.Id} neizvediv ({code})");
            })
            .ToList();

        var expected = run.Defect?.ExpectedRuleIds ?? [];
        outcomes.AddRange(RuleCounts(run.Violations).Select(r =>
        {
            var isExpected = expected.Contains(r.RuleId);
            var text = r.Count > 1 ? $"{r.RuleId} ×{r.Count}" : r.RuleId;
            return run.Defect is null || isExpected
                ? new LabOutcomeViewModel(text, "sl-badge sl-badge-danger", run.Defect is null ? "prekršaj validatora" : "očekivano pravilo")
                : new LabOutcomeViewModel(text, "sl-badge sl-badge-neutral", "posljedica kvara");
        }));

        if (run.Violations.Count == 0)
        {
            outcomes.Add(new LabOutcomeViewModel("0 prekršaja", "sl-badge sl-badge-ok", "validator ne nalazi prekršaj"));
        }

        return outcomes;
    }

    private static PlanSummaryViewModel Summary(LabRun run, PlanViewModel plan)
    {
        var violations = plan.Violations.Count;
        var facts = new List<string>();
        if (run.Defect is null)
        {
            facts.Add($"izračun: {plan.ElapsedText}");
        }

        facts.Add($"radni dan: {plan.WorkdayModeText}");
        facts.Add($"radnja {Format.Coordinate(run.Options.MinutesPerAction)} min · {Format.Coordinate(run.Options.SpeedKmh)} km/h");

        var fromPlanner = run.Defect is null;
        return new PlanSummaryViewModel(
            plan.Stats,
            plan.IsValid,
            plan.IsValid ? "Plan provjeren validatorom" : $"Validator je pronašao {ViolationsText(violations)}",
            fromPlanner ? $"Algoritam: {PlannerCatalog.Exact.Name}" : $"Plan: {PlannerCatalog.Exact.Name} + ručni kvar",
            fromPlanner,
            fromPlanner ? "Egzaktni optimum" : "Namjerno pokvaren plan",
            fromPlanner ? "sl-badge sl-badge-ok sl-badge-lg" : "sl-badge sl-badge-warning sl-badge-lg",
            fromPlanner ? "check" : "alert",
            plan.Violations,
            facts);
    }

    private static IReadOnlyList<(string RuleId, int Count)> RuleCounts(IReadOnlyList<PlanViolation> violations) =>
        violations.GroupBy(v => v.RuleId, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToList();

    private static string ViolationsText(int count) => $"{count} {Format.Plural(count, "prekršaj", "prekršaja", "prekršaja")}";
}
