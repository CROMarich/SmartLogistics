using System.Globalization;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels.Transport;

/// <summary>One radio button of the working-day mode.</summary>
/// <param name="Value">Query value: "ukupno" or "voznja".</param>
public sealed record WorkdayModeOptionViewModel(string Value, string Text, string Hint, bool IsChecked);

/// <summary>One radio button of the algorithm choice.</summary>
/// <param name="Value">Query value: "exact" or "ortools" (<see cref="PlannerDescriptor.Key"/>).</param>
/// <param name="Hint">"dokazani optimum (zadano)", "optimum nije zajamčen …" or "nije dostupan u ovoj verziji".</param>
/// <param name="Title">Longer description (tooltip).</param>
/// <param name="IsAvailable">False when no planner is registered for the key (the radio button is disabled).</param>
public sealed record AlgorithmOptionViewModel(string Value, string Text, string Hint, string Title, bool IsChecked, bool IsAvailable);

/// <summary>The GET form of the plan page (shared by URL, e.g. /Plan?algoritam=ortools&amp;nacin=voznja&amp;maxMinuta=300).</summary>
/// <param name="ModeValue">Query value of the selected (or default) working-day mode, for links that keep the form.</param>
/// <param name="MaxMinutes">Value of the number field (the attempted value when it was invalid).</param>
/// <param name="ModeError">Croatian validation message, or null.</param>
/// <param name="AlgorithmError">Croatian validation message, or null.</param>
/// <param name="MaxMinutesError">Croatian validation message, or null.</param>
public sealed record PlanFormViewModel(
    string ModeParameter,
    string AlgorithmParameter,
    string MaxMinutesParameter,
    IReadOnlyList<WorkdayModeOptionViewModel> Modes,
    IReadOnlyList<AlgorithmOptionViewModel> Algorithms,
    string ModeValue,
    string MaxMinutes,
    string MinMinutes,
    string MaxMinutesLimit,
    string MaxMinutesHint,
    string? ModeError,
    string? AlgorithmError,
    string? MaxMinutesError);

/// <summary>Model of _PlanSummary: key figures, algorithm and optimality guarantee, validator result and facts.</summary>
/// <param name="ValidatorText">"Plan provjeren validatorom" or "Validator je pronašao 2 prekršaja".</param>
/// <param name="AlgorithmText">"Algoritam: OR-Tools (heuristika)".</param>
/// <param name="IsExact">True for a proven optimum (spec R38).</param>
/// <param name="GuaranteeText">"Egzaktni optimum" or "Optimum nije zajamčen (heuristika)" (R38).</param>
/// <param name="GuaranteeCssClass">Badge classes: "sl-badge sl-badge-ok sl-badge-lg" or "sl-badge sl-badge-warning sl-badge-lg".</param>
/// <param name="GuaranteeIcon">Icon name for <c>Icons.Get</c>: "check" or "alert".</param>
/// <param name="Facts">"izračun: 12 ms", "planer: ExactDpPlanner", "radni dan: vožnja + radnje ≤ 09:00" ….</param>
public sealed record PlanSummaryViewModel(
    IReadOnlyList<StatTileViewModel> Stats,
    bool IsValid,
    string ValidatorText,
    string AlgorithmText,
    bool IsExact,
    string GuaranteeText,
    string GuaranteeCssClass,
    string GuaranteeIcon,
    IReadOnlyList<ViolationRowViewModel> Violations,
    IReadOnlyList<string> Facts);

