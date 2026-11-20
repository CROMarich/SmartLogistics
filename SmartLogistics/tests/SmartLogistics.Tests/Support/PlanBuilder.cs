using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Tests.Support;

/// <summary>One action of a hand-built route. <paramref name="LocationId"/> overrides the order's location (for R8 tests).</summary>
internal sealed record PlanStep(string OrderId, StopAction Action, string? LocationId = null)
{
    public PlanStep At(string locationId) => this with { LocationId = locationId };
}

/// <summary>
/// Builds plans by hand for validator tests. From a sequence of (order, action) per truck it computes
/// correct <see cref="Stop"/>s and <see cref="TruckRoute"/>s straight from the rules:
/// legs via <see cref="Distance.Leg"/> (R14), driving minutes via <see cref="PlanningOptions.DrivingMinutes"/> (R17),
/// <see cref="PlanningOptions.MinutesPerAction"/> per action (R18), load from machine weights (R10).
/// A test that wants a broken plan builds the correct route and then changes one reported value
/// with a <c>with</c> expression or <see cref="RouteEdits.WithStop"/>.
/// </summary>
internal sealed class PlanBuilder
{
    private readonly Scenario _scenario;
    private readonly PlanningOptions _options;
    private readonly List<TruckRoute> _routes = [];
    private readonly List<InfeasibleOrder> _infeasible = [];
    private bool _addUnusedTrucks = true;

    public PlanBuilder(Scenario scenario, PlanningOptions? options = null)
    {
        _scenario = scenario;
        _options = options ?? PlanningOptions.Default;
    }

    public static PlanStep Pickup(string orderId) => new(orderId, StopAction.Pickup);

    public static PlanStep Delivery(string orderId) => new(orderId, StopAction.Delivery);

    /// <summary>Adds a correctly computed route for the scenario truck.</summary>
    public PlanBuilder Route(string truckId, params PlanStep[] steps) => Route(BuildRoute(truckId, steps));

    /// <summary>Adds an already built (possibly deliberately broken) route as is.</summary>
    public PlanBuilder Route(TruckRoute route)
    {
        _routes.Add(route);
        return this;
    }

    public PlanBuilder Infeasible(
        string orderId,
        string message = "Test: order does not fit into the plan with the other orders.",
        InfeasibilityReason reason = InfeasibilityReason.DoesNotFitWithOtherOrders)
    {
        _infeasible.Add(new InfeasibleOrder(Order(orderId), reason, message));
        return this;
    }

    /// <summary>By default <see cref="Build"/> adds an empty route for every scenario truck without a route (R3, R36).</summary>
    public PlanBuilder WithoutAutomaticUnusedTrucks()
    {
        _addUnusedTrucks = false;
        return this;
    }

    /// <summary>Plan with routes in scenario truck order first, then extra routes; plan km = sum of route km (R16).</summary>
    public Plan Build()
    {
        var routes = new List<TruckRoute>(_routes);
        if (_addUnusedTrucks)
        {
            foreach (var truck in _scenario.Trucks.Where(t => routes.All(r => r.Truck.Id != t.Id)))
            {
                routes.Add(UnusedRoute(truck));
            }
        }

        var ordered = routes
            .Select((route, index) => (route, index))
            .OrderBy(x => TruckOrder(x.route.Truck.Id))
            .ThenBy(x => x.index)
            .Select(x => x.route)
            .ToList();

        return new Plan(ordered, _infeasible.ToList(), ordered.Sum(r => r.TotalKm));
    }

    public TruckRoute BuildRoute(string truckId, params PlanStep[] steps) => BuildRoute(Truck(truckId), steps);

    /// <summary>
    /// Computes a route the way the spec defines it. With <paramref name="returnToBase"/> = false the
    /// final leg back to the base is left out of <see cref="TruckRoute.TotalKm"/> and
    /// <see cref="TruckRoute.DrivingMinutes"/> (used to build an R2 violation).
    /// </summary>
    public TruckRoute BuildRoute(Truck truck, IReadOnlyList<PlanStep> steps, bool returnToBase = true)
    {
        if (steps.Count == 0)
        {
            return UnusedRoute(truck);
        }

        var stops = new List<Stop>();
        var previous = _scenario.BaseLocationId;
        double km = 0, drivingMinutes = 0, elapsedMinutes = 0;
        int loadKg = 0, machines = 0;

        foreach (var step in steps)
        {
            var order = Order(step.OrderId);
            var locationId = step.LocationId
                ?? (step.Action == StopAction.Pickup ? order.PickupLocationId : order.DeliveryLocationId);
            var weight = _scenario.Machines.Single(m => m.Id == order.MachineId).WeightKg;

            var leg = Distance.Leg(Location(previous), Location(locationId));
            var legMinutes = _options.DrivingMinutes(leg);
            km += leg;
            drivingMinutes += legMinutes;
            elapsedMinutes += legMinutes + _options.MinutesPerAction;

            var sign = step.Action == StopAction.Pickup ? 1 : -1;
            loadKg += sign * weight;
            machines += sign;

            stops.Add(new Stop(locationId, step.Action, order.Id, loadKg, machines, leg, km, elapsedMinutes));
            previous = locationId;
        }

        if (returnToBase)
        {
            var returnLeg = Distance.Leg(Location(previous), Location(_scenario.BaseLocationId));
            km += returnLeg;
            drivingMinutes += _options.DrivingMinutes(returnLeg);
        }

        var orderCount = steps.Select(s => s.OrderId).Distinct().Count();
        return new TruckRoute(truck, stops, km, drivingMinutes, stops.Count * _options.MinutesPerAction, orderCount);
    }

    public static TruckRoute UnusedRoute(Truck truck) => new(truck, [], 0, 0, 0, 0);

    public Truck Truck(string truckId) => _scenario.Trucks.Single(t => t.Id == truckId);

    private TransportOrder Order(string orderId) => _scenario.Orders.Single(o => o.Id == orderId);

    private Location Location(string locationId) => _scenario.Locations.Single(l => l.Id == locationId);

    private int TruckOrder(string truckId)
    {
        for (var i = 0; i < _scenario.Trucks.Count; i++)
        {
            if (_scenario.Trucks[i].Id == truckId)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}

internal static class RouteEdits
{
    /// <summary>Returns a copy of the route with one stop replaced (to break a single reported value).</summary>
    public static TruckRoute WithStop(this TruckRoute route, int index, Func<Stop, Stop> change)
    {
        var stops = route.Stops.ToList();
        stops[index] = change(stops[index]);
        return route with { Stops = stops };
    }

    /// <summary>Returns a copy of the route with every stop changed.</summary>
    public static TruckRoute WithAllStops(this TruckRoute route, Func<Stop, Stop> change) =>
        route with { Stops = route.Stops.Select(change).ToList() };
}
