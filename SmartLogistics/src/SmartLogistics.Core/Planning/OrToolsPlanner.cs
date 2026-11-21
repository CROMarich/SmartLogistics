using Google.OrTools.ConstraintSolver;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Core.Planning;

/// <summary>
/// Heuristic planner built on the Google OR-Tools routing library (pickup and delivery VRP with capacities and a
/// working day). The optimum is NOT guaranteed (spec R38): see <see cref="GuaranteesOptimum"/>.
/// <para>
/// <b>Input.</b> Same checks as the exact planner (<see cref="Feasibility.Check"/>, R37, R40): scenario data never
/// throws; K4 / K1 / K2 orders are rejected before the model is built and only "plannable" orders become nodes.
/// Legs are rounded first and kept in whole tenths of a km (<see cref="PreparedScenario.Legs10"/>, R14, T24), so
/// OR-Tools works with exactly the same integer legs as <see cref="Distance.Leg"/> and <see cref="ExactDpPlanner"/>.
/// </para>
/// <para>
/// <b>Model.</b>
/// Nodes: node 0 is the base (start and end of every vehicle); plannable order i (scenario order) has a pickup node
/// 2i + 1 and a delivery node 2i + 2. Every node is exactly one action, so a route never has an empty intermediate
/// visit (R31) and two actions at the same location are two nodes with a 0 km leg between them (R15, R18).
/// Vehicles: one per usable truck, in scenario order; start = end = base (R2); an empty vehicle is an unused truck (R3).
/// Arc cost: leg in tenths of a km (objective = total km, R16, R24).
/// Pairs: <c>AddPickupAndDelivery(pickup, delivery)</c> plus the constraints vehicle(pickup) == vehicle(delivery) and
/// active(pickup) == active(delivery) (R4, R5, R7) and position(pickup) &lt; position(delivery) (R6).
/// Allowed vehicles: trucks that cannot carry the machine are removed from the vehicle variable of both nodes (R13).
/// Dimensions (all with slack 0 and the start cumul fixed to 0):
/// <c>Weight</c> – unary transit +kg at a pickup, -kg at a delivery, vehicle capacity = <see cref="Truck.CapacityKg"/>;
/// <c>Count</c> – +1 / -1, vehicle capacity = <see cref="Truck.MaxMachines"/>. With a unary transit the cumul of the
/// node after an action is the load after that action, and the end node is constrained too, so the load is checked
/// after every action (R9–R12);
/// <c>Time</c> – tenths of a minute: transit(i, j) = driving time of the leg (km x 60 / <see cref="PlanningOptions.SpeedKmh"/>)
/// plus, in <see cref="WorkdayMode.Total"/>, <see cref="PlanningOptions.MinutesPerAction"/> for the action at node i
/// (0 at the base); vehicle capacity = <see cref="PlanningOptions.MaxMinutes"/> (R17–R21, R29). Driving and action
/// times are rounded UP to whole tenths and the limit DOWN, so an integer-feasible route is always feasible in exact
/// arithmetic (with the default options all values are whole tenths and the model is exact);
/// <c>Position</c> – +1 per arc, used only for the strict pickup-before-delivery constraint (legs of 0 km would make
/// a km-based precedence non-strict).
/// Disjunctions: every node is in its own disjunction with penalty P = (2n + T) x (longest leg in tenths) + 1, so
/// omitting one order (2P) costs more than any possible difference in km; orders are dropped only when the solver
/// cannot place them (R32).
/// </para>
/// <para>
/// <b>Search (portfolio of deterministic runs).</b> Every run is <c>PATH_CHEAPEST_ARC</c> + <c>GUIDED_LOCAL_SEARCH</c>
/// and stops after <see cref="SolutionLimit"/> solutions (deterministic, R39); <see cref="TimeLimitSeconds"/> is the
/// upper bound of all runs together (a run stopped by it is reported in <see cref="OrToolsRun.StoppedByTimeLimit"/>).
/// The runs differ only in the GLS lambda (<see cref="GuidedLocalSearchLambdas"/>): with the default 0.1 the search
/// gets stuck in local optima where one truck is unused and an order is omitted (on the task data in
/// <see cref="WorkdayMode.Total"/>: 9 of 10 orders), the stronger diversification 1.0 escapes them but is worse in
/// other cases. Each run's solution is repaired (below), and the best one wins lexicographically: more planned
/// orders, then fewer km, then the earlier run.
/// </para>
/// <para>
/// <b>Repair and result.</b> Omitted orders are re-inserted greedily (exact arithmetic, every insertion position on
/// every capable truck, cheapest feasible insertion in km); orders that still do not fit are K3 "Ne stane u radni dan"
/// with the minutes of the best insertion per capable truck. The heuristic does not guarantee the SMALLEST number of
/// omitted orders nor the fewest km. The plan is built by <see cref="PlanAssembly"/> and checked with
/// <see cref="PlanValidator"/>; any violation is a bug and throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Complexity.</b> Building the model is O(n^2) for n plannable orders (2n + 1 nodes, dense transit matrices).
/// The search is bounded by R x <see cref="SolutionLimit"/> solutions (R runs) and the time limit; every local-search
/// move is evaluated incrementally by the routing filters (roughly O(n + T)). The repair step is O(k x T x L^3) for k omitted orders and routes of at most L actions.
/// Memory O(n^2 + T) for the matrices.
/// </para>
/// </summary>
public sealed class OrToolsPlanner : IRoutePlanner, IPlannerCapabilities
{
    /// <summary>Upper bound (wall clock) of the whole search, shared by all portfolio runs; requested by the technical lead.</summary>
    public const int TimeLimitSeconds = 5;

