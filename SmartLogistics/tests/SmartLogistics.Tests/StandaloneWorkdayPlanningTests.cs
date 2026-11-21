using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.RobustPlanning;

namespace SmartLogistics.Tests;

/// <summary>
/// K2 (spec R33, D7, edge case RS17): an order whose stand-alone route base -> pickup -> delivery -> base
/// exceeds the working day on every truck that can carry its machine. Checked on both planners, on the task
/// data with one extra far-away order, and on a one-order scenario where every number is easy to compute by hand.
/// Rules used for the hand arithmetic: legs rounded to one decimal (R14), 60 km/h so 1 km = 1 min (R17),
/// 20 min per action (R18), pickup + delivery = 2 actions, return to L0 included (R2), limit 540 min inclusive
/// (R20 Total: driving + actions; R21 DrivingOnly: driving only).
/// </summary>
public class StandaloneWorkdayPlanningTests
{
    private static IRoutePlanner CreatePlanner(string planner) => planner switch
    {
        nameof(ExactDpPlanner) => new ExactDpPlanner(),
        nameof(OrToolsPlanner) => new OrToolsPlanner(),
        _ => throw new ArgumentOutOfRangeException(nameof(planner), planner, "Unknown planner."),
    };

    private static Plan Solve(string planner, Scenario scenario, PlanningOptions options)
    {
        Plan? plan = null;
        var exception = Record.Exception(() => plan = CreatePlanner(planner).Solve(scenario, options));
        Assert.True(exception is null, $"R37: the planner must not throw, but threw: {exception}");
        Assert.NotNull(plan);
        return plan!;
    }

    /// <summary>
    /// One 1000 kg order on the x axis: pickup LP (5, 0), delivery LF (<paramref name="deliveryX"/>, 0), base L0 (0, 0).
    /// Legs: L0 -> LP = 5.0, LP -> LF = X - 5, LF -> L0 = X; stand-alone km = 2X (for X with one decimal).
    /// Stand-alone minutes: Total = 2X + 2 x 20 = 2X + 40; DrivingOnly = 2X.
    /// </summary>
    private static (Scenario Scenario, TransportOrder Order) FarOrderScenario(double deliveryX)
    {
        var order = new TransportOrder("F1", "MF", "LP", "LF");
        var scenario = new Scenario(
            TestScenarios.Base,
            [new Truck("A", 3500, 2), new Truck("B", 10000, 3)],
            [new Machine("MF", 1000)],
            [new Location("L0", 0, 0), new Location("LP", 5, 0), new Location("LF", deliveryX, 0)],
            [order]);
        return (scenario, order);
    }

    // ---- Task data + one order 300 km away (RS17 example) ----

    [Theory]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R23")]
    [InlineData(nameof(ExactDpPlanner), WorkdayMode.Total, "640")]
    [InlineData(nameof(ExactDpPlanner), WorkdayMode.DrivingOnly, "600")]
    [InlineData(nameof(OrToolsPlanner), WorkdayMode.Total, "640")]
    public void Order_300_km_away_is_K2_and_the_ten_task_orders_are_still_planned(string planner, WorkdayMode mode, string neededMinutes)
    {
        // N11: M11 (1000 kg, fits A and B) from L0 to LF (300, 0).
        // Legs: L0 -> L0 = 0.0, L0 -> LF = 300.0, LF -> L0 = 300.0; stand-alone 600.0 km = 600 min driving.
        // Total: 600 + 2 x 20 = 640 min > 540; DrivingOnly: 600 min > 540 -> K2 in both modes on A and B.
        // The other 10 orders are the task data -> optimum 200.1 km (Total) / 182.2 km (DrivingOnly), CLAUDE.md.
        var n11 = new TransportOrder("N11", "M11", "L0", "LF");
        var scenario = TaskWith([new Machine("M11", 1000)], n11) with
        {
            Locations = [.. TestScenarios.Task.Locations, new Location("LF", 300, 0)],
        };
        var options = Options(mode);

        var plan = Solve(planner, scenario, options);

        var entry = SingleInfeasible(plan, n11, InfeasibilityReason.ExceedsWorkday);
        Assert.Contains(neededMinutes, entry.Message, StringComparison.Ordinal);
        Assert.Contains("540", entry.Message, StringComparison.Ordinal);
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(TestScenarios.Task.Orders.Select(o => o.Id).Order(StringComparer.Ordinal), PlannedOrderIds(plan));
        if (planner == nameof(ExactDpPlanner))
        {
            // Only the exact planner guarantees the optimum (OR-Tools km is checked in OrToolsPlannerTests).
            Assert.Equal(ReferenceKm(mode), plan.TotalKm, KmTolerance);
        }

        PassesValidator(plan, scenario, options);
    }

