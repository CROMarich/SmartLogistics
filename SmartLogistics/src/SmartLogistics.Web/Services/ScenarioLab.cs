using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Services;

/// <summary>How a lab case gets its plan.</summary>
public enum LabCaseKind
{
    /// <summary>The exact planner plans a modified copy of the task data.</summary>
    Planner,

    /// <summary>The planner's valid plan for the task data gets one deliberate defect (<see cref="LabDefect"/>).</summary>
    BrokenPlan,
}

/// <summary>One test case of the "Testni scenariji" page: an edge case built from the original task data.</summary>
/// <param name="Key">Query value (<c>/Scenariji?slucaj=k1</c>).</param>
/// <param name="Number">Display number, "1" … "7".</param>
/// <param name="Title">Croatian title.</param>
/// <param name="Category">What the case tests: "Osnova", "K1" … "K4" or "Validator".</param>
/// <param name="Summary">One Croatian sentence for the overview.</param>
/// <param name="ExpectedCodes">Short labels of what the test definition expects, e.g. "K1", "0 prekršaja".</param>
public sealed record LabCase(
    string Key,
    string Number,
    string Title,
    string Category,
    string Summary,
    IReadOnlyList<string> ExpectedCodes,
    LabCaseKind Kind);

/// <summary>A deliberate defect applied to the planner's valid plan (case "Ručno pokvaren plan").</summary>
/// <param name="Key">Query value (<c>/Scenariji?slucaj=kvar&amp;kvar=r11</c>).</param>
/// <param name="ExpectedRuleIds">Rules of docs/spec.md the independent validator must report (expected ⊆ actual).</param>
public sealed record LabDefect(string Key, string Title, string Summary, IReadOnlyList<string> ExpectedRuleIds);

/// <summary>One expectation of a test definition and whether the run met it.</summary>
/// <param name="Code">Short label, e.g. "K1", "R11", "0 prekršaja".</param>
/// <param name="Text">Croatian explanation of the expectation.</param>
public sealed record LabCheck(string Code, string Text, bool IsMet);

/// <summary>The computed outcome of one lab case (and defect).</summary>
/// <param name="Scenario">The modified copy of the task data that was planned / validated.</param>
/// <param name="Plan">The plan (planner result, or the broken plan); null when <paramref name="Error"/> is set.</param>
/// <param name="Violations">Result of <see cref="PlanValidator.Validate"/> for <paramref name="Plan"/>.</param>
/// <param name="Changes">Croatian description of what was changed, with numbers computed from the data.</param>
/// <param name="Checks">The test definition's expectations with their result.</param>
/// <param name="InputWarnings">Warnings about the scenario from <see cref="Feasibility.Check"/>.</param>
/// <param name="Elapsed">Planner time (planner cases; zero for a broken plan).</param>
/// <param name="Error">Why the case could not be run (e.g. the defect does not apply to the plan), or null.</param>
public sealed record LabRun(
    LabCase Case,
    LabDefect? Defect,
    Scenario Scenario,
    PlanningOptions Options,
    Plan? Plan,
    IReadOnlyList<PlanViolation> Violations,
    IReadOnlyList<string> Changes,
    IReadOnlyList<LabCheck> Checks,
    IReadOnlyList<string> InputWarnings,
    TimeSpan Elapsed,
    string? Error)
{
    /// <summary>True when every expectation is met ("Očekivano ✓").</summary>
    public bool IsExpected => Error is null && Checks.Count > 0 && Checks.All(c => c.IsMet);
}

/// <summary>
/// Test cases for the "Testni scenariji" page. Every case starts from the original task data (data/scenario.json, loaded
/// once with the same path logic as <see cref="InMemoryScenarioStore"/>) and changes one thing; the current scenario of
/// <see cref="IScenarioStore"/> (the user's order edits) is never read or changed. Cases are planned with
/// <see cref="ExactDpPlanner"/> on demand and cached per case (and defect); then <see cref="PlanValidator"/> checks the plan.
/// <para>
/// What a case expects (category K1–K4, rule R…) is the test definition and is written here; everything else – the
/// modification (IDs, weights, coordinates), its arithmetic and the actual result – is computed from the data.
/// </para>
/// </summary>
public sealed class ScenarioLab
{
    public const string BaselineKey = "osnova";
    public const string DrivingOnlyKey = "voznja";
    public const string K1Key = "k1";
    public const string K2Key = "k2";
    public const string K3Key = "k3";
    public const string K4Key = "k4";
    public const string BrokenPlanKey = "kvar";

    /// <summary>Working-day limit of the K3 case (minutes).</summary>
    public const double ShortWorkdayMinutes = 300;

    /// <summary>How much heavier than the largest capacity the K1 machine is (kg).</summary>
    public const int K1OverweightKg = 1000;

    /// <summary>Error added to the plan total by the "wrong km" defect (km).</summary>
    public const double WrongKmOffset = 10;

    private const double KmTolerance = 0.05;

    private readonly ExactDpPlanner _planner;
    private readonly Scenario? _task;
    private readonly IReadOnlyList<CaseDefinition> _cases;
    private readonly IReadOnlyList<DefectDefinition> _defects;
    private readonly ConcurrentDictionary<string, Lazy<LabRun>> _cache = new(StringComparer.Ordinal);

    /// <summary>A lab over the given task data (e.g. loaded by a test).</summary>
    public ScenarioLab(Scenario taskScenario, string source = InMemoryScenarioStore.DefaultSource, ExactDpPlanner? planner = null)
        : this(Freeze(taskScenario ?? throw new ArgumentNullException(nameof(taskScenario))), source, null, planner)
    {
    }

    private ScenarioLab(Scenario? task, string source, string? loadError, ExactDpPlanner? planner)
    {
        _task = task;
        Source = source;
        LoadError = loadError;
        _planner = planner ?? new ExactDpPlanner();
        _cases = CreateCases();
        _defects = CreateDefects();
        Cases = _cases.Select(c => c.Info).ToList();
        Defects = _defects.Select(d => d.Info).ToList();
    }

