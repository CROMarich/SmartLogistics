using System.Globalization;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Tests.Support;

/// <summary>
/// A deliberately simple, exact reference planner written only from docs/spec.md and the domain types
/// (it shares nothing with the production planner except <see cref="Distance"/> and the records).
///
/// One truck, one set of orders: Dijkstra over states (current location, status of every order), where the
/// status of an order is "not picked up", "on the truck" or "delivered". The only moves are actions (R31: no
/// empty visits): a pickup when weight and machine count after it still fit (R11, R12), and the delivery of an
/// order that is on the truck (R5, R6). Once every order is delivered the truck drives back to the base (R2).
/// Edge weights are rounded legs (R14) in whole tenths of a km, so sums and comparisons are exact.
///
/// Several trucks: every assignment of each order to a truck that can carry it, or to nobody, is tried.
/// For a fixed truck and order set the number of actions is fixed, so the shortest route is also the
/// shortest workday in both modes – checking only the shortest route against the limit is exact (R20, R21, R29).
/// Objective (R32): most planned orders, then least km. Orders no truck can carry (K1) are never counted.
///
/// Expects valid input (no K4 data); it is a test reference, not a product.
/// </summary>
internal static class ReferenceOracle
{
    private const int NotPickedUp = 0;
    private const int OnTruck = 1;
    private const int Delivered = 2;

    /// <summary>Inclusive limit with a floating-point guard (spec R35).</summary>
    private const double LimitEpsilon = 1e-6;

    public sealed record TruckResult(Truck Truck, IReadOnlyList<TransportOrder> Orders, int KmTenths, double WorkdayMinutes);