/// <summary>One algorithm in the comparison table.</summary>
/// <param name="IsCurrent">True for the algorithm whose plan is shown on the page.</param>
/// <param name="IsComputed">False when the result is not in the cache yet; then <paramref name="ComputeRoute"/> links to it.</param>
/// <param name="TotalKm">"200,1 km", or "–".</param>
/// <param name="Difference">"+3,4 km (+1,7 %)" against the exact plan, "referenca" for the exact row, or "–".</param>
/// <param name="Orders">"10 / 10" planned orders, or "–".</param>
/// <param name="Elapsed">Computation time "12 ms" / "4,9 s", or "–".</param>
/// <param name="ResultText">"ispravan", "2 prekršaja", "greška" or "nije izračunat".</param>
/// <param name="ResultCssClass">Badge classes for <paramref name="ResultText"/>.</param>
/// <param name="ActionText">"Prikaži" (cached) or "Izračunaj" (starts the planner); unused for the current row.</param>
/// <param name="ActionIcon">Icon name: "arrow-right" or "play".</param>
/// <param name="ComputeRoute">Query values of the plan form with this algorithm (same mode and limit).</param>
public sealed record PlanComparisonRowViewModel(
    string Algorithm,
    string GuaranteeText,
    string GuaranteeCssClass,
    bool IsCurrent,
    bool IsComputed,
    string TotalKm,
    string Difference,
    string Orders,
    string Elapsed,
    string ResultText,
    string ResultCssClass,
    string ActionText,
    string ActionIcon,
    IReadOnlyDictionary<string, string> ComputeRoute);

/// <summary>Model of _PlanComparison: "Egzaktni vs. OR-Tools" for the same scenario version and options.</summary>
/// <param name="Note">Where the values come from (cache) and how the difference is computed.</param>
/// <param name="Warning">Set when the plans do not plan the same number of orders (km are then not comparable), or null.</param>
public sealed record PlanComparisonViewModel(
    string Title,
    string Note,
    string? Warning,
    IReadOnlyList<PlanComparisonRowViewModel> Rows);

/// <summary>Input of the comparison: an available algorithm and its cached result (null = not computed yet).</summary>
public sealed record PlanComparisonEntry(PlannerDescriptor Planner, PlanRunResult? Result);

/// <summary>Model of _Warnings: infeasible orders with reasons and warnings about the input data.</summary>
/// <param name="Title">"2 naloga nisu izvediva" (null when there are no infeasible orders).</param>
public sealed record PlanWarningsViewModel(
    string? Title,
    IReadOnlyList<InfeasibleOrderViewModel> InfeasibleOrders,
    IReadOnlyList<string> InputWarnings);

/// <summary>The plan page (Pages/Plan).</summary>
/// <param name="Error">Why no plan is shown (invalid form, load error, missing planner, planner error), or null.</param>
/// <param name="ErrorTitle">Bold start of the error callout: "Plan nije izračunat." or "Algoritam nije dostupan.".</param>
/// <param name="Plan">The mapped plan; null together with <paramref name="Error"/>.</param>
/// <param name="Comparison">Comparison of the available algorithms; null with fewer than two of them or without a plan.</param>
/// <param name="ScenarioNote">"Planira se trenutni scenarij: … 2 izmjene u odnosu na data/scenario.json."</param>
public sealed record PlanPageViewModel(
    PageHeaderViewModel Header,
    PlanFormViewModel Form,
    string? Error,
    string ErrorTitle,
    PlanViewModel? Plan,
    PlanSummaryViewModel? Summary,
    PlanComparisonViewModel? Comparison,
    PlanWarningsViewModel Warnings,
    string ScenarioNote,
    bool ScenarioModified);

/// <summary>Parameters and defaults of the plan form; parsing of the query values.</summary>
public static class PlanForm
{
    public const string ModeParameter = "nacin";
    public const string AlgorithmParameter = "algoritam";
    public const string MaxMinutesParameter = "maxMinuta";
    public const string TotalValue = "ukupno";
    public const string DrivingOnlyValue = "voznja";
    public const int MinMinutes = 1;
    public const int MaxMinutesLimit = 1440;
    public const int DefaultMaxMinutes = 540;

    public const string MaxMinutesRangeMessage = "Najdulje trajanje radnog dana mora biti cijeli broj minuta između 1 i 1440 (24 h).";
    public const string UnknownModeMessage = "Nepoznat način radnog dana: odaberite \"Ukupno vrijeme\" ili \"Samo vožnja\".";

    /// <summary>Nepoznat algoritam: odaberite "Egzaktni (DP)" ili "OR-Tools (heuristika)". (names from the catalog).</summary>
    public static string UnknownAlgorithmMessage { get; } =
        "Nepoznat algoritam: odaberite " + string.Join(" ili ", PlannerCatalog.All.Select(p => $"\"{p.Name}\"")) + ".";

