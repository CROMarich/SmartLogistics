using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Core.Planning;

/// <summary>
/// Exact planner: enumerates every assignment of orders to trucks and finds the shortest route of every truck
/// with a dynamic programme (spec R24, R32, R38, R39).
/// <para>
/// <b>Algorithm.</b>
/// (0) Input checks (<see cref="PreparedScenario"/>): a globally invalid input throws <see cref="ArgumentException"/>
/// listing all errors (R37, T25). Each order is then classified in scenario order: invalid data is K4, a machine
/// heavier than every truck is K1, an order whose stand-alone route (base -> pickup -> delivery -> base) exceeds the
/// working day is K2; all other orders are "plannable". All legs are rounded first and kept in whole tenths of a
/// km (<c>int</c>, T24), so sums and comparisons are exact.
/// (1) Assignment: a plannable order may only go to a truck that can carry its machine (<see cref="Truck.CanCarry"/>,
/// R13). Every combination is enumerated (for two trucks: 2^k for the k orders both can carry; an order only one
/// truck can carry goes to that truck), in a fixed odometer order: the first order in the scenario is the most
/// significant digit and trucks are tried in scenario order.
/// (2) Per truck, the shortest route for its order set is computed by <see cref="RouteDp"/>: state = (current
/// location, base-3 status of every order: 0 not picked up, 1 on the truck, 2 delivered), value = minimum remaining
/// km to finish all orders and return to the base, memoised in a dictionary. Pickups are allowed only when kg and
/// machine count after the pickup fit the truck (R11, R12, checked after every action); the load is derived from the
/// status. The result for a (truck, order set) pair is cached, because many assignments share it.
/// (3) The working day is checked after the shortest route is known (see the proof below); an assignment where any
/// truck exceeds it is infeasible.
/// (4) The feasible assignment with the smallest total km wins; on equal km the first one in enumeration order
/// wins (strictly smaller replaces the best), and inside the DP the first transition in scenario order wins (R39).
/// (5) If no assignment of all plannable orders is feasible, the search is repeated with an extra option
/// "unassigned" (tried last) for every order and the objective becomes: most planned orders, then fewest km (R32,
/// T23). Unassigned plannable orders are K3 (they fit alone, otherwise they would be K2).
/// (6) The plan is checked with <see cref="PlanValidator.Validate"/>; any violation is a bug and throws
/// <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Why the working day can be checked after the shortest route (step 3).</b> For a fixed truck and a fixed set of
/// m orders every feasible route has exactly 2m actions (one pickup and one delivery per order, R4), so the action
/// time 2m x <see cref="PlanningOptions.MinutesPerAction"/> is the same for all routes of that set. Driving time is
/// km x 60 / speed (<see cref="PlanningOptions.DrivingMinutes"/>), a strictly increasing function of km for speed &gt; 0.
/// The limit only applies to the whole route (R29); there are no time windows or waiting (R34). Hence for both
/// modes the checked time T(route) = f(km(route)) + c with f increasing and c constant (c = 0 in DrivingOnly), so
/// km(r1) &lt;= km(r2) implies T(r1) &lt;= T(r2). If the shortest route violates the limit, every route of that set does,
/// and if any route of the set satisfies it, the shortest one does too. The shortest route is therefore also the
/// shortest feasible route, and the set is feasible exactly when its shortest route is.
/// </para>
/// <para>
/// <b>Complexity.</b> Let n be the number of plannable orders, T the number of trucks, c(o) the number of trucks
/// that can carry order o. Assignments: A = prod c(o) (2^k here), and prod (c(o) + 1) in the extended search.
/// One DP over m orders has at most 3^m x (2m + 1) states with O(m) work each. With the cache there are at most
/// T x 2^n distinct DP runs, so the total work is O(A x (T + n) + T x sum over m of C(n, m) x 3^m x m^2)
/// = O(A x (T + n) + T x 4^n x n^2) in the worst case; in practice capacity limits cut most states. Memory is
/// dominated by one DP memo (at most 3^m x (2m + 1) entries) and the route cache. The status of one truck is
/// encoded in a 64-bit integer, so at most <see cref="RouteDp.MaxOrders"/> orders fit on one truck and at most 63
/// plannable orders take part in the search (far beyond what the exponential search can finish anyway).
/// </para>
/// </summary>
public sealed class ExactDpPlanner : IRoutePlanner
{
    /// <summary>Order sets are cached as 64-bit masks over the plannable orders.</summary>
    private const int MaxPlannableOrders = 63;

    private const int Unassigned = -1;

    public ExactDpPlanner()
    {
    }

    /// <inheritdoc />
    public Plan Solve(Scenario scenario, PlanningOptions options) => SolveWithStatistics(scenario, options).Plan;

