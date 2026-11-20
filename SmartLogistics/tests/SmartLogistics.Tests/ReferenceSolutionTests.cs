using System.Globalization;
using System.Text;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;

namespace SmartLogistics.Tests;

/// <summary>
/// Solver results for data/scenario.json against the independently computed reference values in CLAUDE.md.
/// The solver is located by reflection (<see cref="PlannerLocator"/>); until it exists these tests fail.
/// The per-truck split (A 37.2 / B 162.9) is not tested: docs/spec.md does not show that the optimum is unique.
/// </summary>
public class ReferenceSolutionTests
{
    // Plan km is a sum of one-decimal legs; 0.05 separates neighbouring one-decimal values.
    private const double KmTolerance = 0.05;

    private static Scenario LoadScenario() => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath);

    private static Plan Solve(WorkdayMode mode, out Scenario scenario, out PlanningOptions options)
    {
        scenario = LoadScenario();
        options = PlanningOptions.Default with { WorkdayMode = mode };
        return PlannerLocator.Create().Solve(scenario, options);
    }

    [Theory]
    [Trait("Rule", "R24")]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R38")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total, 200.1)]       // CLAUDE.md: Total optimum 200.1 km
    [InlineData(WorkdayMode.DrivingOnly, 182.2)] // CLAUDE.md: DrivingOnly optimum 182.2 km
    public void Solver_total_km_matches_the_reference_optimum(WorkdayMode mode, double expectedKm)
    {
        var plan = Solve(mode, out _, out _);

        Assert.Equal(expectedKm, plan.TotalKm, KmTolerance);
        Assert.Equal(expectedKm, plan.Routes.Sum(r => r.TotalKm), KmTolerance);
    }

    [Theory]
    [Trait("Rule", "R2")]
    [Trait("Rule", "R4")]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R12")]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R23")]
    [Trait("Rule", "R25")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Solver_plan_passes_the_validator(WorkdayMode mode)
    {
        var plan = Solve(mode, out var scenario, out var options);

        ViolationAssert.Empty(PlanValidator.Validate(plan, scenario, options));
    }

    [Theory]
    [Trait("Rule", "R1")]
    [Trait("Rule", "R25")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Solver_plan_has_one_route_per_scenario_truck(WorkdayMode mode)
    {
        var plan = Solve(mode, out var scenario, out _);

        Assert.Equal(
            scenario.Trucks.Select(t => t.Id).Order(StringComparer.Ordinal),
            plan.Routes.Select(r => r.Truck.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Rule", "R21")]
    public void DrivingOnly_optimum_is_not_longer_than_Total_optimum()
    {
        // spec 3.8: every Total-feasible plan is DrivingOnly-feasible, so min km(DrivingOnly) <= min km(Total).
        var total = Solve(WorkdayMode.Total, out _, out _);
        var drivingOnly = Solve(WorkdayMode.DrivingOnly, out _, out _);

        Assert.True(
            drivingOnly.TotalKm <= total.TotalKm + 1e-9,
            $"DrivingOnly {drivingOnly.TotalKm:F1} km > Total {total.TotalKm:F1} km");
    }

    [Theory]
    [Trait("Rule", "R39")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Solver_is_deterministic(WorkdayMode mode)
    {
        var first = Describe(Solve(mode, out _, out _));
        var second = Describe(Solve(mode, out _, out _));

        Assert.Equal(first, second);
    }

    /// <summary>Canonical text of a plan (km to 0.1) so two runs can be compared without float equality.</summary>
    private static string Describe(Plan plan)
    {
        var text = new StringBuilder();
        foreach (var route in plan.Routes)
        {
            text.Append(CultureInfo.InvariantCulture, $"{route.Truck.Id} {route.TotalKm:F1} km {route.OrderCount} orders:");
            foreach (var stop in route.Stops)
            {
                text.Append(CultureInfo.InvariantCulture, $" {stop.Action} {stop.OrderId}@{stop.LocationId} {stop.LoadKgAfter}kg/{stop.MachinesAfter};");
            }

            text.AppendLine();
        }

        foreach (var infeasible in plan.InfeasibleOrders)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"infeasible {infeasible.Order.Id} {infeasible.Reason}");
        }

        text.Append(CultureInfo.InvariantCulture, $"total {plan.TotalKm:F1}");
        return text.ToString();
    }
}