    /// <summary>Missing or empty → Total (default, R22); "ukupno" → Total; "voznja" → DrivingOnly; anything else → null.</summary>
    public static WorkdayMode? ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or TotalValue => WorkdayMode.Total,
        DrivingOnlyValue => WorkdayMode.DrivingOnly,
        _ => null,
    };

    /// <summary>Missing or empty → the default (exact) algorithm; a catalog key (case-insensitive) → its entry; anything else → null.</summary>
    public static PlannerDescriptor? ParseAlgorithm(string? value) =>
        PlannerCatalog.Find(string.IsNullOrWhiteSpace(value) ? PlannerCatalog.DefaultKey : value);

    /// <summary>"ukupno" or "voznja".</summary>
    public static string ModeValue(WorkdayMode mode) => mode == WorkdayMode.DrivingOnly ? DrivingOnlyValue : TotalValue;

    /// <param name="algorithm">The selected algorithm (the default one when the query value was invalid).</param>
    /// <param name="isAvailable">Whether a planner is registered for an algorithm.</param>
    /// <param name="maxMinutesText">The value to show in the number field (attempted value when invalid).</param>
    public static PlanFormViewModel Create(
        WorkdayMode mode,
        PlannerDescriptor algorithm,
        Func<PlannerDescriptor, bool> isAvailable,
        string maxMinutesText,
        string? modeError,
        string? algorithmError,
        string? maxMinutesError)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentNullException.ThrowIfNull(isAvailable);

        var algorithms = PlannerCatalog.All.Select(p =>
        {
            var available = isAvailable(p);
            var hint = !available
                ? "nije dostupan u ovoj verziji"
                : p.IsExact ? "dokazani optimum (zadano)" : "optimum nije zajamčen";
            return new AlgorithmOptionViewModel(p.Key, p.Name, hint, p.Description, p.Key == algorithm.Key, available);
        }).ToList();

        return new(
            ModeParameter,
            AlgorithmParameter,
            MaxMinutesParameter,
            [
                new(TotalValue, "Ukupno vrijeme", "vožnja + radnje (zadano)", mode == WorkdayMode.Total),
                new(DrivingOnlyValue, "Samo vožnja", "radnje se ne broje u radni dan", mode == WorkdayMode.DrivingOnly),
            ],
            algorithms,
            ModeValue(mode),
            maxMinutesText,
            MinMinutes.ToString(CultureInfo.InvariantCulture),
            MaxMinutesLimit.ToString(CultureInfo.InvariantCulture),
            $"Zadano {DefaultMaxMinutes} min (09:00); dopušteno {MinMinutes}–{MaxMinutesLimit} min.",
            modeError,
            algorithmError,
            maxMinutesError);
    }
}