    /// <summary>
    /// Number of solutions after which one guided-local-search run stops. A time limit alone is not deterministic
    /// (the number of iterations depends on the machine and its load); a solution limit is (spec R39).
    /// </summary>
    public const long SolutionLimit = 1000;

    private const string WeightDimension = "Weight";
    private const string CountDimension = "Count";
    private const string TimeDimension = "Time";
    private const string PositionDimension = "Position";

    /// <summary>
    /// Guided-local-search lambda coefficients of the portfolio runs, in run order. 0.1 is the OR-Tools default;
    /// 1.0 diversifies more strongly and escapes local optima in which an omitted order would need a truck that the
    /// default run left unused. On equal results the earlier run wins.
    /// </summary>
    private static readonly double[] Lambdas = [0.1, 1.0];

    private readonly long _solutionLimit;
    private readonly TimeSpan _timeLimit;

    public OrToolsPlanner()
        : this(SolutionLimit, TimeSpan.FromSeconds(TimeLimitSeconds))
    {
    }

    /// <summary>Search limits for experiments; <paramref name="solutionLimit"/> &lt;= 0 means "time limit only".</summary>
    internal OrToolsPlanner(long solutionLimit, TimeSpan timeLimit)
    {
        _solutionLimit = solutionLimit;
        _timeLimit = timeLimit;
    }

    /// <summary>Guided-local-search lambda coefficients of the portfolio runs, in run order.</summary>
    public static IReadOnlyList<double> GuidedLocalSearchLambdas { get; } = Array.AsReadOnly(Lambdas);

    /// <inheritdoc />
    public string DisplayName => "OrToolsPlanner (heuristika, optimum nije zajamčen)";

    /// <inheritdoc />
    public bool GuaranteesOptimum => false;

    /// <inheritdoc />
    public Plan Solve(Scenario scenario, PlanningOptions options) => SolveWithStatistics(scenario, options).Plan;

    /// <summary>Same as <see cref="Solve"/>, but also returns how every solver run ended.</summary>
    public OrToolsResult SolveWithStatistics(Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        var report = Feasibility.Check(scenario, options);
        var input = PreparedScenario.Create(report, options);
        var orderIndex = new Dictionary<PlannableOrder, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < input.Plannable.Count; i++)
        {
            orderIndex[input.Plannable[i]] = i;
        }

