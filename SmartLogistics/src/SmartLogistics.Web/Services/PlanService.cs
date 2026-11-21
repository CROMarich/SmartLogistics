using System.Collections.Concurrent;
using System.Diagnostics;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Web.Services;

/// <summary>Outcome of one planning run, ready for the plan page.</summary>
/// <param name="Snapshot">The store state that was planned (scenario, version, number of edits).</param>
/// <param name="Planner">The algorithm that was asked for (catalog entry).</param>
/// <param name="Plan">The plan; null when <paramref name="Error"/> is set.</param>
/// <param name="Violations">Validator result for the plan (empty = valid).</param>
/// <param name="Elapsed">Planner computation time (of the first run; later requests come from the cache).</param>
/// <param name="PlannerName">Type name of the planner implementation, or null when it is not registered.</param>
/// <param name="Error">Why there is no plan (Croatian, for the UI), or null.</param>
/// <param name="Statistics">Search effort of the exact planner, or null for another planner.</param>
/// <param name="SolverStatistics">How the OR-Tools search ended, or null for another planner.</param>
/// <param name="GuaranteesOptimum">
/// What the planner itself reports (<see cref="IPlannerCapabilities.GuaranteesOptimum"/>); a planner without the interface
/// counts as a heuristic (R38). Before a run (missing planner, load error) the catalog value is used.
/// </param>
/// <param name="ScenarioWarnings">Warnings about the input data from the feasibility check (e.g. duplicate trucks).</param>
/// <param name="PlannerUnavailable">True when the algorithm has no registration in the DI container.</param>
public sealed record PlanRunResult(
    ScenarioSnapshot Snapshot,
    PlanningOptions Options,
    PlannerDescriptor Planner,
    Plan? Plan,
    IReadOnlyList<PlanViolation> Violations,
    TimeSpan Elapsed,
    string? PlannerName,
    string? Error,
    ExactDpStatistics? Statistics,
    IReadOnlyList<string> ScenarioWarnings,
    bool PlannerUnavailable = false,
    OrToolsStatistics? SolverStatistics = null,
    bool? GuaranteesOptimum = null)
{
    public Scenario Scenario => Snapshot.Scenario;

    /// <summary>True when the plan is a proven optimum (spec R38).</summary>
    public bool IsExact => GuaranteesOptimum ?? Planner.IsExact;
}

/// <summary>
/// Runs a planner on the current scenario of <see cref="IScenarioStore"/> and caches the result per
/// (store version, options, algorithm), so reloading the plan page does not recompute it. Every order edit changes the
/// version, which invalidates the cache; entries of older versions are dropped. Each entry is computed at most once,
/// even under concurrent requests (a heuristic may run for seconds). The plan is validated again for display.
/// <para>
/// Planners are keyed <see cref="IRoutePlanner"/> services (key = <see cref="PlannerDescriptor.Key"/>, see
/// <see cref="PlannerCatalog"/>). The app never crashes because of a plan: a missing registration and every planner
/// failure become an error message for the page.
/// </para>
/// </summary>
public sealed class PlanService(IScenarioStore store, IServiceProvider services, ILogger<PlanService> logger)
{
    private readonly record struct CacheKey(long Version, PlanningOptions Options, string PlannerKey);

    private readonly ConcurrentDictionary<CacheKey, Lazy<PlanRunResult>> _cache = new();

    /// <summary>True when a planner is registered for the algorithm.</summary>
    public bool IsAvailable(PlannerDescriptor planner)
    {
        ArgumentNullException.ThrowIfNull(planner);
        return Resolve(planner) is not null;
    }

    /// <summary>Plans the current scenario of the store with the default (exact) algorithm.</summary>
    public PlanRunResult Run(PlanningOptions options) => Run(options, PlannerCatalog.Exact);