/// <summary>Builds the plan page from a planning run (or from an invalid form, without a run).</summary>
public static class PlanPageViewModelFactory
{
    private const string Dash = "–";
    private const string PlanNotComputed = "Plan nije izračunat.";

    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Plan prijevoza",
        "Dodjela naloga kamionima i redoslijed radnji za trenutni scenarij – egzaktnim planerom (najmanja ukupna kilometraža) ili heuristikom OR-Tools; rezultat provjerava validator.");

    /// <param name="run">The planning result; null when the form is invalid and nothing was planned.</param>
    /// <param name="snapshot">The store state, for the note about edits (taken from <paramref name="run"/> when given).</param>
    /// <param name="comparison">
    /// The available algorithms with their cached results (including the current one); a table is built only for two
    /// or more entries. Null or empty = no comparison.
    /// </param>
    public static PlanPageViewModel Create(
        PlanFormViewModel form,
        PlanRunResult? run,
        ScenarioSnapshot snapshot,
        IReadOnlyList<PlanComparisonEntry>? comparison = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(snapshot);

        var state = run?.Snapshot ?? snapshot;
        var note = state.IsModified
            ? $"Planira se trenutni scenarij: {state.ChangeCount} {Format.Plural(state.ChangeCount, "izmjena", "izmjene", "izmjena")} u odnosu na {state.Source}."
            : $"Planira se trenutni scenarij: podaci iz {state.Source} bez izmjena.";
        var noWarnings = new PlanWarningsViewModel(null, [], []);

        if (run is null)
        {
            return new PlanPageViewModel(Header, form, "Plan nije izračunat jer postavke u obrascu nisu ispravne.", PlanNotComputed,
                null, null, null, noWarnings, note, state.IsModified);
        }

        if (run.Plan is null || run.Error is not null)
        {
            return new PlanPageViewModel(Header, form, run.Error ?? PlanNotComputed,
                run.PlannerUnavailable ? "Algoritam nije dostupan." : PlanNotComputed,
                null, null, null, noWarnings, note, state.IsModified);
        }

        var plan = PlanMapper.ToPlan(run.Plan, run.Scenario, run.Options, run.Violations, run.Elapsed);

        var facts = new List<string>
        {
            $"izračun: {plan.ElapsedText}",
            $"planer: {run.PlannerName}",
            $"radni dan: {plan.WorkdayModeText}",
            $"radnja {Format.Coordinate(run.Options.MinutesPerAction)} min · {Format.Coordinate(run.Options.SpeedKmh)} km/h",
        };
        if (run.Statistics is { } s)
        {
            facts.Add($"pretraga: {Format.Integer(s.AssignmentsTried)} raspodjela, {Format.Integer(s.DpRuns)} DP izračuna{(s.ExtendedSearchUsed ? ", proširena pretraga (T23)" : string.Empty)}");
        }

        if (run.SolverStatistics is { } o)
        {
            var repaired = o.InsertedByRepair > 0
                ? $", popravak umetnuo {o.InsertedByRepair} {Format.Plural(o.InsertedByRepair, "nalog", "naloga", "naloga")}"
                : string.Empty;
            var limit = o.SolutionLimit > 0
                ? $"{Format.Integer(o.SolutionLimit)} rješenja ili {Format.Coordinate(o.TimeLimitSeconds)} s"
                : $"{Format.Coordinate(o.TimeLimitSeconds)} s";
            facts.Add($"OR-Tools: {o.SolverStatus}, ograničenje {limit}{repaired}");
        }

        var (guaranteeText, guaranteeCss, guaranteeIcon) = Guarantee(run.IsExact, large: true);
        var violations = plan.Violations.Count;
        var summary = new PlanSummaryViewModel(
            plan.Stats,
            plan.IsValid,
            plan.IsValid
                ? "Plan provjeren validatorom"
                : $"Validator je pronašao {violations} {Format.Plural(violations, "prekršaj", "prekršaja", "prekršaja")}",
            $"Algoritam: {run.Planner.Name}",
            run.IsExact,
            guaranteeText,
            guaranteeCss,
            guaranteeIcon,
            plan.Violations,
            facts);

        var infeasible = plan.InfeasibleOrders.Count;
        var warnings = new PlanWarningsViewModel(
            infeasible == 0
                ? null
                : $"{infeasible} {Format.Plural(infeasible, "nalog nije izvediv", "naloga nisu izvediva", "naloga nije izvedivo")}",
            plan.InfeasibleOrders,
            run.ScenarioWarnings);

        return new PlanPageViewModel(Header, form, null, PlanNotComputed, plan, summary, Compare(form, run, comparison), warnings, note, state.IsModified);
    }

    /// <summary>R38: the UI states whether the optimum is proven.</summary>
    private static (string Text, string CssClass, string Icon) Guarantee(bool isExact, bool large, bool compact = false)
    {
        var size = large ? " sl-badge-lg" : string.Empty;
        return isExact
            ? ("Egzaktni optimum", $"sl-badge sl-badge-ok{size}", "check")
            : (compact ? "Heuristika (bez jamstva)" : "Optimum nije zajamčen (heuristika)", $"sl-badge sl-badge-warning{size}", "alert");
    }

    private static PlanComparisonViewModel? Compare(PlanFormViewModel form, PlanRunResult current, IReadOnlyList<PlanComparisonEntry>? entries)
    {
        if (entries is null || entries.Count < 2)
        {
            return null;
        }

        // The page's own run is always "computed", even if the caller passed the entry without it.
        var results = entries
            .Select(e => (e.Planner, Result: e.Planner.Key == current.Planner.Key ? current : e.Result))
            .ToList();
        var reference = results.FirstOrDefault(r => (r.Result?.IsExact ?? false) && r.Result?.Plan is not null && r.Result.Error is null).Result?.Plan;
        var orderCount = current.Scenario.Orders?.Count ?? 0;

        var rows = results.Select(r =>
        {
            var (guaranteeText, guaranteeCss, _) = Guarantee(r.Result?.IsExact ?? r.Planner.IsExact, large: false, compact: true);
            var route = new Dictionary<string, string>
            {
                [PlanForm.AlgorithmParameter] = r.Planner.Key,
                [PlanForm.ModeParameter] = form.ModeValue,
                [PlanForm.MaxMinutesParameter] = form.MaxMinutes,
            };
            var isCurrent = r.Planner.Key == current.Planner.Key;
            var result = r.Result;

            if (result is null)
            {
                return new PlanComparisonRowViewModel(r.Planner.Name, guaranteeText, guaranteeCss, isCurrent, false,
                    Dash, Dash, Dash, Dash, "nije izračunat", "sl-badge sl-badge-neutral", "Izračunaj", "play", route);
            }

            if (result.Plan is null || result.Error is not null)
            {
                return new PlanComparisonRowViewModel(r.Planner.Name, guaranteeText, guaranteeCss, isCurrent, true,
                    Dash, Dash, Dash, Format.Elapsed(result.Elapsed), "greška", "sl-badge sl-badge-danger", "Prikaži", "arrow-right", route);
            }

            var km = result.Plan.TotalKm;
            var difference = result.IsExact
                ? "referenca"
                : reference is null ? Dash : Difference(km, reference.TotalKm);
            var planned = result.Plan.Routes.Sum(x => x.OrderCount);
            var violations = result.Violations.Count;

            return new PlanComparisonRowViewModel(
                r.Planner.Name, guaranteeText, guaranteeCss, isCurrent, true,
                Format.Km(km),
                difference,
                $"{planned} / {orderCount}",
                Format.Elapsed(result.Elapsed),
                violations == 0 ? "ispravan" : $"{violations} {Format.Plural(violations, "prekršaj", "prekršaja", "prekršaja")}",
                violations == 0 ? "sl-badge sl-badge-ok" : "sl-badge sl-badge-danger",
                "Prikaži",
                "arrow-right",
                route);
        }).ToList();

        var plannedCounts = results
            .Where(r => r.Result?.Plan is not null && r.Result.Error is null)
            .Select(r => r.Result!.Plan!.Routes.Sum(x => x.OrderCount))
            .Distinct()
            .Count();
        var warning = plannedCounts > 1
            ? "Algoritmi nisu isplanirali jednak broj naloga, pa ukupni km nisu izravno usporedivi."
            : null;

        return new PlanComparisonViewModel(
            "Usporedba algoritama",
            "Isti scenarij i iste postavke; vrijednosti dolaze iz međuspremnika (nema ponovnog izračuna). Razlika = km algoritma − km egzaktnog plana. Algoritam koji još nije izračunat pokreće se tek gumbom \"Izračunaj\".",
            warning,
            rows);
    }

    /// <summary>"+3,4 km (+1,7 %)", "0,0 km (0,0 %)"; percentage with one decimal, "–" without a positive reference.</summary>
    private static string Difference(double km, double referenceKm)
    {
        var diff = Math.Round(km - referenceKm, 1, MidpointRounding.AwayFromZero);
        if (!(referenceKm > 0))
        {
            return Format.SignedKm(diff);
        }

        var percent = Math.Round(diff / referenceKm * 100, 1, MidpointRounding.AwayFromZero);
        var sign = percent > 0 ? "+" : percent < 0 ? "−" : string.Empty;
        return $"{Format.SignedKm(diff)} ({sign}{Format.Number(Math.Abs(percent))} %)";
    }
}
