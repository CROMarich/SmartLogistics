using System.Globalization;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;

namespace SmartLogistics.Tests;

/// <summary>
/// One planning case on the task data (data/scenario.json): ExactDp plan, OR-Tools plan, and a second OR-Tools
/// call on a fresh scenario instance for the determinism check (R39).
/// </summary>
public sealed record OrToolsCase(
    Scenario Scenario,
    PlanningOptions Options,
    Plan ExactPlan,
    OrToolsResult OrTools,
    Lazy<OrToolsResult> OrToolsSecondCall);

/// <summary>
/// Computes every plan once per test run (one OR-Tools call takes ~2–2.5 s, docs/odluke.md T55 c) and shares it
/// between the tests. Each case is lazy, so an exception in one case only fails the tests of that case.
/// </summary>
public sealed class OrToolsPlansFixture
{
    private readonly Dictionary<(WorkdayMode Mode, double MaxMinutes), Lazy<OrToolsCase>> _cases = [];

    public OrToolsPlansFixture()
    {
        foreach (var mode in new[] { WorkdayMode.Total, WorkdayMode.DrivingOnly })
        {
            foreach (var maxMinutes in new[] { 540.0, 300.0 })
            {
                var options = PlanningOptions.Default with { WorkdayMode = mode, MaxMinutes = maxMinutes };
                _cases[(mode, maxMinutes)] = new Lazy<OrToolsCase>(() => Compute(options), LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }
    }

    public OrToolsCase Get(WorkdayMode mode, double maxMinutes) => _cases[(mode, maxMinutes)].Value;

    private static Scenario LoadScenario() => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath);

    private static OrToolsCase Compute(PlanningOptions options)
    {
        var scenario = LoadScenario();
        var exact = new ExactDpPlanner().Solve(scenario, options);
        var orTools = new OrToolsPlanner().SolveWithStatistics(scenario, options);

        // Second call: new planner and a freshly loaded scenario, so no state can be shared with the first call.
        var second = new Lazy<OrToolsResult>(
            () => new OrToolsPlanner().SolveWithStatistics(LoadScenario(), options),
            LazyThreadSafetyMode.ExecutionAndPublication);

        return new OrToolsCase(scenario, options, exact, orTools, second);
    }
}

/// <summary>
/// The OR-Tools solver runs with a shared 5 s time budget (T55); a run stopped by time instead of the solution limit
/// may differ between calls. These tests therefore run in their own non-parallel collection, so that CPU load
/// from the other test classes does not decide whether the determinism test (R39) passes.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrToolsCollection : ICollectionFixture<OrToolsPlansFixture>
{
    public const string Name = "OR-Tools planner";
}

/// <summary>
/// Tech lead's test "OR-Tools result is valid and >= ExactDp result" (docs/odluke.md T59), on the task data in both
/// working-day modes, with the default 540 min and a shorter day of 300 min (not every order fits, so the
/// lexicographic comparison is exercised with omitted orders too).
/// The planners are created directly (<c>new ExactDpPlanner()</c>, <c>new OrToolsPlanner()</c>), not via
/// <see cref="PlannerLocator"/>.
/// </summary>
[Collection(OrToolsCollection.Name)]
public class OrToolsPlannerTests(OrToolsPlansFixture fixture)
{
    // Plan km is a sum of one-decimal legs; 0.05 separates neighbouring one-decimal values (T20).
    private const double KmTolerance = 0.05;

    public static TheoryData<WorkdayMode, double> Cases => new()
    {
        { WorkdayMode.Total, 540 },
        { WorkdayMode.DrivingOnly, 540 },
        { WorkdayMode.Total, 300 },
        { WorkdayMode.DrivingOnly, 300 },
    };

    private static int PlannedOrders(Plan plan) => plan.Routes.Sum(r => r.OrderCount);

    private static string Describe(Plan plan) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{PlannedOrders(plan)} orders, {plan.TotalKm:0.0} km, omitted [{string.Join(", ", plan.InfeasibleOrders.Select(o => o.Order.Id))}], ")
        + string.Join("; ", plan.Routes.Select(r =>
            string.Create(CultureInfo.InvariantCulture, $"{r.Truck.Id} {r.TotalKm:0.0} km ({r.OrderCount})")));