    // ---- Mode dependence (RS17: "may be feasible in DrivingOnly and infeasible in Total") ----

    [Theory]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R20")]
    [InlineData(nameof(ExactDpPlanner))]
    [InlineData(nameof(OrToolsPlanner))]
    public void Order_needing_550_min_in_Total_is_K2_with_minutes_and_limit_in_the_message(string planner)
    {
        // X = 255: legs 5.0 + 250.0 + 255.0 = 510.0 km; Total = 510 + 2 x 20 = 550 min > 540 -> K2.
        var (scenario, order) = FarOrderScenario(255);
        var options = Options(WorkdayMode.Total);

        var plan = Solve(planner, scenario, options);

        var entry = SingleInfeasible(plan, order, InfeasibilityReason.ExceedsWorkday);
        Assert.Contains("550", entry.Message, StringComparison.Ordinal);
        Assert.Contains("540", entry.Message, StringComparison.Ordinal);
        Assert.Contains("510", entry.Message, StringComparison.Ordinal);
        Assert.All(plan.Routes, r => Assert.Empty(r.Stops));
        Assert.Equal(0.0, plan.TotalKm, KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R21")]
    [InlineData(nameof(ExactDpPlanner))]
    [InlineData(nameof(OrToolsPlanner))]
    public void Same_order_is_planned_in_DrivingOnly_because_driving_alone_is_510_min(string planner)
    {
        // X = 255: driving 5.0 + 250.0 + 255.0 = 510.0 min <= 540 (actions do not count) -> not K2, planned.
        // The plan is exactly this one round trip: 510.0 km on one truck (which truck is not asserted).
        var (scenario, order) = FarOrderScenario(255);
        var options = Options(WorkdayMode.DrivingOnly);

        var plan = Solve(planner, scenario, options);

        Assert.Empty(plan.InfeasibleOrders);
        Assert.Equal(new[] { order.Id }, PlannedOrderIds(plan));
        Assert.Equal(510.0, plan.TotalKm, KmTolerance);
        Assert.Equal(510.0, plan.Routes.Sum(r => r.DrivingMinutes), KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    // ---- Boundary: exactly at the limit is not K2, one tenth of a km more on two legs is ----

    [Theory]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R35")]
    // Total, X = 250: 5.0 + 245.0 + 250.0 = 500.0 km; 500 + 40 = 540 min = limit -> planned, 500.0 km.
    [InlineData(WorkdayMode.Total, 250.0, 500.0)]
    // DrivingOnly, X = 270: 5.0 + 265.0 + 270.0 = 540.0 km = 540 min driving = limit -> planned, 540.0 km.
    [InlineData(WorkdayMode.DrivingOnly, 270.0, 540.0)]
    public void Order_that_fits_the_workday_exactly_at_the_limit_is_not_K2(WorkdayMode mode, double deliveryX, double expectedKm)
    {
        var (scenario, order) = FarOrderScenario(deliveryX);
        var options = Options(mode);

        var plan = Solve(nameof(ExactDpPlanner), scenario, options);

        Assert.Empty(plan.InfeasibleOrders);
        Assert.Equal(new[] { order.Id }, PlannedOrderIds(plan));
        Assert.Equal(expectedKm, plan.TotalKm, KmTolerance);
        var route = Assert.Single(plan.Routes, r => r.Stops.Count > 0);
        Assert.Equal(540.0, RecomputedWorkdayMinutes(route, scenario, options), 6);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    // Total, X = 250.1: 5.0 + 245.1 + 250.1 = 500.2 km; 500.2 + 40 = 540.2 min > 540 -> K2.
    [InlineData(WorkdayMode.Total, 250.1, "540.2")]
    // DrivingOnly, X = 270.1: 5.0 + 265.1 + 270.1 = 540.2 km = 540.2 min > 540 -> K2.
    [InlineData(WorkdayMode.DrivingOnly, 270.1, "540.2")]
    public void Order_just_above_the_limit_is_K2(WorkdayMode mode, double deliveryX, string neededMinutes)
    {
        var (scenario, order) = FarOrderScenario(deliveryX);
        var options = Options(mode);

        var plan = Solve(nameof(ExactDpPlanner), scenario, options);

        var entry = SingleInfeasible(plan, order, InfeasibilityReason.ExceedsWorkday);
        Assert.Contains(neededMinutes, entry.Message, StringComparison.Ordinal);
        Assert.Contains("540", entry.Message, StringComparison.Ordinal);
        Assert.All(plan.Routes, r => Assert.Empty(r.Stops));
        PassesValidator(plan, scenario, options);
    }
}
