using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Core.Planning;

/// <summary>
/// Exact planner: enumerates every assignment of orders to trucks and finds the shortest route of every truck
/// with a dynamic programme (spec R24, R32, R38, R39).
/// <para>
/// <b>Algorithm.</b>
/// (0) Input checks (<see cref="Feasibility.Check"/>): scenario data never throws (R37 as amended, T31, T32).
/// Unusable trucks (duplicate ID, capacity or max machines &lt;= 0) get no route; each order is classified in
/// scenario order: invalid data (unknown / duplicated / invalid machine or location, pickup == delivery, duplicate
/// order ID, machine reused, missing base) is K4, a machine heavier than every usable truck is K1, an order whose
/// stand-alone route (base -> pickup -> delivery -> base) exceeds the working day is K2; all other orders are
/// "plannable". Only invalid <see cref="PlanningOptions"/> and null arguments throw <see cref="ArgumentException"/>.
/// All legs are rounded first and kept in whole tenths of a km (<c>int</c>, T24), so sums and comparisons are exact.
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
/// (5) If no assignment of all plannable orders fits the working day, the search is repeated with an extra option
/// "unassigned" (tried last) for every order and the objective becomes: most planned orders, then fewest km (R32,
/// T23). "Most planned" is the same as "fewest omitted" (first 1, then 2, ...; R40), and among plans with the same
/// number of omitted orders the one with the fewest km wins (first in enumeration order on equal km). Omitted
/// orders are K3 with a message starting "Ne stane u radni dan" (they fit alone, otherwise they would be K2).
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
public sealed class ExactDpPlanner : IRoutePlanner, IPlannerCapabilities
{
    /// <summary>Order sets are cached as 64-bit masks over the plannable orders.</summary>
    private const int MaxPlannableOrders = 63;

    private const int Unassigned = -1;

    public ExactDpPlanner()
    {
    }

    /// <inheritdoc />
    public string DisplayName => "ExactDpPlanner (egzaktno)";

    /// <inheritdoc />
    public bool GuaranteesOptimum => true;

    /// <inheritdoc />
    public Plan Solve(Scenario scenario, PlanningOptions options) => SolveWithStatistics(scenario, options).Plan;

    /// <summary>Same as <see cref="Solve"/>, but also returns how much search was needed.</summary>
    public ExactDpResult SolveWithStatistics(Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        // Step 0: input checks never throw for scenario data; only invalid options do (R37, T31, T32).
        var report = Feasibility.Check(scenario, options);
        var input = PreparedScenario.Create(report, options);
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
        PlanAssembly.EnsureValid(plan, scenario, options, nameof(ExactDpPlanner));

        var statistics = new ExactDpStatistics(
            search.AssignmentsTried, search.AssignmentsEvaluated, extended, search.DpRuns, search.DpStates);
        return new ExactDpResult(plan, statistics) { Feasibility = report };
    }

    private static Plan BuildPlan(PreparedScenario input, AssignmentSearch search, Assignment best)
    {
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

            var actions = result.Dp.Steps
                .Select(step => new RouteAction(result.Orders[step.OrderPosition], step.Action))
                .ToList();
            var (route, routeKm10) = PlanAssembly.BuildRoute(input, truck, actions, result.Orders.Count);
            if (routeKm10 != result.Dp.Km10)
            {
                throw new InvalidOperationException(
                    $"Bug in {nameof(ExactDpPlanner)}: reconstructed route of truck {truck.Id} has {routeKm10 / 10.0} km, DP value {result.Dp.Km10 / 10.0} km.");
            }

            totalKm10 += routeKm10;
            routes.Add(route);
        }

        var infeasible = input.Report.Rejected.Select(r => (r.ScenarioIndex, r.Infeasible)).ToList();
        var omitted = best.Choice.Count(c => c == Unassigned);
        for (var i = 0; i < input.Plannable.Count; i++)
        {
            if (best.Choice[i] != Unassigned)
            {
                continue;
            }

            var order = input.Plannable[i];
            infeasible.Add((order.ScenarioIndex, new InfeasibleOrder(order.Order, InfeasibilityReason.DoesNotFitWithOtherOrders,
                DoesNotFitMessage(input, search, best, i, omitted))));
        }

        return new Plan(
            routes,
            infeasible.OrderBy(x => x.ScenarioIndex).Select(x => x.Infeasible).ToList(),
            totalKm10 / 10.0);
    }

    /// <summary>
    /// K3 message (R40) for the omitted plannable order <paramref name="i"/>. The "needed minutes" are those of each
    /// capable truck if the order were added to that truck's orders in the chosen plan. Every one of them exceeds
    /// the limit: otherwise a plan with one more order would exist, contradicting the maximum number of planned
    /// orders. Capacity is never the cause, because every plannable order fits each capable truck alone and orders
    /// can always be served one after another.
    /// </summary>
    private static string DoesNotFitMessage(PreparedScenario input, AssignmentSearch search, Assignment best, int i, int omitted)
    {
        var options = input.Options;
        var order = input.Plannable[i];
        var needed = new List<string>(order.CapableTrucks.Count);
        foreach (var t in order.CapableTrucks)
        {
            var mask = best.Masks[t] | (1UL << i);
            var truckId = input.Trucks[t].Id;
            if (ulong.PopCount(mask) > (ulong)RouteDp.MaxOrders)
            {
                needed.Add($"kamion {truckId} više od {RouteDp.MaxOrders} naloga");
                continue;
            }

            var result = search.Evaluate(t, mask);
            needed.Add($"kamion {truckId} {Feasibility.Format(input.WorkdayMinutes(result.Dp.Km10, 2 * result.Orders.Count))} min");
        }

        return $"Ne stane u radni dan ({Feasibility.ModeDescription(options)}, limit {Feasibility.Format(options.MaxMinutes)} min): "
            + $"nalog {order.Order.Id} sam traži {Feasibility.Format(order.StandaloneMinutes)} min, "
            + $"ali uz ostale isplanirane naloge trebao bi: {string.Join(", ", needed)}. "
            + $"Svi izvedivi nalozi zajedno ne stanu u radni dan, pa je izostavljen najmanji mogući broj naloga "
            + $"({omitted} od {input.Plannable.Count}); među takvim planovima ovaj ima najmanje km ({Feasibility.Format(best.Km10 / 10.0)} km).";
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