        var budget = System.Diagnostics.Stopwatch.StartNew();
        var runs = new List<OrToolsRun>();
        var bestRoutes = EmptyRoutes(input);
        var bestPlanned = new bool[input.Plannable.Count];
        var bestKm10 = 0;
        var bestRun = -1;
        for (var k = 0; k < Lambdas.Length && input.Plannable.Count > 0; k++)
        {
            var remaining = _timeLimit - budget.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break; // budget used up (only under extreme load): the remaining runs are skipped
            }

            var routes = EmptyRoutes(input);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (status, objective) = RunSolver(input, routes, Lambdas[k], remaining);
            var solverMs = watch.Elapsed.TotalMilliseconds;

            var planned = new bool[input.Plannable.Count];
            foreach (var action in routes.SelectMany(r => r))
            {
                planned[orderIndex[action.Order]] = true;
            }

            var omittedBySolver = planned.Count(p => !p);
            var inserted = Repair(input, routes, planned);
            var plannedCount = planned.Count(p => p);
            var km10 = routes.Sum(r => RouteKm10(input, r));
            runs.Add(new OrToolsRun(
                Lambdas[k], status, objective, omittedBySolver, inserted, plannedCount, km10 / 10.0, solverMs,
                StoppedByTimeLimit: solverMs >= remaining.TotalMilliseconds * 0.98));

