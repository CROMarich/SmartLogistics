using SmartLogistics.Core.Domain;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.PlanBuilder;

namespace SmartLogistics.Tests;

/// <summary>
/// Checks the test helper itself against values computed by hand from docs/spec.md 3.5, so that the
/// validator tests built on it rest on known-correct plans. These tests do not use the validator.
/// </summary>
public class PlanBuilderTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R17")]
    [Trait("Rule", "R18")]
    public void Builder_computes_hand_calculated_route_for_N01_on_A()
    {
        // A: L0 -> L1 (pickup N01) -> L3 (delivery N01) -> L0
        //   legs (spec 3.5): L0-L1 = 8.5, L1-L3 = 14.0, return L3-L0 = 19.2
        //   km = 8.5 + 14.0 + 19.2 = 41.7; driving = 41.7 min; actions = 2 x 20 = 40 min; total 81.7 min
        //   stop 0: cumulative km 8.5,  minutes 8.5 + 20 = 28.5, load 800 kg / 1 machine
        //   stop 1: cumulative km 22.5, minutes 28.5 + 14.0 + 20 = 62.5, load 0 kg / 0 machines
        var route = new PlanBuilder(TestScenarios.Task).BuildRoute("A", Pickup("N01"), Delivery("N01"));

        Assert.Equal(2, route.Stops.Count);
        AssertStop(route.Stops[0], "L1", StopAction.Pickup, "N01", 800, 1, 8.5, 8.5, 28.5);
        AssertStop(route.Stops[1], "L3", StopAction.Delivery, "N01", 0, 0, 14.0, 22.5, 62.5);
        Assert.Equal(41.7, route.TotalKm, Tolerance);
        Assert.Equal(41.7, route.DrivingMinutes, Tolerance);
        Assert.Equal(40.0, route.ActionMinutes, Tolerance);
        Assert.Equal(81.7, route.TotalMinutes, Tolerance);
        Assert.Equal(1, route.OrderCount);
    }

    [Fact]
    [Trait("Rule", "R16")]
    public void Builder_computes_hand_calculated_plan_A_N01_B_N06()
    {
        // A (N01): 8.5 + 14.0 + 19.2 = 41.7 km
        // B (N06): L0-L5 26.2 + L5-L7 12.5 + L7-L0 21.5 = 60.2 km (spec 3.7 row N06)
        // plan: 41.7 + 60.2 = 101.9 km
        var plan = new PlanBuilder(TestScenarios.TaskWithOrders("N01", "N06"))
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        Assert.Equal(["A", "B"], plan.Routes.Select(r => r.Truck.Id));
        var b = plan.Routes[1];
        AssertStop(b.Stops[0], "L5", StopAction.Pickup, "N06", 3900, 1, 26.2, 26.2, 46.2);
        AssertStop(b.Stops[1], "L7", StopAction.Delivery, "N06", 0, 0, 12.5, 38.7, 78.7);
        Assert.Equal(60.2, b.TotalKm, Tolerance);
        Assert.Equal(100.2, b.TotalMinutes, Tolerance);
        Assert.Equal(101.9, plan.TotalKm, Tolerance);
    }

    [Fact]
    [Trait("Rule", "R36")]
    public void Builder_adds_unused_truck_with_no_stops_and_zero_km()
    {
        var plan = new PlanBuilder(TestScenarios.TaskWithOrders("N01"))
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build();

        var b = Assert.Single(plan.Routes, r => r.Truck.Id == "B");
        Assert.Empty(b.Stops);
        Assert.Equal(0.0, b.TotalKm, Tolerance);
        Assert.Equal(0.0, b.TotalMinutes, Tolerance);
        Assert.Equal(0, b.OrderCount);
    }

    [Fact]
    [Trait("Rule", "R2")]
    public void Builder_without_return_leaves_return_leg_out_of_km()
    {
        // 8.5 + 14.0 = 22.5 km (return leg L3-L0 = 19.2 left out)
        var builder = new PlanBuilder(TestScenarios.Task);
        var route = builder.BuildRoute(builder.Truck("A"), [Pickup("N01"), Delivery("N01")], returnToBase: false);

        Assert.Equal(22.5, route.TotalKm, Tolerance);
        Assert.Equal(22.5, route.DrivingMinutes, Tolerance);
    }

    private static void AssertStop(
        Stop stop, string locationId, StopAction action, string orderId,
        int loadKg, int machines, double legKm, double cumulativeKm, double cumulativeMinutes)
    {
        Assert.Equal(locationId, stop.LocationId);
        Assert.Equal(action, stop.Action);
        Assert.Equal(orderId, stop.OrderId);
        Assert.Equal(loadKg, stop.LoadKgAfter);
        Assert.Equal(machines, stop.MachinesAfter);
        Assert.Equal(legKm, stop.LegKm, Tolerance);
        Assert.Equal(cumulativeKm, stop.CumulativeKm, Tolerance);
        Assert.Equal(cumulativeMinutes, stop.CumulativeMinutes, Tolerance);
    }
}