    public sealed record Result(int PlannedOrders, int KmTenths, IReadOnlyList<TruckResult> Trucks)
    {
        public double TotalKm => KmTenths / 10.0;

        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"{PlannedOrders} orders, {TotalKm:F1} km; ")
            + string.Join("; ", Trucks.Select(t => string.Create(CultureInfo.InvariantCulture,
                $"{t.Truck.Id}: [{string.Join(", ", t.Orders.Select(o => o.Id))}] {t.KmTenths / 10.0:F1} km {t.WorkdayMinutes:F1} min")));
    }

    /// <summary>Best plan value over all assignments of orders to trucks (or to nobody).</summary>
    public static Result Solve(Scenario scenario, PlanningOptions options)
    {
        var orders = scenario.Orders;
        var trucks = scenario.Trucks;
        var machines = scenario.Machines.ToDictionary(m => m.Id, StringComparer.Ordinal);

        // choices[i] = truck indices that may carry order i (R13), followed by -1 = "not planned".
        var choices = orders
            .Select(o => trucks
                .Select((truck, index) => (truck, index))
                .Where(x => x.truck.CanCarry(machines[o.MachineId]))
                .Select(x => x.index)
                .Append(-1)
                .ToArray())
            .ToArray();

        // Shortest feasible route per (truck, set of orders as bit mask); computed when first needed.
        // A plain array, not a dictionary: index = mask, value = km in tenths, or Infeasible.
        const int NotComputed = -2;
        const int Infeasible = -1;
        var routeTenths = trucks.Select(_ => Enumerable.Repeat(NotComputed, 1 << orders.Count).ToArray()).ToArray();

        Result? best = null;
        var assignment = new int[orders.Count]; // index into choices[i]

        while (true)
        {
            var masks = new int[trucks.Count];
            var planned = 0;
            for (var i = 0; i < orders.Count; i++)
            {
                var truckIndex = choices[i][assignment[i]];
                if (truckIndex >= 0)
                {
                    masks[truckIndex] |= 1 << i;
                    planned++;
                }
            }

            var feasible = true;
            var totalTenths = 0;
            for (var t = 0; t < trucks.Count && feasible; t++)
            {
                if (routeTenths[t][masks[t]] == NotComputed)
                {
                    var subset = OrdersInMask(orders, masks[t]);
                    var tenths = ShortestRouteTenths(scenario, trucks[t], subset);
                    routeTenths[t][masks[t]] = tenths is int km && FitsWorkday(km, subset.Count, options) ? km : Infeasible;
                }

                feasible = routeTenths[t][masks[t]] != Infeasible;
                if (feasible)
                {
                    totalTenths += routeTenths[t][masks[t]];
                }
            }

            if (feasible && (best is null || planned > best.PlannedOrders || (planned == best.PlannedOrders && totalTenths < best.KmTenths)))
            {
                var truckResults = trucks
                    .Select((truck, t) => new TruckResult(
                        truck,
                        OrdersInMask(orders, masks[t]),
                        routeTenths[t][masks[t]],
                        WorkdayMinutes(routeTenths[t][masks[t]], OrdersInMask(orders, masks[t]).Count, options)))
                    .ToList();
                best = new Result(planned, totalTenths, truckResults);
            }

            // Next assignment (odometer over the choices of every order).
            var position = 0;
            while (position < orders.Count && ++assignment[position] == choices[position].Length)
            {
                assignment[position] = 0;
                position++;
            }

            if (position == orders.Count)
            {
                break;
            }
        }

        // The all-"not planned" assignment is always feasible (0 km), so best is never null.
        return best!;
    }

    /// <summary>
    /// Shortest route in tenths of a km: base -> actions for every order -> base, respecting capacity after
    /// every action. Null when no order of the actions keeps the truck within its limits.
    /// </summary>
    public static int? ShortestRouteTenths(Scenario scenario, Truck truck, IReadOnlyList<TransportOrder> orders)
    {
        var locations = scenario.Locations;
        int LocationIndex(string id) => locations.Select((l, i) => (l, i)).Single(x => x.l.Id == id).i;

        var baseIndex = LocationIndex(scenario.BaseLocationId);
        var pickup = orders.Select(o => LocationIndex(o.PickupLocationId)).ToArray();
        var delivery = orders.Select(o => LocationIndex(o.DeliveryLocationId)).ToArray();
        var weight = orders.Select(o => scenario.Machines.Single(m => m.Id == o.MachineId).WeightKg).ToArray();

        var legTenths = new int[locations.Count, locations.Count];
        for (var a = 0; a < locations.Count; a++)
        {
            for (var b = 0; b < locations.Count; b++)
            {
                legTenths[a, b] = (int)Math.Round(Distance.Leg(locations[a], locations[b]) * 10, MidpointRounding.AwayFromZero);
            }
        }

        // Status of all orders as a base-3 number: digit i = status of order i.
        var power = new int[orders.Count + 1];
        power[0] = 1;
        for (var i = 1; i <= orders.Count; i++)
        {
            power[i] = power[i - 1] * 3;
        }

        var statusCount = power[orders.Count];
        var allDelivered = statusCount - 1; // every digit is 2 = Delivered

        // State = status * locationCount + location; one extra state is the goal "back in the base".
        var goal = statusCount * locations.Count;
        var distance = new int[goal + 1];
        Array.Fill(distance, int.MaxValue);
        var done = new bool[goal + 1];

        var queue = new PriorityQueue<int, int>();
        var start = 0 * locations.Count + baseIndex; // all orders "not picked up", truck in the base
        distance[start] = 0;
        queue.Enqueue(start, 0);

        void Relax(int state, int newDistance)
        {
            if (newDistance < distance[state])
            {
                distance[state] = newDistance;
                queue.Enqueue(state, newDistance);
            }
        }

        while (queue.TryDequeue(out var state, out var dist))
        {
            if (done[state])
            {
                continue;
            }

            done[state] = true;
            if (state == goal)
            {
                return dist;
            }

            var status = state / locations.Count;
            var here = state % locations.Count;

            if (status == allDelivered)
            {
                Relax(goal, dist + legTenths[here, baseIndex]);
                continue;
            }

            var loadKg = 0;
            var loadMachines = 0;
            for (var i = 0; i < orders.Count; i++)
            {
                if (Digit(status, i, power) == OnTruck)
                {
                    loadKg += weight[i];
                    loadMachines++;
                }
            }

            for (var i = 0; i < orders.Count; i++)
            {
                var digit = Digit(status, i, power);

                // Both moves raise digit i by one: NotPickedUp -> OnTruck, OnTruck -> Delivered.
                if (digit == NotPickedUp && loadKg + weight[i] <= truck.CapacityKg && loadMachines + 1 <= truck.MaxMachines)
                {
                    var next = (status + power[i]) * locations.Count + pickup[i];
                    Relax(next, dist + legTenths[here, pickup[i]]);
                }
                else if (digit == OnTruck)
                {
                    var next = (status + power[i]) * locations.Count + delivery[i];
                    Relax(next, dist + legTenths[here, delivery[i]]);
                }
            }
        }

        return null;
    }

    /// <summary>Workday minutes of a route with the given km and number of orders (R17, R18, R20, R21, R29).</summary>
    public static double WorkdayMinutes(int kmTenths, int orderCount, PlanningOptions options)
    {
        var driving = options.DrivingMinutes(kmTenths / 10.0);
        return options.WorkdayMode == WorkdayMode.Total
            ? driving + 2 * orderCount * options.MinutesPerAction
            : driving;
    }

    private static bool FitsWorkday(int kmTenths, int orderCount, PlanningOptions options) =>
        WorkdayMinutes(kmTenths, orderCount, options) <= options.MaxMinutes + LimitEpsilon;

    private static int Digit(int status, int i, int[] power) => status / power[i] % 3;

    private static List<TransportOrder> OrdersInMask(IReadOnlyList<TransportOrder> orders, int mask) =>
        orders.Where((_, i) => (mask & (1 << i)) != 0).ToList();
}