            // Lexicographic choice (R32): more planned orders, then fewer km; on ties the earlier run wins (R39).
            var bestCount = bestPlanned.Count(p => p);
            if (bestRun < 0 || plannedCount > bestCount || (plannedCount == bestCount && km10 < bestKm10))
            {
                bestRoutes = routes;
                bestPlanned = planned;
                bestKm10 = km10;
                bestRun = k;
            }
        }

        if (bestRun < 0 && input.Plannable.Count > 0)
        {
            Repair(input, bestRoutes, bestPlanned); // no solver run at all (zero time budget): greedy insertion only
        }

        var plan = BuildPlan(input, bestRoutes, bestPlanned);
        PlanAssembly.EnsureValid(plan, scenario, options, nameof(OrToolsPlanner));

        var statistics = new OrToolsStatistics(runs, bestRun, budget.Elapsed.TotalMilliseconds, _solutionLimit, _timeLimit.TotalSeconds);
        return new OrToolsResult(plan, statistics) { Feasibility = report };
    }

    private static List<RouteAction>[] EmptyRoutes(PreparedScenario input)
    {
        var routes = new List<RouteAction>[input.Trucks.Count];
        for (var t = 0; t < routes.Length; t++)
        {
            routes[t] = [];
        }

        return routes;
    }

    /// <summary>Builds and solves the routing model; fills <paramref name="routes"/> with the solver's routes.</summary>
    private (string Status, long Objective) RunSolver(PreparedScenario input, List<RouteAction>[] routes, double lambda, TimeSpan timeLimit)
    {
        var options = input.Options;
        var orders = input.Plannable;
        var trucks = input.Trucks;
        var nodeCount = 2 * orders.Count + 1;

        // Node -> location index (base for node 0), demand per node.
        var location = new int[nodeCount];
        var weight = new long[nodeCount];
        var count = new long[nodeCount];
        location[0] = input.BaseIndex;
        for (var i = 0; i < orders.Count; i++)
        {
            location[PickupNode(i)] = orders[i].PickupIndex;
            location[DeliveryNode(i)] = orders[i].DeliveryIndex;
            weight[PickupNode(i)] = orders[i].Machine.WeightKg;
            weight[DeliveryNode(i)] = -orders[i].Machine.WeightKg;
            count[PickupNode(i)] = 1;
            count[DeliveryNode(i)] = -1;
        }

        // Time in tenths of a minute: rounded up per leg / action, limit rounded down (conservative, exact by default).
        var horizon10 = (long)Math.Floor(options.MaxMinutes * 10 + 1e-6);
        var service10 = options.WorkdayMode == WorkdayMode.Total ? CeilTenths(options.MinutesPerAction * 10, horizon10) : 0;
        var minutesPerKm10 = 60.0 / options.SpeedKmh; // driving minutes per km = tenths of a minute per tenth of a km

        var distance = new long[nodeCount][];
        var time = new long[nodeCount][];
        var position = new long[nodeCount][];
        long longestLeg10 = 0;
        for (var a = 0; a < nodeCount; a++)
        {
            distance[a] = new long[nodeCount];
            time[a] = new long[nodeCount];
            position[a] = new long[nodeCount];
            for (var b = 0; b < nodeCount; b++)
            {
                var leg10 = input.Legs10[location[a], location[b]];
                longestLeg10 = Math.Max(longestLeg10, leg10);
                distance[a][b] = leg10;
                var drive10 = leg10 == 0 ? 0 : CeilTenths(leg10 * minutesPerKm10, horizon10);
                time[a][b] = Math.Min(horizon10 + 1, drive10 + (a == 0 ? 0 : service10));
                position[a][b] = 1;
            }
        }

        using var manager = new RoutingIndexManager(nodeCount, trucks.Count, 0);
        using var routing = new RoutingModel(manager);

        var distanceCallback = routing.RegisterTransitMatrix(distance);
        routing.SetArcCostEvaluatorOfAllVehicles(distanceCallback);

        routing.AddDimensionWithVehicleCapacity(
            routing.RegisterUnaryTransitVector(weight), 0, trucks.Select(t => (long)t.CapacityKg).ToArray(), true, WeightDimension);
        routing.AddDimensionWithVehicleCapacity(
            routing.RegisterUnaryTransitVector(count), 0, trucks.Select(t => (long)t.MaxMachines).ToArray(), true, CountDimension);
        routing.AddDimensionWithVehicleCapacity(
            routing.RegisterTransitMatrix(time), 0, trucks.Select(_ => horizon10).ToArray(), true, TimeDimension);
        routing.AddDimension(routing.RegisterTransitMatrix(position), 0, nodeCount + 1, true, PositionDimension);
        var positionDimension = routing.GetDimensionOrDie(PositionDimension);

        // Omitting an order (two nodes) must cost more than any total of km: (2n + T) arcs at most, each <= longest leg.
        var penalty = checked((2L * orders.Count + trucks.Count) * Math.Max(1, longestLeg10) + 1);
        var solver = routing.solver();
        for (var i = 0; i < orders.Count; i++)
        {
            var pickup = manager.NodeToIndex(PickupNode(i));
            var delivery = manager.NodeToIndex(DeliveryNode(i));
            routing.AddPickupAndDelivery(pickup, delivery);
            solver.Add(solver.MakeEquality(routing.VehicleVar(pickup), routing.VehicleVar(delivery)));
            solver.Add(solver.MakeEquality(routing.ActiveVar(pickup), routing.ActiveVar(delivery)));
            solver.Add(solver.MakeLess(positionDimension.CumulVar(pickup), positionDimension.CumulVar(delivery)));

            var notCapable = Enumerable.Range(0, trucks.Count)
                .Where(t => !orders[i].CapableTrucks.Contains(t))
                .Select(t => (long)t)
                .ToArray();
            if (notCapable.Length > 0)
            {
                routing.VehicleVar(pickup).RemoveValues(notCapable);
                routing.VehicleVar(delivery).RemoveValues(notCapable);
            }

            routing.AddDisjunction([pickup], penalty);
            routing.AddDisjunction([delivery], penalty);
        }

        var parameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        parameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        parameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        parameters.GuidedLocalSearchLambdaCoefficient = lambda;
        parameters.TimeLimit = Duration.FromTimeSpan(timeLimit);
        if (_solutionLimit > 0)
        {
            parameters.SolutionLimit = _solutionLimit;
        }

        var solution = routing.SolveWithParameters(parameters);
        var status = routing.GetStatus().ToString();
        if (solution is null)
        {
            // Cannot happen with disjunctions (the empty plan is feasible); every order is then left to the repair step.
            return (status, 0);
        }

        for (var t = 0; t < trucks.Count; t++)
        {
            var index = solution.Value(routing.NextVar(routing.Start(t)));
            while (!routing.IsEnd(index))
            {
                var node = manager.IndexToNode(index);
                var order = orders[(node - 1) / 2];
                routes[t].Add(new RouteAction(order, node % 2 == 1 ? StopAction.Pickup : StopAction.Delivery));
                index = solution.Value(routing.NextVar(index));
            }
        }

        return (status, solution.ObjectiveValue());
    }

    private static int PickupNode(int order) => 2 * order + 1;

    private static int DeliveryNode(int order) => 2 * order + 2;

    /// <summary>Rounds a non-negative time in tenths of a minute up (tolerant to floating noise), capped above the horizon.</summary>
    private static long CeilTenths(double tenths, long horizon10)
    {
        var value = Math.Ceiling(tenths - 1e-7);
        return value > horizon10 ? horizon10 + 1 : (long)Math.Max(0, value);
    }

    /// <summary>
    /// Greedy re-insertion of the orders the solver omitted, in scenario order, repeated until nothing changes:
    /// cheapest feasible insertion (fewest added km; ties: truck, pickup position, delivery position in that order),
    /// checked in exact arithmetic. Returns the number of inserted orders.
    /// </summary>
    private static int Repair(PreparedScenario input, List<RouteAction>[] routes, bool[] planned)
    {
        var inserted = 0;
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < input.Plannable.Count; i++)
            {
                if (planned[i])
                {
                    continue;
                }

                var best = BestInsertion(input, routes, i);
                if (best is { } b)
                {
                    routes[b.Truck] = b.Actions;
                    planned[i] = true;
                    inserted++;
                    changed = true;
                }
            }
        }

        return inserted;
    }

    /// <summary>Best insertion of order <paramref name="i"/> into one truck's route.</summary>
    private sealed record Insertion(int Truck, List<RouteAction> Actions, int AddedKm10, double Minutes);

    /// <summary>
    /// Cheapest insertion of plannable order <paramref name="i"/> over all capable trucks and positions that respects
    /// capacity after every action and the working day (exact arithmetic, like the validator).
    /// </summary>
    private static Insertion? BestInsertion(PreparedScenario input, List<RouteAction>[] routes, int i)
    {
        var order = input.Plannable[i];
        Insertion? best = null;
        foreach (var t in order.CapableTrucks)
        {
            var route = routes[t];
            var baseKm10 = RouteKm10(input, route);
            for (var p = 0; p <= route.Count; p++)
            {
                for (var d = p + 1; d <= route.Count + 1; d++)
                {
                    var candidate = new List<RouteAction>(route);
                    candidate.Insert(p, new RouteAction(order, StopAction.Pickup));
                    candidate.Insert(d, new RouteAction(order, StopAction.Delivery));
                    if (!FitsCapacity(input.Trucks[t], candidate))
                    {
                        continue;
                    }

                    var km10 = RouteKm10(input, candidate);
                    var minutes = input.WorkdayMinutes(km10, candidate.Count);
                    if (!input.FitsWorkday(minutes))
                    {
                        continue;
                    }

                    var added = km10 - baseKm10;
                    if (best is null || added < best.AddedKm10)
                    {
                        best = new Insertion(t, candidate, added, minutes);
                    }
                }
            }
        }

        return best;
    }

    private static bool FitsCapacity(Truck truck, List<RouteAction> actions)
    {
        long kg = 0;
        var machines = 0;
        foreach (var (order, action) in actions)
        {
            var pickup = action == StopAction.Pickup;
            kg += pickup ? order.Machine.WeightKg : -order.Machine.WeightKg;
            machines += pickup ? 1 : -1;
            if (kg > truck.CapacityKg || machines > truck.MaxMachines)
            {
                return false;
            }
        }

        return true;
    }

    private static int RouteKm10(PreparedScenario input, List<RouteAction> actions)
    {
        var location = input.BaseIndex;
        var km10 = 0;
        foreach (var (order, action) in actions)
        {
            var next = action == StopAction.Pickup ? order.PickupIndex : order.DeliveryIndex;
            km10 = checked(km10 + input.Legs10[location, next]);
            location = next;
        }

        return actions.Count == 0 ? 0 : checked(km10 + input.Legs10[location, input.BaseIndex]);
    }

    private static Plan BuildPlan(PreparedScenario input, List<RouteAction>[] routes, bool[] planned)
    {
        var truckRoutes = new List<TruckRoute>(input.Trucks.Count);
        var totalKm10 = 0;
        for (var t = 0; t < input.Trucks.Count; t++)
        {
            var (route, km10) = PlanAssembly.BuildRoute(input, input.Trucks[t], routes[t], routes[t].Count / 2);
            truckRoutes.Add(route);
            totalKm10 = checked(totalKm10 + km10);
        }

        var infeasible = input.Report.Rejected.Select(r => (r.ScenarioIndex, r.Infeasible)).ToList();
        var omitted = planned.Count(p => !p);
        for (var i = 0; i < input.Plannable.Count; i++)
        {
            if (planned[i])
            {
                continue;
            }

            var order = input.Plannable[i];
            infeasible.Add((order.ScenarioIndex, new InfeasibleOrder(order.Order, InfeasibilityReason.DoesNotFitWithOtherOrders,
                DoesNotFitMessage(input, routes, i, omitted, totalKm10))));
        }

        return new Plan(
            truckRoutes,
            infeasible.OrderBy(x => x.ScenarioIndex).Select(x => x.Infeasible).ToList(),
            totalKm10 / 10.0);
    }

    /// <summary>
    /// K3 message (R40) for an omitted order: per capable truck the working-day minutes of the best insertion into
    /// that truck's final route (all exceed the limit, otherwise the repair step would have inserted the order).
    /// Appending pickup and delivery at the end always respects capacity, so every capable truck has a value.
    /// </summary>
    private static string DoesNotFitMessage(PreparedScenario input, List<RouteAction>[] routes, int i, int omitted, int totalKm10)
    {
        var options = input.Options;
        var order = input.Plannable[i];
        var needed = new List<string>(order.CapableTrucks.Count);
        foreach (var t in order.CapableTrucks)
        {
            var best = BestInsertionMinutes(input, routes, i, t);
            needed.Add($"kamion {input.Trucks[t].Id} {Feasibility.Format(best)} min");
        }

        return $"Ne stane u radni dan ({Feasibility.ModeDescription(options)}, limit {Feasibility.Format(options.MaxMinutes)} min): "
            + $"nalog {order.Order.Id} sam traži {Feasibility.Format(order.StandaloneMinutes)} min, "
            + $"ali umetnut u isplanirane rute (najbolje mjesto) trebao bi: {string.Join(", ", needed)}. "
            + $"Heuristika (OR-Tools) izostavila je {omitted} od {input.Plannable.Count} naloga; najmanji mogući broj "
            + $"izostavljenih nije zajamčen (km plana {Feasibility.Format(totalKm10 / 10.0)} km).";
    }

    /// <summary>Smallest working-day minutes over all capacity-feasible insertions of order i into truck t's route.</summary>
    private static double BestInsertionMinutes(PreparedScenario input, List<RouteAction>[] routes, int i, int t)
    {
        var order = input.Plannable[i];
        var route = routes[t];
        var best = double.PositiveInfinity;
        for (var p = 0; p <= route.Count; p++)
        {
            for (var d = p + 1; d <= route.Count + 1; d++)
            {
                var candidate = new List<RouteAction>(route);
                candidate.Insert(p, new RouteAction(order, StopAction.Pickup));
                candidate.Insert(d, new RouteAction(order, StopAction.Delivery));
                if (FitsCapacity(input.Trucks[t], candidate))
                {
                    best = Math.Min(best, input.WorkdayMinutes(RouteKm10(input, candidate), candidate.Count));
                }
            }
        }

        return best;
    }
}