    /// <summary>The lab for the app: data/scenario.json found like the store finds it; a load error is kept for the page.</summary>
    public static ScenarioLab FromEnvironment(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var (scenario, error) = InMemoryScenarioStore.LoadFile(InMemoryScenarioStore.TaskDataPath(environment), InMemoryScenarioStore.DefaultSource);
        return scenario is null
            ? new ScenarioLab(null, InMemoryScenarioStore.DefaultSource, error ?? "Podaci zadatka nisu učitani.", null)
            : new ScenarioLab(scenario, InMemoryScenarioStore.DefaultSource);
    }

    /// <summary>Where the task data comes from, e.g. "data/scenario.json".</summary>
    public string Source { get; }

    /// <summary>Why the task data could not be loaded (Croatian), or null.</summary>
    public string? LoadError { get; }

    /// <summary>The original task data (read-only copy), or null when it could not be loaded.</summary>
    public Scenario? TaskScenario => _task;

    public IReadOnlyList<LabCase> Cases { get; }

    public IReadOnlyList<LabDefect> Defects { get; }

    /// <summary>Case-insensitive lookup; null for an unknown key.</summary>
    public LabCase? FindCase(string? key) => Find(Cases, c => c.Key, key);

    /// <summary>Case-insensitive lookup; null for an unknown key.</summary>
    public LabDefect? FindDefect(string? key) => Find(Defects, d => d.Key, key);