    /// <summary>Plans the current scenario of the store with the given options and algorithm.</summary>
    public PlanRunResult Run(PlanningOptions options, PlannerDescriptor planner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(planner);
        var snapshot = store.GetSnapshot();
        if (snapshot.LoadError is not null)
        {
            return new PlanRunResult(snapshot, options, planner, null, [], TimeSpan.Zero, null, snapshot.LoadError, null, []);
        }

        var implementation = Resolve(planner);
        if (implementation is null)
        {
            return new PlanRunResult(
                snapshot, options, planner, null, [], TimeSpan.Zero, null,
                $"Algoritam \"{planner.Name}\" nije dostupan: planer nije registriran u ovoj verziji aplikacije. Odaberite drugi algoritam.",
                null, [], PlannerUnavailable: true);
        }

        DropStale(snapshot.Version);
        var entry = _cache.GetOrAdd(
            new CacheKey(snapshot.Version, options, planner.Key),
            _ => new Lazy<PlanRunResult>(
                () => Compute(snapshot, options, planner, implementation),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return entry.Value;
    }

    /// <summary>
    /// The cached result for the current scenario version, or null when that combination has not been computed yet.
    /// Never starts a planner (used for the comparison table, which must not slow the page down).
    /// </summary>
    public PlanRunResult? TryGetCached(PlanningOptions options, PlannerDescriptor planner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(planner);
        var version = store.GetSnapshot().Version;
        return _cache.TryGetValue(new CacheKey(version, options, planner.Key), out var entry) && entry.IsValueCreated
            ? entry.Value
            : null;
    }

    private IRoutePlanner? Resolve(PlannerDescriptor planner) => services.GetKeyedService<IRoutePlanner>(planner.Key);

    // Entries for older versions can never be hit again.
    private void DropStale(long version)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.Version != version)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    private PlanRunResult Compute(ScenarioSnapshot snapshot, PlanningOptions options, PlannerDescriptor descriptor, IRoutePlanner planner)
    {
        var scenario = snapshot.Scenario;
        var plannerName = planner.GetType().Name;
        var guaranteesOptimum = planner is IPlannerCapabilities { GuaranteesOptimum: true };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Both planners of Core can also report statistics; any other IRoutePlanner just returns a plan.
            ExactDpStatistics? statistics = null;
            OrToolsStatistics? solverStatistics = null;
            IReadOnlyList<string>? warnings = null;
            Plan plan;
            if (planner is ExactDpPlanner exact)
            {
                var result = exact.SolveWithStatistics(scenario, options);
                (plan, statistics) = (result.Plan, result.Statistics);
                warnings = result.Feasibility?.Warnings ?? [];
            }
            else if (planner is OrToolsPlanner orTools)
            {
                var result = orTools.SolveWithStatistics(scenario, options);
                (plan, solverStatistics) = (result.Plan, result.Statistics);
                warnings = result.Feasibility?.Warnings;
            }
            else
            {
                plan = planner.Solve(scenario, options);
            }

            stopwatch.Stop();

            // Input warnings do not depend on the planner: the same check the exact planner runs first (not timed).
            warnings ??= Feasibility.Check(scenario, options).Warnings;
            var violations = PlanValidator.Validate(plan, scenario, options);
            return new PlanRunResult(
                snapshot, options, descriptor, plan, violations, stopwatch.Elapsed, plannerName, null, statistics, warnings,
                SolverStatistics: solverStatistics,
                GuaranteesOptimum: guaranteesOptimum);
        }
        catch (ArgumentException ex)
        {
            return Failure($"Planiranje nije pokrenuto (neispravne postavke ili ulaz): {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return Failure($"Ulaz je prevelik za planer \"{descriptor.Name}\": {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Failure($"Planer \"{descriptor.Name}\" prijavio je grešku (npr. plan koji ne prolazi validator): {ex.Message}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Any other planner failure (e.g. inside a native solver) must not take the page down.
            logger.LogError(ex, "Planner {Planner} failed", plannerName);
            return Failure($"Planer \"{descriptor.Name}\" prekinut je neočekivanom greškom ({ex.GetType().Name}): {ex.Message}");
        }

        PlanRunResult Failure(string error) =>
            new(snapshot, options, descriptor, null, [], stopwatch.Elapsed, plannerName, error, null, [], GuaranteesOptimum: guaranteesOptimum);
    }
}