/// <summary>One guided-local-search run of the <see cref="OrToolsPlanner"/> portfolio.</summary>
/// <param name="Lambda">GLS lambda coefficient of the run.</param>
/// <param name="SolverStatus">OR-Tools routing status, e.g. <c>RoutingSuccess</c>.</param>
/// <param name="Objective">Objective of the solver solution: km in tenths + penalties of omitted orders.</param>
/// <param name="OmittedBySolver">Plannable orders the solver left out (before the repair step).</param>
/// <param name="InsertedByRepair">Orders the greedy repair step inserted afterwards.</param>
/// <param name="PlannedOrders">Planned orders after the repair step.</param>
/// <param name="TotalKm">Km of the run's plan after the repair step.</param>
/// <param name="SolverMilliseconds">Wall-clock time of model building and search.</param>
/// <param name="StoppedByTimeLimit">
/// True when the run used (almost) all of its time budget, i.e. it was most likely stopped by the time limit and
/// not by the solution limit; then the plan may differ between calls (R39).
/// </param>
public sealed record OrToolsRun(
    double Lambda,
    string SolverStatus,
    long Objective,
    int OmittedBySolver,
    int InsertedByRepair,
    int PlannedOrders,
    double TotalKm,
    double SolverMilliseconds,
    bool StoppedByTimeLimit);