    /// <summary>
    /// Runs (or returns the cached result of) a case. A <see cref="LabCaseKind.BrokenPlan"/> case needs a defect; when
    /// <paramref name="defect"/> is null the first one is used. The defect is ignored for planner cases.
    /// </summary>
    /// <exception cref="InvalidOperationException">The task data could not be loaded (<see cref="LoadError"/>).</exception>
    public LabRun Run(LabCase labCase, LabDefect? defect = null)
    {
        ArgumentNullException.ThrowIfNull(labCase);
        if (_task is null)
        {
            throw new InvalidOperationException($"Task data is not loaded: {LoadError}");
        }

        var definition = _cases.FirstOrDefault(c => c.Info == labCase)
            ?? throw new ArgumentException($"Unknown lab case '{labCase.Key}'.", nameof(labCase));
        DefectDefinition? defectDefinition = null;
        if (labCase.Kind == LabCaseKind.BrokenPlan)
        {
            defectDefinition = defect is null
                ? _defects[0]
                : _defects.FirstOrDefault(d => d.Info == defect) ?? throw new ArgumentException($"Unknown defect '{defect.Key}'.", nameof(defect));
        }

        var key = defectDefinition is null ? labCase.Key : $"{labCase.Key}/{defectDefinition.Info.Key}";
        return _cache.GetOrAdd(key, _ => new Lazy<LabRun>(
            () => Guarded(definition, defectDefinition, _task,
                () => defectDefinition is null ? RunPlanner(definition, _task) : RunDefect(definition, defectDefinition, _task)),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    // A page whose purpose is to show "Neočekivano ✗" must not fail: a planner or validator exception becomes an error
    // result (no checks, so the verdict is "unexpected"). Without this, Lazy would cache the exception until a restart.
    private static LabRun Guarded(CaseDefinition definition, DefectDefinition? defect, Scenario task, Func<LabRun> run)
    {
        try
        {
            return run();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new LabRun(definition.Info, defect?.Info, task, PlanningOptions.Default, null, [], [], [], [], TimeSpan.Zero,
                $"Slučaj nije izračunat: planer ili validator prijavio je grešku ({ex.GetType().Name}): {ex.Message}");
        }
    }

    /// <summary>Every case once; the broken-plan case with every defect (for the overview).</summary>
    public IReadOnlyList<LabRun> RunAll() =>
        Cases.SelectMany(c => c.Kind == LabCaseKind.BrokenPlan ? Defects.Select(d => Run(c, d)) : [Run(c)]).ToList();

    private LabRun Baseline() => Run(Cases[0]);

    // ---------- planner cases ----------

    private LabRun RunPlanner(CaseDefinition definition, Scenario task)
    {
        var setup = definition.Setup!(task, this);
        var stopwatch = Stopwatch.StartNew();
        var result = _planner.SolveWithStatistics(setup.Scenario, setup.Options);
        stopwatch.Stop();
        var violations = PlanValidator.Validate(result.Plan, setup.Scenario, setup.Options);
        var warnings = result.Feasibility?.Warnings ?? Feasibility.Check(setup.Scenario, setup.Options).Warnings;

        var outcome = new Outcome(setup.Scenario, setup.Options, result.Plan, violations,
            definition.Info.Key == BaselineKey ? null : Baseline(), this);
        var checks = definition.Expect(outcome).ToList();
        return new LabRun(definition.Info, null, setup.Scenario, setup.Options, result.Plan, violations, setup.Changes, checks,
            warnings, stopwatch.Elapsed, null);
    }

    /// <summary>Plans the scenario again (spec R39: the same input always gives the same plan).</summary>
    private bool PlansAgainIdentically(Scenario scenario, PlanningOptions options, Plan plan) =>
        Signature(_planner.Solve(scenario, options)) == Signature(plan);

    private static string Signature(Plan plan) =>
        string.Join("|", plan.Routes.Select(r => $"{r.Truck.Id}:{r.TotalKm:0.0}:" + string.Join(",", r.Stops.Select(s => $"{s.Action}-{s.OrderId}-{s.LocationId}"))))
        + "#" + string.Join(",", plan.InfeasibleOrders.Select(i => $"{i.Order.Id}-{i.Reason}"));

    // ---------- broken plan ----------

    private LabRun RunDefect(CaseDefinition definition, DefectDefinition defect, Scenario task)
    {
        var baseline = Baseline();
        var options = baseline.Options;
        var baselineCheck = new LabCheck("0 prekršaja",
            "plan planera prije kvara prolazi validator – svaki prijavljeni prekršaj dolazi od kvara", baseline.Violations.Count == 0);
        if (baseline.Plan is null)
        {
            return new LabRun(definition.Info, defect.Info, task, options, null, [], [], [baselineCheck], [], TimeSpan.Zero,
                "Osnovni plan nije izračunat.");
        }

        var lookup = new Lookup(task);
        var applied = defect.Apply(baseline.Plan, lookup, options);
        if (applied is null)
        {
            return new LabRun(definition.Info, defect.Info, task, options, null, [], [], [baselineCheck], baseline.InputWarnings,
                TimeSpan.Zero, $"Kvar \"{defect.Info.Title}\" ne može se primijeniti na plan planera (nema odgovarajućeg naloga ili rute).");
        }

        var violations = PlanValidator.Validate(applied.Plan, task, options);
        var reported = violations.Select(v => v.RuleId).ToHashSet(StringComparer.Ordinal);
        var checks = new List<LabCheck> { baselineCheck };
        checks.AddRange(defect.Info.ExpectedRuleIds.Select(rule =>
            new LabCheck(rule, $"validator prijavljuje {rule}", reported.Contains(rule))));
        return new LabRun(definition.Info, defect.Info, task, options, applied.Plan, violations, applied.Changes, checks,
            baseline.InputWarnings, TimeSpan.Zero, null);
    }

    // ---------- case definitions ----------

    private sealed record Setup(Scenario Scenario, PlanningOptions Options, IReadOnlyList<string> Changes);

    /// <param name="Setup">Builds the modified scenario; null for the broken-plan case.</param>
    private sealed record CaseDefinition(LabCase Info, Func<Scenario, ScenarioLab, Setup>? Setup, Func<Outcome, IEnumerable<LabCheck>> Expect);

    /// <summary>What a planner case produced, with the baseline run for comparisons (null for the baseline itself).</summary>
    private sealed record Outcome(
        Scenario Scenario, PlanningOptions Options, Plan Plan, IReadOnlyList<PlanViolation> Violations, LabRun? Baseline, ScenarioLab Lab)
    {
        public int PlannedCount => Plan.Routes.Sum(r => r.OrderCount);

        public int OrderCount => Scenario.Orders.Count;
    }

    private static IReadOnlyList<CaseDefinition> CreateCases() =>
    [
        new(new LabCase(BaselineKey, "1", "Podaci zadatka (Total, 540 min)", "Osnova",
                "Izvorni podaci bez izmjena; radni dan vožnja + radnje ≤ 540 min.",
                ["svi nalozi", "0 prekršaja", "R39"], LabCaseKind.Planner),
            (task, _) => new Setup(task, PlanningOptions.Default,
            [
                $"bez izmjena: {Counts(task)} iz {InMemoryScenarioStore.DefaultSource}",
                $"radni dan {nameof(WorkdayMode.Total)}: vožnja + radnje ≤ {Format.Coordinate(PlanningOptions.Default.MaxMinutes)} min po kamionu",
            ]),
            o =>
            [
                AllPlanned(o),
                NoViolations(o),
                new("R39", "ponovljeni izračun daje identičan plan (deterministički planer)",
                    o.Lab.PlansAgainIdentically(o.Scenario, o.Options, o.Plan)),
            ]),

        new(new LabCase(DrivingOnlyKey, "2", "Isti podaci, DrivingOnly", "Osnova",
                "U 540 min broji se samo vožnja; radnje ne troše radni dan.",
                ["svi nalozi", "0 prekršaja", "km ≤ Total"], LabCaseKind.Planner),
            (task, _) =>
            {
                var options = PlanningOptions.Default with { WorkdayMode = WorkdayMode.DrivingOnly };
                return new Setup(task, options,
                [
                    $"način radnog dana {nameof(WorkdayMode.DrivingOnly)}: u {Format.Coordinate(options.MaxMinutes)} min broji se samo vožnja",
                    $"radnje ({Format.Coordinate(options.MinutesPerAction)} min) i dalje se izvode, ali ne ulaze u ograničenje (R21)",
                ]);
            },
            o =>
            [
                AllPlanned(o),
                NoViolations(o),
                new("km ≤ Total",
                    $"ukupni km ({Format.Km(o.Plan.TotalKm)}) nije veći od osnovnog slučaja ({Format.Km(o.Baseline!.Plan!.TotalKm)}) – DrivingOnly je blaže ograničenje",
                    o.Plan.TotalKm <= o.Baseline.Plan.TotalKm + KmTolerance),
            ]),

        new(new LabCase(K1Key, "3", "K1 – stroj teži od svih kamiona", "K1",
                "Dodan nalog sa strojem težim od nosivosti svih kamiona.",
                ["K1"], LabCaseKind.Planner),
            (task, _) => SetupK1(task),
            o => NewOrdersInfeasible(o, InfeasibilityReason.ExceedsAllCapacities)),

        new(new LabCase(K2Key, "4", "K2 – nalog ni sam ne stane u radni dan", "K2",
                "Dodana udaljena lokacija: samostalna ruta baza → preuzimanje → isporuka → baza traje dulje od radnog dana.",
                ["K2"], LabCaseKind.Planner),
            (task, _) => SetupK2(task),
            o => NewOrdersInfeasible(o, InfeasibilityReason.ExceedsWorkday)),

        new(new LabCase(K3Key, "5", $"K3 – prekratak radni dan ({Format.Coordinate(ShortWorkdayMinutes)} min)", "K3",
                "Svaki nalog sam stane u dan, ali svi zajedno ne; planer izostavlja najmanji broj naloga.",
                ["K3", "0 prekršaja"], LabCaseKind.Planner),
            SetupK3,
            o =>
            [
                new("K3", "barem jedan nalog je neizvediv s kategorijom K3 (ne stane uz ostale naloge)",
                    o.Plan.InfeasibleOrders.Any(i => i.Reason == InfeasibilityReason.DoesNotFitWithOtherOrders)),
                new("samo K3", "nijedan nalog nije K1, K2 ni K4 – svaki sam stane u skraćeni dan",
                    o.Plan.InfeasibleOrders.All(i => i.Reason == InfeasibilityReason.DoesNotFitWithOtherOrders)),
                new("R23", "svaki nalog je isplaniran ili naveden kao neizvediv",
                    o.PlannedCount + o.Plan.InfeasibleOrders.Count == o.OrderCount),
                NoViolations(o, "validator (R20, R32, R33) ne nalazi prekršaj: izostavljeni nalog ne stane ni na kraj jedne rute"),
            ]),

        new(new LabCase(K4Key, "6", "K4 – neispravni nalozi", "K4",
                "Dodan nalog s preuzimanjem jednakim isporuci i nalog s nepostojećom lokacijom.",
                ["K4"], LabCaseKind.Planner),
            (task, _) => SetupK4(task),
            o => NewOrdersInfeasible(o, InfeasibilityReason.InvalidOrderData)),

        new(new LabCase(BrokenPlanKey, "7", "Ručno pokvaren plan", "Validator",
                "Ispravan plan planera s jednim namjernim kvarom; neovisni validator mora prijaviti očekivano pravilo (ostali prekršaji prikazuju se kao posljedica kvara).",
                ["R…"], LabCaseKind.BrokenPlan),
            null,
            _ => []),
    ];

    private static string Counts(Scenario s) =>
        $"{s.Trucks.Count} {Format.Plural(s.Trucks.Count, "kamion", "kamiona", "kamiona")}, "
        + $"{s.Machines.Count} {Format.Plural(s.Machines.Count, "stroj", "stroja", "strojeva")}, "
        + $"{s.Locations.Count} {Format.Plural(s.Locations.Count, "lokacija", "lokacije", "lokacija")}, "
        + $"{s.Orders.Count} {Format.Plural(s.Orders.Count, "nalog", "naloga", "naloga")}";

    private static LabCheck AllPlanned(Outcome o) =>
        new("svi nalozi", $"svi nalozi su isplanirani ({o.PlannedCount} / {o.OrderCount}), nijedan nije neizvediv",
            o.PlannedCount == o.OrderCount && o.Plan.InfeasibleOrders.Count == 0);

    private static LabCheck NoViolations(Outcome o, string? text = null) =>
        new("0 prekršaja", text ?? "neovisni validator ne nalazi nijedan prekršaj", o.Violations.Count == 0);

    /// <summary>
    /// Orders added by a case (IDs not in the task data) must be infeasible with the given category; every task order is
    /// planned as in the baseline (same total km) and the validator agrees with the planner.
    /// </summary>
    private static IEnumerable<LabCheck> NewOrdersInfeasible(Outcome o, InfeasibilityReason reason)
    {
        var code = ReasonCode(reason);
        var baseline = o.Baseline!;
        var taskIds = baseline.Scenario.Orders.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var added = o.Scenario.Orders.Where(x => !taskIds.Contains(x.Id)).ToList();
        foreach (var order in added)
        {
            var entry = o.Plan.InfeasibleOrders.FirstOrDefault(i => i.Order.Id == order.Id);
            yield return new LabCheck(code,
                $"nalog {order.Id} je neizvediv s kategorijom {code}"
                + (entry is null ? " (isplaniran je)" : entry.Reason == reason ? string.Empty : $" (dobio je {ReasonCode(entry.Reason)})"),
                entry?.Reason == reason);
        }

        var taskCount = baseline.Scenario.Orders.Count;
        yield return new LabCheck($"{taskCount} / {taskCount}", $"svih {taskCount} naloga zadatka je isplanirano",
            o.PlannedCount == taskCount && o.Plan.InfeasibleOrders.Count == added.Count);
        yield return new LabCheck("= osnova",
            $"ukupni km ({Format.Km(o.Plan.TotalKm)}) jednak je osnovnom slučaju ({Format.Km(baseline.Plan!.TotalKm)}) – neizvedivi nalog ne mijenja plan ostalih",
            Math.Abs(o.Plan.TotalKm - baseline.Plan.TotalKm) <= KmTolerance);
        yield return NoViolations(o, $"validator ne nalazi prekršaj – i on nalog vidi kao {code} (R33)");
    }

    private static string ReasonCode(InfeasibilityReason reason) => ViewModels.Transport.PlanMapper.ReasonCode(reason);

    private static Location BaseOf(Scenario s) => s.Locations.First(l => l.Id == s.BaseLocationId);

    private static IReadOnlyList<Location> NonBaseLocations(Scenario s) => s.Locations.Where(l => l.Id != s.BaseLocationId).ToList();

    private static string NextLocationId(Scenario s) => ScenarioIds.NextId("L", s.Locations.Select(l => l.Id));

    /// <summary>A light machine every truck can carry: as heavy as the lightest machine of the task.</summary>
    private static int LightWeight(Scenario s) => s.Machines.Min(m => m.WeightKg);

    private static Setup SetupK1(Scenario task)
    {
        var heaviestTruck = task.Trucks.MaxBy(t => t.CapacityKg)!;
        var machine = new Machine(ScenarioIds.NextMachineId(task), heaviestTruck.CapacityKg + K1OverweightKg);
        var locations = NonBaseLocations(task);
        var order = new TransportOrder(ScenarioIds.NextOrderId(task), machine.Id, locations[0].Id, locations[1].Id);
        var capacities = string.Join(", ", task.Trucks.Select(t => $"{t.Id} {Format.Kg(t.CapacityKg)}"));
        return new Setup(
            task with { Machines = [.. task.Machines, machine], Orders = [.. task.Orders, order] },
            PlanningOptions.Default,
            [
                $"dodan stroj {machine.Id}, {Format.Kg(machine.WeightKg)} – za {Format.Kg(K1OverweightKg)} teži od najveće nosivosti ({heaviestTruck.Id} {Format.Kg(heaviestTruck.CapacityKg)})",
                $"nosivosti kamiona: {capacities}",
                $"dodan nalog {order.Id}: {machine.Id}, {order.PickupLocationId} → {order.DeliveryLocationId}",
            ]);
    }

    /// <summary>
    /// A new location east of the base, as far as halfway between the Total limit and the DrivingOnly limit of a
    /// stand-alone route base → base (pickup) → new location (delivery) → base: K2 in Total, feasible alone in DrivingOnly.
    /// </summary>
    private static Setup SetupK2(Scenario task)
    {
        var options = PlanningOptions.Default;
        var baseLocation = BaseOf(task);
        var minutesPerKm = options.DrivingMinutes(1);
        var actions = 2 * options.MinutesPerAction;
        var totalLimitKm = (options.MaxMinutes - actions) / (2 * minutesPerKm);
        var drivingLimitKm = options.MaxMinutes / (2 * minutesPerKm);
        var distance = Math.Round((totalLimitKm + drivingLimitKm) / 2, MidpointRounding.AwayFromZero);
        if (distance <= totalLimitKm)
        {
            distance = Math.Floor(totalLimitKm) + 1;
        }

        var far = new Location(NextLocationId(task), baseLocation.X + distance, baseLocation.Y);
        var machine = new Machine(ScenarioIds.NextMachineId(task), LightWeight(task));
        var order = new TransportOrder(ScenarioIds.NextOrderId(task), machine.Id, baseLocation.Id, far.Id);

        // The same arithmetic as the planner's feasibility check: legs rounded first (R14), then summed.
        var first = Distance.Leg(baseLocation, baseLocation);
        var outbound = Distance.Leg(baseLocation, far);
        var back = Distance.Leg(far, baseLocation);
        var km = first + outbound + back;
        var driving = options.DrivingMinutes(km);
        var total = driving + actions;
        var limit = Format.Coordinate(options.MaxMinutes);
        return new Setup(
            task with { Machines = [.. task.Machines, machine], Locations = [.. task.Locations, far], Orders = [.. task.Orders, order] },
            options,
            [
                $"dodana lokacija {far.Id} ({Format.Coordinate(far.X)}, {Format.Coordinate(far.Y)}), {Format.Km(outbound)} od baze {baseLocation.Id}",
                $"dodan stroj {machine.Id} ({Format.Kg(machine.WeightKg)}, nosi ga svaki kamion) i nalog {order.Id}: {machine.Id}, {baseLocation.Id} → {far.Id}",
                $"samostalna ruta {baseLocation.Id} → {baseLocation.Id} → {far.Id} → {baseLocation.Id}: {Format.Number(first)} + {Format.Number(outbound)} + {Format.Number(back)} = {Format.Km(km)} = {Format.Coordinate(driving)} min vožnje",
                $"{nameof(WorkdayMode.Total)}: {Format.Coordinate(driving)} + 2 radnje × {Format.Coordinate(options.MinutesPerAction)} min = {Format.Coordinate(total)} min > {limit} min → K2 na svakom kamionu",
                $"{nameof(WorkdayMode.DrivingOnly)} bi brojio samo {Format.Coordinate(driving)} min ≤ {limit} min – ondje nalog sam stane u dan (RS17)",
            ]);
    }

    private static Setup SetupK3(Scenario task, ScenarioLab lab)
    {
        var options = PlanningOptions.Default with { MaxMinutes = ShortWorkdayMinutes };
        var baseline = lab.Baseline().Plan!;
        var actions = task.Orders.Count * 2;
        var needed = baseline.TotalKm * options.DrivingMinutes(1) + actions * options.MinutesPerAction;
        var available = task.Trucks.Count * options.MaxMinutes;
        return new Setup(task, options,
        [
            $"radni dan skraćen s {Format.Coordinate(PlanningOptions.Default.MaxMinutes)} na {Format.Coordinate(options.MaxMinutes)} min ({nameof(WorkdayMode.Total)}), podaci bez izmjena",
            $"svi nalozi zajedno traže najmanje {Format.Coordinate(needed)} min: optimum osnovnog slučaja {Format.Km(baseline.TotalKm)} vožnje + {actions} radnji × {Format.Coordinate(options.MinutesPerAction)} min",
            $"kamioni imaju ukupno {task.Trucks.Count} × {Format.Coordinate(options.MaxMinutes)} = {Format.Coordinate(available)} min"
                + (needed > available ? " – svi nalozi ne mogu stati, neki moraju biti K3" : " – ova granica sama ne isključuje nijedan nalog"),
        ]);
    }

    private static Setup SetupK4(Scenario task)
    {
        var locations = NonBaseLocations(task);
        var firstMachine = new Machine(ScenarioIds.NextMachineId(task), LightWeight(task));
        var secondMachine = new Machine(ScenarioIds.NextId(ScenarioIds.MachinePrefix, [.. task.Machines.Select(m => m.Id), firstMachine.Id]), LightWeight(task));
        var firstOrder = new TransportOrder(ScenarioIds.NextOrderId(task), firstMachine.Id, locations[0].Id, locations[0].Id);
        var missingLocation = NextLocationId(task);
        var secondOrder = new TransportOrder(
            ScenarioIds.NextId(ScenarioIds.OrderPrefix, [.. task.Orders.Select(o => o.Id), firstOrder.Id]),
            secondMachine.Id, locations[0].Id, missingLocation);
        return new Setup(
            task with { Machines = [.. task.Machines, firstMachine, secondMachine], Orders = [.. task.Orders, firstOrder, secondOrder] },
            PlanningOptions.Default,
            [
                $"dodani strojevi {firstMachine.Id} i {secondMachine.Id} ({Format.Kg(firstMachine.WeightKg)}) – ispravni, nosi ih svaki kamion",
                $"dodan nalog {firstOrder.Id}: {firstMachine.Id}, {firstOrder.PickupLocationId} → {firstOrder.DeliveryLocationId} – preuzimanje jednako isporuci",
                $"dodan nalog {secondOrder.Id}: {secondMachine.Id}, {secondOrder.PickupLocationId} → {secondOrder.DeliveryLocationId} – lokacija {missingLocation} ne postoji",
            ]);
    }

    // ---------- defect definitions ----------

    private sealed record DefectResult(Plan Plan, IReadOnlyList<string> Changes);

    private sealed record DefectDefinition(LabDefect Info, Func<Plan, Lookup, PlanningOptions, DefectResult?> Apply);

    private static IReadOnlyList<DefectDefinition> CreateDefects() =>
    [
        new(new LabDefect("preopterecenje", "Preopterećenje",
                "Nalog sa strojem težim od nosivosti premješten na manji kamion.", ["R11", "R13"]), Overload),
        new(new LabDefect("strojevi", "Previše strojeva",
                "Preuzimanje prije isporuke dok je kamion pun – jedan stroj previše.", ["R12"]), TooManyMachines),
        new(new LabDefect("redoslijed", "Isporuka prije preuzimanja",
                "Zamijenjeni preuzimanje i isporuka istog naloga.", ["R6"]), DeliveryBeforePickup),
        new(new LabDefect("kamioni", "Različiti kamioni",
                "Isporuku naloga obavlja drugi kamion.", ["R5"]), DifferentTrucks),
        new(new LabDefect("povratak", "Bez povratka u bazu",
                "Km i vrijeme rute završavaju na zadnjoj radnji.", ["R2"]), MissingReturn),
        new(new LabDefect("km", "Pogrešan ukupni km",
                $"Ukupni km plana uvećan za {Format.Km(WrongKmOffset)}.", ["R16"]), WrongTotalKm),
        new(new LabDefect("izostavljen", "Izostavljen izvediv nalog",
                "Nalog koji stane u plan naveden kao neizvediv (K3).", ["R32"]), (plan, lookup, options) => LeaveOut(plan, lookup, options, listAsInfeasible: true)),
        new(new LabDefect("nestao", "Nalog nestao iz plana",
                "Nalog nije ni u planu ni na popisu neizvedivih.", ["R23"]), (plan, lookup, options) => LeaveOut(plan, lookup, options, listAsInfeasible: false)),
        new(new LabDefect("radni-dan", "Prekoračen radni dan",
                "Sve radnje nadovezane na jedan kamion.", ["R20"]), WorkdayExceeded),
    ];

    private const string RecomputedNote = "km, minute i teret ruta preračunati su za izmijenjeni redoslijed (pribrajanjem, kao da ih je izračunao planer)";

    /// <summary>R11/R13: the lightest order whose machine is too heavy for the smallest truck moves to the end of that truck's route.</summary>
    private static DefectResult? Overload(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var routes = Editable(plan);
        var target = routes.MinBy(r => r.Truck.CapacityKg)!;
        var candidate = routes
            .Where(r => r != target)
            .SelectMany(r => r.Stops.Where(s => s.Action == StopAction.Pickup).Select(s => (Route: r, s.OrderId, Weight: lookup.WeightOf(s.OrderId))))
            .Where(x => x.Weight > target.Truck.CapacityKg)
            .OrderBy(x => x.Weight)
            .FirstOrDefault();
        if (candidate.Route is null)
        {
            return null;
        }

        var moved = candidate.Route.Stops.Where(s => s.OrderId == candidate.OrderId).ToList();
        candidate.Route.Stops.RemoveAll(s => s.OrderId == candidate.OrderId);
        target.Stops.AddRange(moved);
        var machineId = lookup.MachineIdOf(candidate.OrderId);
        return Result(routes, plan, lookup, options,
        [
            $"nalog {candidate.OrderId} (stroj {machineId}, {Format.Kg(candidate.Weight)}) premješten je s kamiona {candidate.Route.Truck.Id} na kraj rute kamiona {target.Truck.Id}, čija je nosivost {Format.Kg(target.Truck.CapacityKg)}",
            RecomputedNote,
        ]);
    }

    /// <summary>R12: when a truck carries its maximum number of machines, a delivery and the following pickup are swapped.</summary>
    private static DefectResult? TooManyMachines(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var routes = Editable(plan);
        foreach (var route in routes)
        {
            var machines = 0;
            for (var k = 0; k < route.Stops.Count - 1; k++)
            {
                var stop = route.Stops[k];
                var next = route.Stops[k + 1];
                if (stop.Action == StopAction.Delivery && next.Action == StopAction.Pickup && machines == route.Truck.MaxMachines)
                {
                    (route.Stops[k], route.Stops[k + 1]) = (next, stop);
                    var sameLocation = stop.LocationId == next.LocationId ? $" (obje radnje su na {stop.LocationId}, km se ne mijenjaju)" : string.Empty;
                    return Result(routes, plan, lookup, options,
                    [
                        $"na kamionu {route.Truck.Id} zamijenjeni su koraci {k + 1} i {k + 2}: preuzimanje naloga {next.OrderId} sada je prije isporuke naloga {stop.OrderId}{sameLocation}",
                        $"kamion tada nosi {machines + 1} {Format.Plural(machines + 1, "stroj", "stroja", "strojeva")}, a smije najviše {route.Truck.MaxMachines}",
                        RecomputedNote,
                    ]);
                }

                machines += stop.Action == StopAction.Pickup ? 1 : -1;
            }
        }

        return null;
    }

    /// <summary>R6: on the route with the fewest actions, the pickup and the delivery of its first order swap places.</summary>
    private static DefectResult? DeliveryBeforePickup(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var routes = Editable(plan);
        var route = routes.Where(r => r.Stops.Count > 0).MinBy(r => r.Stops.Count);
        if (route is null)
        {
            return null;
        }

        var orderId = route.Stops[0].OrderId;
        var pickup = route.Stops.FindIndex(s => s.OrderId == orderId && s.Action == StopAction.Pickup);
        var delivery = route.Stops.FindIndex(s => s.OrderId == orderId && s.Action == StopAction.Delivery);
        if (pickup < 0 || delivery < 0)
        {
            return null;
        }

        (route.Stops[pickup], route.Stops[delivery]) = (route.Stops[delivery], route.Stops[pickup]);
        return Result(routes, plan, lookup, options,
        [
            $"na kamionu {route.Truck.Id} zamijenjeni su preuzimanje (korak {pickup + 1}) i isporuka (korak {delivery + 1}) naloga {orderId}: isporuka je sada prije preuzimanja",
            RecomputedNote,
        ]);
    }

    /// <summary>R5: the last delivery of the longest route moves to the end of another truck that can carry the machine.</summary>
    private static DefectResult? DifferentTrucks(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var routes = Editable(plan);
        var source = routes.Where(r => r.Stops.Count > 0).MaxBy(r => r.Stops.Count);
        if (source is null || source.Stops[^1].Action != StopAction.Delivery)
        {
            return null;
        }

        var delivery = source.Stops[^1];
        var target = routes.FirstOrDefault(r => r != source && lookup.WeightOf(delivery.OrderId) <= r.Truck.CapacityKg);
        if (target is null)
        {
            return null;
        }

        source.Stops.RemoveAt(source.Stops.Count - 1);
        target.Stops.Add(delivery);
        return Result(routes, plan, lookup, options,
        [
            $"isporuka naloga {delivery.OrderId} na {delivery.LocationId} (zadnja radnja kamiona {source.Truck.Id}) premještena je na kraj rute kamiona {target.Truck.Id}",
            $"preuzimanje naloga {delivery.OrderId} ostaje na kamionu {source.Truck.Id}",
            RecomputedNote,
        ]);
    }

    /// <summary>R2: the route with the longest return leg reports km and driving time only up to its last action.</summary>
    private static DefectResult? MissingReturn(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var candidates = plan.Routes
            .Select((route, index) => (Route: route, Index: index, Return: route.Stops.Count == 0 ? 0 : ReturnLeg(route, lookup)))
            .Where(x => x.Return > KmTolerance)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var chosen = candidates.MaxBy(x => x.Return);
        var route = chosen.Route;
        var lastKm = route.Stops[^1].CumulativeKm;
        var broken = route with
        {
            TotalKm = lastKm,
            DrivingMinutes = options.DrivingMinutes(lastKm),
        };
        var routes = plan.Routes.Select((r, i) => i == chosen.Index ? broken : r).ToList();
        return new DefectResult(
            plan with { Routes = routes, TotalKm = SumKm(routes) },
            [
                $"kamionu {route.Truck.Id} izostavljen je povratak {route.Stops[^1].LocationId} → {lookup.Scenario.BaseLocationId} ({Format.Km(chosen.Return)})",
                $"km rute {Format.Km(lastKm)} umjesto {Format.Km(route.TotalKm)}; vrijeme vožnje i ukupni km plana također bez povratka",
            ]);
    }

    /// <summary>R16: the plan total is off by <see cref="WrongKmOffset"/>; the routes are unchanged.</summary>
    private static DefectResult? WrongTotalKm(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var sum = SumKm(plan.Routes);
        var wrong = sum + WrongKmOffset;
        return new DefectResult(
            plan with { TotalKm = wrong },
            [
                $"ukupni km plana je {Format.Km(wrong)}, a zbroj km ruta {Format.Km(sum)} (uvećano za {Format.Km(WrongKmOffset)})",
                "rute, dionice i vremena nisu mijenjani",
            ]);
    }

    /// <summary>R32 / R23: the first order of the route with the fewest actions is removed (listed as K3, or not at all).</summary>
    private static DefectResult? LeaveOut(Plan plan, Lookup lookup, PlanningOptions options, bool listAsInfeasible)
    {
        var routes = Editable(plan);
        var route = routes.Where(r => r.Stops.Count > 0).MinBy(r => r.Stops.Count);
        var order = route is null ? null : lookup.Order(route.Stops[0].OrderId);
        if (route is null || order is null)
        {
            return null;
        }

        route.Stops.RemoveAll(s => s.OrderId == order.Id);
        var infeasible = plan.InfeasibleOrders.ToList();
        string listed;
        if (listAsInfeasible)
        {
            infeasible.Add(new InfeasibleOrder(order, InfeasibilityReason.DoesNotFitWithOtherOrders,
                "Ne stane u radni dan (namjerno izostavljen u testnom scenariju)."));
            listed = $"i naveden kao neizvediv (K3), iako stane u plan";
        }
        else
        {
            listed = "i nije naveden na popisu neizvedivih naloga";
        }

        return Result(routes, plan with { InfeasibleOrders = infeasible }, lookup, options,
        [
            $"nalog {order.Id} uklonjen je iz rute kamiona {route.Truck.Id} {listed}",
            RecomputedNote,
        ]);
    }

    /// <summary>R20: every action of the other trucks is appended to the route of the truck with the largest capacity.</summary>
    private static DefectResult? WorkdayExceeded(Plan plan, Lookup lookup, PlanningOptions options)
    {
        var routes = Editable(plan);
        var target = routes.MaxBy(r => r.Truck.CapacityKg)!;
        var others = routes.Where(r => r != target && r.Stops.Count > 0).ToList();
        if (others.Count == 0)
        {
            return null;
        }

        var movedCount = others.Sum(r => r.Stops.Count);
        foreach (var other in others)
        {
            target.Stops.AddRange(other.Stops);
            other.Stops.Clear();
        }

        var result = Result(routes, plan, lookup, options, []);
        var route = result.Plan.Routes.First(r => r.Truck == target.Truck);
        var counted = options.WorkdayMode == WorkdayMode.DrivingOnly ? route.DrivingMinutes : route.TotalMinutes;
        return result with
        {
            Changes =
            [
                $"sve radnje kamiona {string.Join(", ", others.Select(r => r.Truck.Id))} ({movedCount} {Format.Plural(movedCount, "radnja", "radnje", "radnji")}) nadovezane su na kraj rute kamiona {target.Truck.Id}; ostali kamioni nisu korišteni",
                $"kamion {target.Truck.Id} sada treba {Format.Coordinate(Math.Round(counted, 1))} min ({PlanMapperText(options)}), a radni dan ima {Format.Coordinate(options.MaxMinutes)} min",
                RecomputedNote,
            ],
        };
    }

    private static string PlanMapperText(PlanningOptions options) =>
        options.WorkdayMode == WorkdayMode.DrivingOnly ? "samo vožnja" : "vožnja + radnje";

    // ---------- rebuilding a changed plan ----------

    private sealed record EditableRoute(Truck Truck, List<Stop> Stops);

    private static List<EditableRoute> Editable(Plan plan) => plan.Routes.Select(r => new EditableRoute(r.Truck, r.Stops.ToList())).ToList();

    private static DefectResult Result(List<EditableRoute> routes, Plan plan, Lookup lookup, PlanningOptions options, IReadOnlyList<string> changes)
    {
        var rebuilt = routes.Select(r => BuildRoute(r.Truck, r.Stops, lookup, options)).ToList();
        return new DefectResult(plan with { Routes = rebuilt, TotalKm = SumKm(rebuilt) }, changes);
    }

    /// <summary>
    /// Recomputes a route the way a planner reports it: legs rounded (R14) and summed in tenths, minutes = driving +
    /// actions, load after each action = previous load ± machine weight. Nothing is checked – that is the validator's job.
    /// </summary>
    private static TruckRoute BuildRoute(Truck truck, IReadOnlyList<Stop> stops, Lookup lookup, PlanningOptions options)
    {
        if (stops.Count == 0)
        {
            return new TruckRoute(truck, [], 0, 0, 0, 0);
        }

        var previous = lookup.Base;
        var km10 = 0;
        var load = 0;
        var machines = 0;
        var rebuilt = new List<Stop>(stops.Count);
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var location = lookup.Location(stop.LocationId);
            var leg = Distance.Leg(previous, location);
            km10 += Tenths(leg);
            var weight = lookup.WeightOf(stop.OrderId);
            var pickup = stop.Action == StopAction.Pickup;
            load += pickup ? weight : -weight;
            machines += pickup ? 1 : -1;
            var km = km10 / 10.0;
            rebuilt.Add(stop with
            {
                LoadKgAfter = load,
                MachinesAfter = machines,
                LegKm = leg,
                CumulativeKm = km,
                CumulativeMinutes = options.DrivingMinutes(km) + (i + 1) * options.MinutesPerAction,
            });
            previous = location;
        }

        var totalKm = (km10 + Tenths(Distance.Leg(previous, lookup.Base))) / 10.0;
        return new TruckRoute(truck, rebuilt, totalKm, options.DrivingMinutes(totalKm), stops.Count * options.MinutesPerAction,
            stops.Select(s => s.OrderId).Distinct(StringComparer.Ordinal).Count());
    }

    private static double ReturnLeg(TruckRoute route, Lookup lookup) =>
        Distance.Leg(lookup.Location(route.Stops[^1].LocationId), lookup.Base);

    private static double SumKm(IEnumerable<TruckRoute> routes) => routes.Sum(r => Tenths(r.TotalKm)) / 10.0;

    private static int Tenths(double km) => (int)Math.Round(km * 10, MidpointRounding.AwayFromZero);

    /// <summary>Lookups into the (valid) task data used by the defects.</summary>
    private sealed class Lookup(Scenario scenario)
    {
        private readonly Dictionary<string, Location> _locations = scenario.Locations.DistinctBy(l => l.Id).ToDictionary(l => l.Id);
        private readonly Dictionary<string, Machine> _machines = scenario.Machines.DistinctBy(m => m.Id).ToDictionary(m => m.Id);
        private readonly Dictionary<string, TransportOrder> _orders = scenario.Orders.DistinctBy(o => o.Id).ToDictionary(o => o.Id);

        public Scenario Scenario { get; } = scenario;

        public Location Base => _locations[Scenario.BaseLocationId];

        public Location Location(string id) => _locations[id];

        public TransportOrder? Order(string id) => _orders.GetValueOrDefault(id);

        public string? MachineIdOf(string orderId) => Order(orderId)?.MachineId;

        public int WeightOf(string orderId) =>
            MachineIdOf(orderId) is { } machineId && _machines.TryGetValue(machineId, out var machine) ? machine.WeightKg : 0;
    }

    // ---------- helpers ----------

    private static T? Find<T>(IEnumerable<T> items, Func<T, string> key, string? value) where T : class =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : items.FirstOrDefault(i => string.Equals(key(i), value.Trim(), StringComparison.OrdinalIgnoreCase));

    // Read-only copies, so no case (and no caller) can change the task data through a cast to List<T>.
    private static Scenario Freeze(Scenario scenario) => scenario with
    {
        Trucks = new ReadOnlyCollection<Truck>([.. scenario.Trucks]),
        Machines = new ReadOnlyCollection<Machine>([.. scenario.Machines]),
        Locations = new ReadOnlyCollection<Location>([.. scenario.Locations]),
        Orders = new ReadOnlyCollection<TransportOrder>([.. scenario.Orders]),
    };
}
