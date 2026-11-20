using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Tests.Support;

/// <summary>Helpers for the phase-5 tests "the planner always returns a plan" (spec R23, R32, R33, R37, R40).</summary>
internal static partial class RobustPlanning
{
    /// <summary>Plan km is a sum of one-decimal legs; 0.05 separates neighbouring one-decimal values.</summary>
    public const double KmTolerance = 0.05;

    /// <summary>Floating-point guard for inclusive limits (spec R35).</summary>
    public const double LimitEpsilon = 1e-6;

    /// <summary>Reference optimum for the 10 task orders (CLAUDE.md): Total 200.1 km, DrivingOnly 182.2 km.</summary>
    public static double ReferenceKm(WorkdayMode mode) => mode == WorkdayMode.Total ? 200.1 : 182.2;

    public static PlanningOptions Options(WorkdayMode mode, double maxMinutes = 540) =>
        PlanningOptions.Default with { WorkdayMode = mode, MaxMinutes = maxMinutes };

    /// <summary>The task scenario with extra machines and orders appended (trucks and locations unchanged).</summary>
    public static Scenario TaskWith(IEnumerable<Machine> extraMachines, params TransportOrder[] extraOrders) =>
        TestScenarios.Task with
        {
            Machines = [.. TestScenarios.Task.Machines, .. extraMachines],
            Orders = [.. TestScenarios.Task.Orders, .. extraOrders],
        };

    /// <summary>Runs the planner (found by reflection) and fails with the exception text if it throws (R37: never fails).</summary>
    public static Plan SolveWithoutException(Scenario scenario, PlanningOptions options)
    {
        var planner = PlannerLocator.Create();
        Plan? plan = null;
        var exception = Record.Exception(() => plan = planner.Solve(scenario, options));
        Assert.True(exception is null, $"R37: the planner must not throw, but threw: {exception}");
        Assert.NotNull(plan);
        Assert.NotNull(plan!.Routes);
        Assert.NotNull(plan.InfeasibleOrders);
        return plan;
    }

    public static void PassesValidator(Plan plan, Scenario scenario, PlanningOptions options) =>
        ViolationAssert.Empty(PlanValidator.Validate(plan, scenario, options));

    /// <summary>Order IDs with at least one stop in the plan.</summary>
    public static IReadOnlyList<string> PlannedOrderIds(Plan plan) =>
        plan.Routes.SelectMany(r => r.Stops).Select(s => s.OrderId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// R23: every input order is either planned or infeasible, exactly once. Orders are matched by value
    /// (record equality), so that two orders with the same ID (R37 duplicate) are still told apart:
    /// the infeasible entries are removed from the input as a multiset, and the IDs of what remains must
    /// be exactly the planned IDs, each with one pickup and one delivery on one truck.
    /// </summary>
    public static void EachOrderExactlyOnce(Plan plan, Scenario scenario)
    {
        var remaining = scenario.Orders.ToList();
        foreach (var entry in plan.InfeasibleOrders)
        {
            Assert.True(remaining.Remove(entry.Order),
                $"R23: infeasible entry {entry.Order} is not an input order or is listed more than once.");
        }

        var expectedPlanned = remaining.Select(o => o.Id).Order(StringComparer.Ordinal).ToList();
        var stopsByOrder = plan.Routes
            .SelectMany(r => r.Stops.Select(s => (Truck: r.Truck.Id, Stop: s)))
            .GroupBy(x => x.Stop.OrderId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        Assert.Equal(expectedPlanned, stopsByOrder.Keys.Order(StringComparer.Ordinal).ToList());
        foreach (var (orderId, stops) in stopsByOrder)
        {
            Assert.True(stops.Count(s => s.Stop.Action == StopAction.Pickup) == 1, $"R4: order {orderId} must have exactly one pickup.");
            Assert.True(stops.Count(s => s.Stop.Action == StopAction.Delivery) == 1, $"R4: order {orderId} must have exactly one delivery.");
            Assert.True(stops.Select(s => s.Truck).Distinct(StringComparer.Ordinal).Count() == 1, $"R5: order {orderId} must be on one truck.");
        }
    }

    /// <summary>The only infeasible entry is <paramref name="order"/> with <paramref name="reason"/> and a non-empty message.</summary>
    public static InfeasibleOrder SingleInfeasible(Plan plan, TransportOrder order, InfeasibilityReason reason)
    {
        var entry = Assert.Single(plan.InfeasibleOrders);
        Assert.Equal(order, entry.Order);
        Assert.Equal(reason, entry.Reason);
        Assert.False(string.IsNullOrWhiteSpace(entry.Message), $"R33: infeasible order {order.Id} needs a message.");
        return entry;
    }

    /// <summary>
    /// Removes thousands separators between digits ("11 000", "11.000", "11,000", no-break spaces) so that
    /// a number can be found in a message regardless of number formatting.
    /// </summary>
    public static string WithoutThousandsSeparators(string message) => ThousandsSeparator().Replace(message, "");

    [GeneratedRegex(@"(?<=\d)[ .,  ](?=\d{3}(?!\d))")]
    private static partial Regex ThousandsSeparator();

    /// <summary>
    /// Workday minutes of a route recomputed from its stops and the scenario (R14, R17, R18, R20/R21),
    /// independent of the numbers reported in the route.
    /// </summary>
    public static double RecomputedWorkdayMinutes(TruckRoute route, Scenario scenario, PlanningOptions options)
    {
        if (route.Stops.Count == 0)
        {
            return 0;
        }

        Location Find(string id) => scenario.Locations.Single(l => l.Id == id);
        var previous = Find(scenario.BaseLocationId);
        var km = 0.0;
        foreach (var stop in route.Stops)
        {
            var location = Find(stop.LocationId);
            km += Distance.Leg(previous, location);
            previous = location;
        }

        km += Distance.Leg(previous, Find(scenario.BaseLocationId));
        var minutes = options.DrivingMinutes(km);
        return options.WorkdayMode == WorkdayMode.Total ? minutes + route.Stops.Count * options.MinutesPerAction : minutes;
    }

    /// <summary>Everything observable about a plan, as text, for determinism checks (R39).</summary>
    public static string Signature(Plan plan)
    {
        var text = new StringBuilder();
        foreach (var route in plan.Routes)
        {
            text.Append(route.Truck.Id).Append(':');
            foreach (var stop in route.Stops)
            {
                text.Append(' ').Append(stop.LocationId).Append('/').Append(stop.Action).Append('/').Append(stop.OrderId);
            }

            text.Append(" km=").Append(route.TotalKm.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        }

        foreach (var entry in plan.InfeasibleOrders)
        {
            text.Append("X ").Append(entry.Order).Append(' ').Append(entry.Reason).Append(' ').Append(entry.Message).AppendLine();
        }

        return text.Append("total=").Append(plan.TotalKm.ToString("0.###", CultureInfo.InvariantCulture)).ToString();
    }
}
