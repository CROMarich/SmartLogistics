using System.Collections.Concurrent;
using System.Diagnostics;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Web.Services;

/// <summary>Outcome of one planning run, ready for the plan and map pages.</summary>
public sealed record PlanRunResult(
    ScenarioExample Example,
    Scenario? Scenario,
    PlanningOptions Options,
    Plan? Plan,
    IReadOnlyList<PlanViolation> Violations,
    TimeSpan Elapsed,
    string PlannerName,
    string? Error,
    ExactDpStatistics? Statistics = null,
    IReadOnlyList<string>? ScenarioWarnings = null);

/// <summary>
/// Runs the planner for a scenario example and caches the result per (example, file version, workday mode),
/// so switching pages does not recompute the plan. The plan is validated again here for display.
/// The app never crashes because of a plan: every failure becomes an error message for the page.
/// </summary>
public sealed class PlanService(ScenarioProvider scenarios, IRoutePlanner planner)
{
    private readonly ConcurrentDictionary<(string Example, DateTime Version, WorkdayMode Mode), PlanRunResult> _cache = new();

    public PlanRunResult Run(WorkdayMode mode, string? exampleId = null)
    {
        var example = scenarios.FindExample(exampleId);
        var options = PlanningOptions.Default with
        {
            WorkdayMode = mode,
            MaxMinutes = example.MaxMinutes ?? PlanningOptions.Default.MaxMinutes,
        };

        var loaded = scenarios.Load(example);
        if (loaded.Scenario is null || loaded.LastModifiedLocal is null)
        {
            return new PlanRunResult(example, null, options, null, [], TimeSpan.Zero, PlannerName, loaded.Error);
        }

        return _cache.GetOrAdd(
            (example.Id, loaded.LastModifiedLocal.Value, mode),
            _ => Compute(example, loaded.Scenario, options));
    }

    private string PlannerName => planner.GetType().Name;

    private PlanRunResult Compute(ScenarioExample example, Scenario scenario, PlanningOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // The exact planner can also report its search effort; any other IRoutePlanner just returns a plan.
            ExactDpStatistics? statistics = null;
            IReadOnlyList<string> warnings = [];
            Plan plan;
            if (planner is ExactDpPlanner exact)
            {
                var result = exact.SolveWithStatistics(scenario, options);
                (plan, statistics) = (result.Plan, result.Statistics);
                warnings = result.Feasibility?.Warnings ?? [];
            }
            else
            {
                plan = planner.Solve(scenario, options);
            }

            stopwatch.Stop();
            var violations = PlanValidator.Validate(plan, scenario, options);
            return new PlanRunResult(example, scenario, options, plan, violations, stopwatch.Elapsed, PlannerName, null, statistics, warnings);
        }
        catch (ArgumentException ex)
        {
            return Failure($"Planiranje nije pokrenuto (neispravne postavke ili ulaz): {ex.Message}");
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
            new(example, scenario, options, null, [], stopwatch.Elapsed, PlannerName, error);
    }
}
