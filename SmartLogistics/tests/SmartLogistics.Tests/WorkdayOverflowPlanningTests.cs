using SmartLogistics.Core.Domain;
using SmartLogistics.Tests.Support;
using Xunit.Abstractions;
using static SmartLogistics.Tests.Support.RobustPlanning;

namespace SmartLogistics.Tests;

/// <summary>
/// Phase 5, R32 + R40: when not all orders fit into the working day, the planner still returns a plan,
/// leaves out the smallest possible number of orders (K3, "Ne stane u radni dan") and, among those,
/// picks the plan with the fewest km. Scenario: task data with MaxMinutes = 300.
///
/// Why at least one order must be left out in Total mode at 300 min:
/// 10 orders x 2 actions x 20 min = 400 min of actions. Any plan with every truck <= 300 min also has
/// every truck <= 540 min, so its km are >= the 540-min Total optimum 200.1 km (CLAUDE.md). Total time
/// is then >= 400 + 200.1 = 600.1 min > 2 trucks x 300 min = 600 min. Hence not all 10 fit.
///
/// Minimality and "fewest km among equals" are checked against <see cref="ExhaustiveOracle"/>, a
/// test-only exhaustive search written from the spec, which is first checked against the CLAUDE.md
/// reference values at 540 min.
/// </summary>
public class WorkdayOverflowPlanningTests(ITestOutputHelper output)
{
    private const double ShortDay = 300;

