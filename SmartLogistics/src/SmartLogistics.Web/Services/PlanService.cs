using System.Collections.Concurrent;
using System.Diagnostics;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Web.Services;

/// <summary>Outcome of one planning run, ready for the plan and map pages.</summary>
public sealed record PlanRunResult(
    Scenario? Scenario,
    PlanningOptions Options,
    Plan? Plan,
    IReadOnlyList<PlanViolation> Violations,
    TimeSpan Elapsed,
    string PlannerName,
    string? Error,
    ExactDpStatistics? Statistics = null);

/// <summary>
/// Runs the planner for the current scenario and caches the result per (scenario file version, workday mode),
/// so switching pages does not recompute the plan. The plan is validated again here for display.
/// </summary>
public sealed class PlanService(ScenarioProvider scenarios, IRoutePlanner planner)
{
    private readonly ConcurrentDictionary<(DateTime Version, WorkdayMode Mode), PlanRunResult> _cache = new();

    public PlanRunResult Run(WorkdayMode mode)
    {
        var options = PlanningOptions.Default with { WorkdayMode = mode };
        var loaded = scenarios.Load();
        if (loaded.Scenario is null || loaded.LastModifiedLocal is null)
        {
            return new PlanRunResult(null, options, null, [], TimeSpan.Zero, PlannerName, loaded.Error);
        }

        return _cache.GetOrAdd((loaded.LastModifiedLocal.Value, mode), _ => Compute(loaded.Scenario, options));
    }

    private string PlannerName => planner.GetType().Name;

    private PlanRunResult Compute(Scenario scenario, PlanningOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // The exact planner can also report its search effort; any other IRoutePlanner just returns a plan.
            ExactDpStatistics? statistics = null;
            Plan plan;
            if (planner is ExactDpPlanner exact)
            {
                var result = exact.SolveWithStatistics(scenario, options);
                (plan, statistics) = (result.Plan, result.Statistics);
            }
            else
            {
                plan = planner.Solve(scenario, options);
            }

            stopwatch.Stop();
            var violations = PlanValidator.Validate(plan, scenario, options);
            return new PlanRunResult(scenario, options, plan, violations, stopwatch.Elapsed, PlannerName, null, statistics);
        }
        catch (ArgumentException ex)
        {
            return Failure($"Ulazni podaci nisu ispravni, planiranje nije pokrenuto: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return Failure($"Ulaz je prevelik za egzaktni planer: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Failure($"Planer je izračunao plan koji ne prolazi validator (greška u planeru): {ex.Message}");
        }

        PlanRunResult Failure(string error) =>
            new(scenario, options, null, [], stopwatch.Elapsed, PlannerName, error);
    }
}