    /// <summary>Same as <see cref="Solve"/>, but also returns how much search was needed.</summary>
    public ExactDpResult SolveWithStatistics(Scenario scenario, PlanningOptions options)
    {
        var input = PreparedScenario.Create(scenario, options);
        if (input.Plannable.Count > MaxPlannableOrders)
        {
            throw new NotSupportedException(
                $"The exact planner supports at most {MaxPlannableOrders} plannable orders; got {input.Plannable.Count}.");
        }

        var search = new AssignmentSearch(input);
        var best = search.Run(allowUnassigned: false);
        var extended = best is null;
        best ??= search.Run(allowUnassigned: true)
            ?? throw new InvalidOperationException("The extended search found no plan; the empty plan is always feasible.");

        var plan = BuildPlan(input, search, best);
        var violations = PlanValidator.Validate(plan, scenario, options);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bug in {nameof(ExactDpPlanner)}: the plan breaks {violations.Count} rule(s):" + Environment.NewLine
                + string.Join(Environment.NewLine, violations.Select(v =>
                    $"- {v.RuleId}{(v.TruckId is null ? "" : $" truck {v.TruckId}")}{(v.StepIndex is null ? "" : $" step {v.StepIndex + 1}")}: {v.Message}")));
        }

        var statistics = new ExactDpStatistics(
            search.AssignmentsTried, search.AssignmentsEvaluated, extended, search.DpRuns, search.DpStates);
        return new ExactDpResult(plan, statistics);
    }

    private static Plan BuildPlan(PreparedScenario input, AssignmentSearch search, Assignment best)
    {
        var options = input.Options;
        var routes = new List<TruckRoute>(input.Trucks.Count);
        var totalKm10 = 0;
        for (var t = 0; t < input.Trucks.Count; t++)
        {
            var truck = input.Trucks[t];
            var result = search.Evaluate(t, best.Masks[t]);
            if (result.Orders.Count == 0)
            {
                routes.Add(new TruckRoute(truck, [], 0, 0, 0, 0)); // unused truck (R3, R36)
                continue;
            }

            var stops = new List<Stop>(result.Dp.Steps.Count);
            var location = input.BaseIndex;
            var cumulativeKm10 = 0;
            var loadKg = 0;
            var machines = 0;
            foreach (var step in result.Dp.Steps)
            {
                var order = result.Orders[step.OrderPosition];
                var pickup = step.Action == StopAction.Pickup;
                var next = pickup ? order.PickupIndex : order.DeliveryIndex;
                var leg10 = input.Legs10[location, next];
                cumulativeKm10 += leg10;
                loadKg += pickup ? order.Machine.WeightKg : -order.Machine.WeightKg;
                machines += pickup ? 1 : -1;
                var cumulativeKm = cumulativeKm10 / 10.0;
                var actionsDone = stops.Count + 1;
                stops.Add(new Stop(
                    input.Locations[next].Id,
                    step.Action,
                    order.Order.Id,
                    loadKg,
                    machines,
                    leg10 / 10.0,
                    cumulativeKm,
                    options.DrivingMinutes(cumulativeKm) + actionsDone * options.MinutesPerAction));
                location = next;
            }

            var routeKm10 = cumulativeKm10 + input.Legs10[location, input.BaseIndex];
            if (routeKm10 != result.Dp.Km10)
            {
                throw new InvalidOperationException(
                    $"Bug in {nameof(ExactDpPlanner)}: reconstructed route of truck {truck.Id} has {routeKm10 / 10.0} km, DP value {result.Dp.Km10 / 10.0} km.");
            }

            totalKm10 += routeKm10;
            var routeKm = routeKm10 / 10.0;
            routes.Add(new TruckRoute(
                truck,
                stops,
                routeKm,
                options.DrivingMinutes(routeKm),
                stops.Count * options.MinutesPerAction,
                result.Orders.Count));
        }

        var infeasible = input.Rejected.Select(r => (r.ScenarioIndex, r.Infeasible)).ToList();
        for (var i = 0; i < input.Plannable.Count; i++)
        {
            if (best.Choice[i] != Unassigned)
            {
                continue;
            }

            var order = input.Plannable[i];
            var trucks = string.Join(", ", order.CapableTrucks.Select(t => $"{input.Trucks[t].Id} {input.Trucks[t].CapacityKg} kg"));
            infeasible.Add((order.ScenarioIndex, new InfeasibleOrder(order.Order, InfeasibilityReason.DoesNotFitWithOtherOrders,
                $"K3: order {order.Order.Id} fits alone (machine {order.Machine.Id} {order.Machine.WeightKg} kg fits {trucks}; "
                + $"alone it needs {PreparedScenario.Format(order.StandaloneMinutes)} min <= {PreparedScenario.Format(options.MaxMinutes)} min, {input.ModeName}), "
                + $"but no plan with it fits the trucks' capacity and working day; the best plan executes {best.PlannedCount} of "
                + $"{input.Plannable.Count} plannable orders with {PreparedScenario.Format(best.Km10 / 10.0)} km.")));
        }

        return new Plan(
            routes,
            infeasible.OrderBy(x => x.ScenarioIndex).Select(x => x.Infeasible).ToList(),
            totalKm10 / 10.0);
    }

    /// <summary>The chosen truck per plannable order (<see cref="Unassigned"/> = not planned) and the per-truck order masks.</summary>
    private sealed record Assignment(int[] Choice, ulong[] Masks, int PlannedCount, int Km10);

    /// <summary>Shortest route of one truck for one order set, plus its working-day check.</summary>
    private sealed record TruckSetResult(IReadOnlyList<PlannableOrder> Orders, RouteDpResult Dp, bool FitsWorkday);

    /// <summary>Enumerates assignments in a fixed order and caches the DP per (truck, order set).</summary>
    private sealed class AssignmentSearch(PreparedScenario input)
    {
        private readonly Dictionary<(int Truck, ulong Mask), TruckSetResult> _cache = new();

        public long AssignmentsTried { get; private set; }

        public long AssignmentsEvaluated { get; private set; }

        public int DpRuns { get; private set; }

        public long DpStates { get; private set; }

        public TruckSetResult Evaluate(int truck, ulong mask)
        {
            if (_cache.TryGetValue((truck, mask), out var cached))
            {
                return cached;
            }

            var orders = new List<PlannableOrder>();
            for (var i = 0; i < input.Plannable.Count; i++)
            {
                if ((mask & (1UL << i)) != 0)
                {
                    orders.Add(input.Plannable[i]);
                }
            }

            var dp = RouteDp.Run(input.Trucks[truck], orders, input.Legs10, input.BaseIndex);
            if (orders.Count > 0)
            {
                DpRuns++;
                DpStates += dp.States;
            }

            // Step 3: the shortest route is also the fastest (see the class comment), so checking it is enough.
            var fits = input.FitsWorkday(input.WorkdayMinutes(dp.Km10, 2 * orders.Count));
            var result = new TruckSetResult(orders, dp, fits);
            _cache[(truck, mask)] = result;
            return result;
        }

        /// <summary>Best assignment, or null if none is feasible (only possible without <paramref name="allowUnassigned"/>).</summary>
        public Assignment? Run(bool allowUnassigned)
        {
            var n = input.Plannable.Count;
            var truckCount = input.Trucks.Count;
            var choices = new int[n][];
            for (var i = 0; i < n; i++)
            {
                var capable = input.Plannable[i].CapableTrucks;
                choices[i] = allowUnassigned ? [.. capable, Unassigned] : [.. capable];
            }

            var digit = new int[n];
            var masks = new ulong[truckCount];
            Assignment? best = null;
            while (true)
            {
                AssignmentsTried++;
                var planned = 0;
                for (var i = 0; i < n; i++)
                {
                    if (choices[i][digit[i]] != Unassigned)
                    {
                        planned++;
                    }
                }

                // With unassigned orders allowed, fewer planned orders can never win (R32), so skip the DP lookups.
                if (best is null || planned >= best.PlannedCount)
                {
                    best = EvaluateAssignment(digit, choices, masks, planned, best);
                }

                // Odometer increment: the last order changes fastest, the first order is the most significant digit.
                var p = n - 1;
                while (p >= 0)
                {
                    digit[p]++;
                    if (digit[p] < choices[p].Length)
                    {
                        break;
                    }

                    digit[p] = 0;
                    p--;
                }

                if (p < 0)
                {
                    return best;
                }
            }
        }

        private Assignment? EvaluateAssignment(int[] digit, int[][] choices, ulong[] masks, int planned, Assignment? best)
        {
            AssignmentsEvaluated++;
            Array.Clear(masks);
            for (var i = 0; i < digit.Length; i++)
            {
                var truck = choices[i][digit[i]];
                if (truck != Unassigned)
                {
                    masks[truck] |= 1UL << i;
                }
            }

            var km10 = 0;
            for (var t = 0; t < masks.Length; t++)
            {
                var result = Evaluate(t, masks[t]);
                if (!result.FitsWorkday)
                {
                    return best;
                }

                km10 = checked(km10 + result.Dp.Km10);
            }

            // Lexicographic objective (R32): more planned orders, then fewer km; strictly better replaces (R39).
            if (best is null || planned > best.PlannedCount || (planned == best.PlannedCount && km10 < best.Km10))
            {
                var choice = new int[digit.Length];
                for (var i = 0; i < digit.Length; i++)
                {
                    choice[i] = choices[i][digit[i]];
                }

                return new Assignment(choice, (ulong[])masks.Clone(), planned, km10);
            }

            return best;
        }
    }
}