    [Theory]
    [Trait("Rule", "R32")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Oracle_reproduces_the_reference_optimum_at_540_min(WorkdayMode mode)
    {
        // Self-check of the test oracle: CLAUDE.md gives 200.1 km (Total) and 182.2 km (DrivingOnly), all 10 orders.
        var result = ExhaustiveOracle.Solve(TestScenarios.Task, Options(mode));

        Assert.Equal(10, result.PlannedOrders);
        Assert.Equal(ReferenceKm(mode), result.TotalKm, KmTolerance);
    }

    [Theory]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R23")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total, 300)]
    [InlineData(WorkdayMode.DrivingOnly, 300)]
    [InlineData(WorkdayMode.Total, 200)]
    [InlineData(WorkdayMode.DrivingOnly, 80)]
    public void Short_day_plan_is_returned_without_exception_and_passes_the_validator(WorkdayMode mode, double maxMinutes)
    {
        var scenario = TestScenarios.Task;
        var options = Options(mode, maxMinutes);

        var plan = SolveWithoutException(scenario, options);

        EachOrderExactlyOnce(plan, scenario);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R29")]
    [InlineData(WorkdayMode.Total, 300)]
    [InlineData(WorkdayMode.DrivingOnly, 300)]
    [InlineData(WorkdayMode.DrivingOnly, 80)]
    public void Short_day_every_truck_fits_into_the_limit(WorkdayMode mode, double maxMinutes)
    {
        var scenario = TestScenarios.Task;
        var options = Options(mode, maxMinutes);

        var plan = SolveWithoutException(scenario, options);

        foreach (var route in plan.Routes)
        {
            var minutes = RecomputedWorkdayMinutes(route, scenario, options);
            Assert.True(minutes <= maxMinutes + LimitEpsilon, $"Truck {route.Truck.Id} needs {minutes} min > {maxMinutes} min ({mode}).");
        }
    }

    [Fact]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R23")]
    public void Short_day_Total_leaves_out_at_least_one_order_and_all_left_out_are_K3_with_message()
    {
        var scenario = TestScenarios.Task;
        var options = Options(WorkdayMode.Total, ShortDay);

        var plan = SolveWithoutException(scenario, options);

        // At least one: see the class comment (>= 600.1 min needed for all 10, 600 min available).
        Assert.NotEmpty(plan.InfeasibleOrders);
        Assert.Equal(10, PlannedOrderIds(plan).Count + plan.InfeasibleOrders.Count);
        EachOrderExactlyOnce(plan, scenario);
        Assert.All(plan.InfeasibleOrders, e =>
        {
            // Every order fits alone (longest standalone route: N06 on B, L0-L5-L7-L0 = 26.2 + 12.5 + 21.5
            // = 60.2 km + 2 x 20 min = 100.2 min <= 300), so K2 is impossible and the category is K3 (R33).
            Assert.Equal(InfeasibilityReason.DoesNotFitWithOtherOrders, e.Reason);
            Assert.StartsWith("Ne stane u radni dan", e.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R33")]
    [InlineData(WorkdayMode.Total, 300)]
    [InlineData(WorkdayMode.DrivingOnly, 80)]
    public void Short_day_K3_message_starts_with_the_R40_text_and_contains_the_limit(WorkdayMode mode, double maxMinutes)
    {
        // R40: "Ne stane u radni dan" with the workday mode and the limit.
        // DrivingOnly at 300 min drops nothing, so DrivingOnly uses 80 min: any plan with <= 80 min per truck
        // is also a 540-min plan, so it needs >= 182.2 km (CLAUDE.md) > 2 x 80 = 160 -> at least one is left out.
        // The longest standalone route is 60.2 km (N06 on B) <= 80, so every left-out order is K3, not K2.
        var plan = SolveWithoutException(TestScenarios.Task, Options(mode, maxMinutes));

        Assert.NotEmpty(plan.InfeasibleOrders);
        var limit = maxMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.All(plan.InfeasibleOrders, e =>
        {
            Assert.Equal(InfeasibilityReason.DoesNotFitWithOtherOrders, e.Reason);
            Assert.StartsWith("Ne stane u radni dan", e.Message, StringComparison.Ordinal);
            Assert.Contains(limit, e.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R24")]
    [InlineData(WorkdayMode.Total, 300)]
    [InlineData(WorkdayMode.DrivingOnly, 300)]
    [InlineData(WorkdayMode.Total, 200)]
    [InlineData(WorkdayMode.DrivingOnly, 80)]
    public void Short_day_leaves_out_the_minimum_number_of_orders_with_the_fewest_km(WorkdayMode mode, double maxMinutes)
    {
        var scenario = TestScenarios.Task;
        var options = Options(mode, maxMinutes);
        var best = ExhaustiveOracle.Solve(scenario, options);
        output.WriteLine($"{mode} @ {maxMinutes} min: oracle best = {best.PlannedOrders} orders, {best.TotalKm:0.0} km");

        var plan = SolveWithoutException(scenario, options);
        output.WriteLine($"planner: {PlannedOrderIds(plan).Count} orders, {plan.TotalKm:0.0} km, left out: " +
            string.Join(", ", plan.InfeasibleOrders.Select(e => e.Order.Id)));

        Assert.Equal(best.PlannedOrders, PlannedOrderIds(plan).Count);
        Assert.Equal(best.TotalKm, plan.TotalKm, KmTolerance);
    }

    [Fact]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R21")]
    public void Short_day_DrivingOnly_leaves_out_no_more_orders_than_Total()
    {
        // Every Total-feasible plan is DrivingOnly-feasible (actions only add time), so DrivingOnly can plan
        // at least as many orders.
        var total = SolveWithoutException(TestScenarios.Task, Options(WorkdayMode.Total, ShortDay));
        var drivingOnly = SolveWithoutException(TestScenarios.Task, Options(WorkdayMode.DrivingOnly, ShortDay));

        Assert.True(drivingOnly.InfeasibleOrders.Count <= total.InfeasibleOrders.Count,
            $"DrivingOnly left out {drivingOnly.InfeasibleOrders.Count}, Total {total.InfeasibleOrders.Count}.");
    }

    [Theory]
    [Trait("Rule", "R39")]
    [Trait("Rule", "R32")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Short_day_plan_is_deterministic(WorkdayMode mode)
    {
        var options = Options(mode, ShortDay);

        var first = Signature(SolveWithoutException(TestScenarios.Task, options));
        var second = Signature(SolveWithoutException(TestScenarios.Task, options));

        Assert.Equal(first, second);
    }
}