/// <summary>How one <see cref="OrToolsPlanner"/> call ended (diagnostics and UI).</summary>
/// <param name="Runs">The portfolio runs in order (empty when there was nothing to plan).</param>
/// <param name="ChosenRun">Index into <paramref name="Runs"/> of the run whose plan was returned; -1 if none.</param>
/// <param name="TotalMilliseconds">Wall-clock time of all runs including the repair steps.</param>
/// <param name="SolutionLimit">Solution limit per run (&lt;= 0: none).</param>
/// <param name="TimeLimitSeconds">Time budget shared by all runs.</param>
public sealed record OrToolsStatistics(
    IReadOnlyList<OrToolsRun> Runs,
    int ChosenRun,
    double TotalMilliseconds,
    long SolutionLimit,
    double TimeLimitSeconds)
{
    /// <summary>Always false: OR-Tools with guided local search is a heuristic (spec R38).</summary>
    public bool OptimumGuaranteed => false;

    /// <summary>True when every run stopped at the solution limit, so the same input gives the same plan (R39).</summary>
    public bool StoppedBySolutionLimit => SolutionLimit > 0 && Runs.All(r => !r.StoppedByTimeLimit);

    /// <summary>The run whose plan was returned, or null when the solver did not run (nothing to plan).</summary>
    public OrToolsRun? Chosen => ChosenRun >= 0 && ChosenRun < Runs.Count ? Runs[ChosenRun] : null;

    /// <summary>Routing status of the chosen run, or "NOT_RUN" when there was nothing to plan.</summary>
    public string SolverStatus => Chosen?.SolverStatus ?? "NOT_RUN";

    /// <summary>Solver objective of the chosen run (km in tenths + penalties), 0 when the solver did not run.</summary>
    public long Objective => Chosen?.Objective ?? 0;

    /// <summary>Orders the solver left out in the chosen run (before repair).</summary>
    public int OmittedBySolver => Chosen?.OmittedBySolver ?? 0;

    /// <summary>Orders the repair step inserted into the chosen run's plan.</summary>
    public int InsertedByRepair => Chosen?.InsertedByRepair ?? 0;
}

/// <summary>A plan together with how the OR-Tools search ended.</summary>
public sealed record OrToolsResult(Plan Plan, OrToolsStatistics Statistics)
{
    /// <summary>The input checks the plan was built on (usable trucks, rejected orders, scenario warnings for the UI).</summary>
    public FeasibilityReport? Feasibility { get; init; }
}