    /// <summary>Everything that identifies a plan: route order, stop order, legs, and the omitted orders with reasons.</summary>
    private static IReadOnlyList<string> Signature(Plan plan)
    {
        var lines = new List<string>();
        foreach (var route in plan.Routes)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"route {route.Truck.Id} {route.TotalKm:0.0} km"));
            lines.AddRange(route.Stops.Select(s => string.Create(CultureInfo.InvariantCulture,
                $"  {route.Truck.Id} {s.Action} {s.OrderId} @ {s.LocationId} leg {s.LegKm:0.0}")));
        }

        lines.AddRange(plan.InfeasibleOrders.Select(o => $"omitted {o.Order.Id} {o.Reason}"));
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"total {plan.TotalKm:0.0}"));
        return lines;
    }

    [Theory]
    [Trait("Rule", "R24")]
    [Trait("Rule", "R32")]
    [MemberData(nameof(Cases))]
    public void OrTools_plan_passes_the_validator(WorkdayMode mode, double maxMinutes)
    {
        var c = fixture.Get(mode, maxMinutes);

        ViolationAssert.Empty(PlanValidator.Validate(c.OrTools.Plan, c.Scenario, c.Options));
    }

    /// <summary>
    /// "OR-Tools >= ExactDp" is compared lexicographically by the objective of R32 – (1) more planned orders,
    /// (2) fewer km – and NOT by km alone: a plan that omits an order can have fewer km than the optimum with all
    /// orders (OR-Tools without the repair step gave 156.4 km with 9 of 10 orders in Total vs. the optimum 200.1 km
    /// with 10, T55 b), so "km(OR-Tools) >= km(ExactDp)" would fail for a plan that is in fact worse, and would pass
    /// for a plan that violates R32. Hence:
    /// OR-Tools may not plan MORE orders than the exact optimum; with the SAME number of orders its km may not be
    /// lower than the optimum (minus the 0.05 one-decimal tolerance); with FEWER orders it is worse regardless of km.
    /// Either failure means a bug in one of the planners (ExactDp not optimal, or an invalid OR-Tools plan).
    /// </summary>
    [Theory]
    [Trait("Rule", "R24")]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R38")]
    [MemberData(nameof(Cases))]
    public void OrTools_result_is_not_better_than_ExactDp_lexicographically(WorkdayMode mode, double maxMinutes)
    {
        var c = fixture.Get(mode, maxMinutes);
        var exactOrders = PlannedOrders(c.ExactPlan);
        var orToolsOrders = PlannedOrders(c.OrTools.Plan);
        var context = $"ExactDp: {Describe(c.ExactPlan)}{Environment.NewLine}OR-Tools: {Describe(c.OrTools.Plan)}";

        // The comparison is only meaningful against a valid exact plan.
        ViolationAssert.Empty(PlanValidator.Validate(c.ExactPlan, c.Scenario, c.Options));

        Assert.True(orToolsOrders <= exactOrders,
            $"OR-Tools planned more orders than the exact optimum.{Environment.NewLine}{context}");

        if (orToolsOrders == exactOrders)
        {
            Assert.True(c.OrTools.Plan.TotalKm >= c.ExactPlan.TotalKm - KmTolerance,
                $"OR-Tools has fewer km than the exact optimum with the same number of orders.{Environment.NewLine}{context}");
        }
    }

    [Fact]
    [Trait("Rule", "R38")]
    public void ExactDp_guarantees_the_optimum_and_OrTools_does_not()
    {
        var exact = Assert.IsAssignableFrom<IPlannerCapabilities>(new ExactDpPlanner());
        var orTools = Assert.IsAssignableFrom<IPlannerCapabilities>(new OrToolsPlanner());

        Assert.True(exact.GuaranteesOptimum);
        Assert.False(orTools.GuaranteesOptimum);
        Assert.False(string.IsNullOrWhiteSpace(exact.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(orTools.DisplayName));
        Assert.NotEqual(exact.DisplayName, orTools.DisplayName);
    }

    [Theory]
    [Trait("Rule", "R38")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void OrTools_result_statistics_say_the_optimum_is_not_guaranteed(WorkdayMode mode)
    {
        var c = fixture.Get(mode, 540);

        Assert.False(c.OrTools.Statistics.OptimumGuaranteed);
    }

    /// <summary>
    /// Two calls on the same input give the same plan: total km, route km, and the exact order of stops
    /// (truck, action, order, location, leg) plus the omitted orders. If this fails, the failure message says
    /// whether a run was stopped by the time limit (T55 c) – the test is not to be relaxed in that case.
    /// </summary>
    [Theory]
    [Trait("Rule", "R39")]
    [MemberData(nameof(Cases))]
    public void OrTools_is_deterministic_for_the_same_input(WorkdayMode mode, double maxMinutes)
    {
        var c = fixture.Get(mode, maxMinutes);
        var first = c.OrTools;
        var second = c.OrToolsSecondCall.Value;
        var limits = $"stopped by solution limit: first={first.Statistics.StoppedBySolutionLimit}, "
            + $"second={second.Statistics.StoppedBySolutionLimit}";

        Assert.True(Math.Abs(first.Plan.TotalKm - second.Plan.TotalKm) < 1e-9,
            $"Total km differs: {first.Plan.TotalKm} vs {second.Plan.TotalKm} ({limits}).");
        Assert.True(Signature(first.Plan).SequenceEqual(Signature(second.Plan)),
            $"Plans differ ({limits}).{Environment.NewLine}First:{Environment.NewLine}{string.Join(Environment.NewLine, Signature(first.Plan))}"
            + $"{Environment.NewLine}Second:{Environment.NewLine}{string.Join(Environment.NewLine, Signature(second.Plan))}");
    }

    /// <summary>
    /// Quality of the heuristic, NOT a business rule: a heuristic may legitimately be worse than the optimum (R38).
    /// On the task data the current settings (T55) reach the reference optimum of CLAUDE.md with all 10 orders –
    /// Total 200.1 km, DrivingOnly 182.2 km. If the OR-Tools settings change, this may become red without any rule
    /// being broken (T59 point 3).
    /// </summary>
    [Theory]
    [Trait("Category", "HeuristicQuality")]
    [InlineData(WorkdayMode.Total, 200.1)]       // CLAUDE.md: Total optimum 200.1 km
    [InlineData(WorkdayMode.DrivingOnly, 182.2)] // CLAUDE.md: DrivingOnly optimum 182.2 km
    public void Quality_OrTools_reaches_the_reference_optimum_on_the_task_data(WorkdayMode mode, double expectedKm)
    {
        var c = fixture.Get(mode, 540);

        Assert.Equal(c.Scenario.Orders.Count, PlannedOrders(c.OrTools.Plan)); // 10 of 10
        Assert.Empty(c.OrTools.Plan.InfeasibleOrders);
        Assert.Equal(expectedKm, c.OrTools.Plan.TotalKm, KmTolerance);
        Assert.Equal(expectedKm, c.OrTools.Plan.Routes.Sum(r => r.TotalKm), KmTolerance);
    }
}
