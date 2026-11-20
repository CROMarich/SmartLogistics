using System.Globalization;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Validation;

/// <summary>One broken rule found in a plan.</summary>
/// <param name="RuleId">Rule from docs/spec.md, e.g. "R11".</param>
/// <param name="TruckId">Truck the violation belongs to; null for plan-level violations.</param>
/// <param name="StepIndex">0-based index into <see cref="TruckRoute.Stops"/>; null when not tied to a stop.</param>
/// <param name="Message">Human-readable explanation with the numbers involved.</param>
public sealed record PlanViolation(string RuleId, string? TruckId, int? StepIndex, string Message);

/// <summary>
/// Independent plan validator. It knows nothing about how a plan was produced: every route is replayed
/// from the base, step by step, and all kilometres, minutes and loads are recomputed from the scenario
/// (coordinates, machine weights, truck limits) and <see cref="PlanningOptions"/>. Numbers reported in
/// the plan are only compared against the recomputed values, never trusted.
/// </summary>
public static class PlanValidator
{
    /// <summary>Allowed difference between a reported and a recomputed km / minute value.</summary>
    private const double ValueTolerance = 0.05;

    /// <summary>Floating-point guard for inclusive limits (spec R35: exactly 540 min is allowed).</summary>
    private const double LimitEpsilon = 1e-6;

    /// <summary>All rule IDs this validator can report (used by the UI to show coverage).</summary>
    public static IReadOnlyList<string> CheckedRuleIds { get; } =
    [
        "R1", "R2", "R4", "R5", "R6", "R7", "R8", "R10", "R11", "R12", "R13", "R16", "R17", "R18",
        "R20", "R21", "R23", "R26", "R28", "R32", "R33", "R36", "R37",
    ];

    /// <summary>Returns every violation found; an empty list means the plan is valid. Never throws on an invalid plan.</summary>
    public static IReadOnlyList<PlanViolation> Validate(Plan plan, Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        return new ValidationRun(plan, scenario, options).Execute();
    }

    private static bool Differs(double reported, double computed) => !(Math.Abs(reported - computed) <= ValueTolerance);

    private static bool Exceeds(double value, double limit) => !(value <= limit + LimitEpsilon);

    private static string N(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);

    private static string Code(InfeasibilityReason reason) => reason switch
    {
        InfeasibilityReason.ExceedsAllCapacities => "K1",
        InfeasibilityReason.ExceedsWorkday => "K2",
        InfeasibilityReason.DoesNotFitWithOtherOrders => "K3",
        InfeasibilityReason.InvalidOrderData => "K4",
        _ => $"undefined ({(int)reason})",
    };

    /// <summary>One action of an order somewhere in the plan.</summary>
    private sealed record StopRef(int Route, int Step, StopAction Action);

    /// <summary>Recomputed facts about one route, used by the plan-level checks.</summary>
    private sealed record RouteOutcome(
        Truck? ScenarioTruck,
        bool DistancesKnown,
        double TotalKm,
        double DrivingMinutes,
        int ActionCount,
        Location? LastLocation,
        double ReturnLegKm,
        bool EndsEmpty);

    /// <summary>The category an infeasible order must have according to the data (spec R33, R37).</summary>
    private sealed record ExpectedCategory(IReadOnlySet<InfeasibilityReason> Allowed, string Detail, bool FeasibleAlone);

    private sealed class ValidationRun
    {
        private readonly Plan _plan;
        private readonly ScenarioIndex _index;
        private readonly PlanningOptions _options;
        private readonly IReadOnlyList<TruckRoute?> _routes;
        private readonly IReadOnlyList<InfeasibleOrder?> _infeasible;
        private readonly string[] _labels;
        private readonly List<PlanViolation> _violations = [];
        private readonly Dictionary<string, List<StopRef>> _occurrences = new(StringComparer.Ordinal);
        private readonly HashSet<string> _invalidDataReported = new(StringComparer.Ordinal);
        private readonly List<RouteOutcome?> _outcomes = [];

        public ValidationRun(Plan plan, Scenario scenario, PlanningOptions options)
        {
            _plan = plan;
            _index = new ScenarioIndex(scenario);
            _options = options;
            _routes = plan.Routes ?? [];
            _infeasible = plan.InfeasibleOrders ?? [];
            _labels = _routes
                .Select((route, r) => route?.Truck?.Id is { } id ? $"truck {id}" : $"route {r + 1}")
                .ToArray();
        }

        private bool DrivingOnly => _options.WorkdayMode == WorkdayMode.DrivingOnly;

        private string ModeName => DrivingOnly ? nameof(WorkdayMode.DrivingOnly) : nameof(WorkdayMode.Total);

        public IReadOnlyList<PlanViolation> Execute()
        {
            CheckScenarioInput();
            IndexStops();

            var firstRouteByTruck = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var r = 0; r < _routes.Count; r++)
            {
                _outcomes.Add(ValidateRoute(r, firstRouteByTruck));
            }

            CheckOrdersOnSeveralRoutes();
            CheckOrderCoverage();
            CheckInfeasibleOrders();
            CheckPlanTotalKm();
            return _violations;
        }

        private void Add(string ruleId, string? truckId, int? stepIndex, string message) =>
            _violations.Add(new PlanViolation(ruleId, truckId, stepIndex, message));

        // R37: a plan must not exist for globally invalid input (planning must not have started).
        private void CheckScenarioInput()
        {
            foreach (var problem in _index.GlobalProblems)
            {
                Add("R37", null, null, $"Invalid scenario input ({problem}); planning should have been refused.");
            }
        }

        private void IndexStops()
        {
            for (var r = 0; r < _routes.Count; r++)
            {
                var stops = _routes[r]?.Stops ?? [];
                for (var k = 0; k < stops.Count; k++)
                {
                    var stop = stops[k];
                    if (stop?.OrderId is null || !Enum.IsDefined(stop.Action))
                    {
                        continue;
                    }

                    if (!_occurrences.TryGetValue(stop.OrderId, out var list))
                    {
                        list = [];
                        _occurrences[stop.OrderId] = list;
                    }

                    list.Add(new StopRef(r, k, stop.Action));
                }
            }
        }

        private RouteOutcome? ValidateRoute(int r, Dictionary<string, int> firstRouteByTruck)
        {
            var route = _routes[r];
            if (route is null)
            {
                Add("R1", null, null, $"Route {r + 1} is missing (null).");
                return null;
            }

            var truckId = route.Truck?.Id;
            var label = _labels[r];
            var scenarioTruck = CheckTruck(r, route, firstRouteByTruck);
            var stops = route.Stops ?? [];

            if (stops.Count == 0)
            {
                return ValidateUnusedRoute(route, truckId, label, scenarioTruck);
            }

            var minutesPerAction = _options.MinutesPerAction;
            var previous = _index.Base;
            var distancesKnown = previous is not null;
            double km = 0;
            double drivingMinutes = 0;
            var loadKg = 0;
            var machines = 0;
            var onBoard = new Dictionary<string, (int WeightKg, int Step)>(StringComparer.Ordinal);
            var pickedUp = new Dictionary<string, int>(StringComparer.Ordinal);
            var delivered = new HashSet<string>(StringComparer.Ordinal);
            var ordersOnTruck = new HashSet<string>(StringComparer.Ordinal);

            for (var k = 0; k < stops.Count; k++)
            {
                var stop = stops[k];
                var step = k + 1;
                if (stop is null)
                {
                    // R8: a missing stop has no location; distances after it cannot be recomputed.
                    Add("R8", truckId, k, $"Step {step} of {label} is missing (null).");
                    distancesKnown = false;
                    continue;
                }

                // R8: the stop location must exist; R14/R15/R16 legs are recomputed from coordinates.
                var location = _index.FindLocation(stop.LocationId);
                if (location is null)
                {
                    Add("R8", truckId, k, $"Step {step} of {label} is at unknown location '{stop.LocationId}'.");
                    distancesKnown = false;
                }

                double? leg = null;
                if (distancesKnown && previous is not null && location is not null)
                {
                    leg = Distance.Leg(previous, location);
                    km += leg.Value;
                    drivingMinutes += _options.DrivingMinutes(leg.Value);
                }

                previous = location;

                if (stop.OrderId is not null)
                {
                    ordersOnTruck.Add(stop.OrderId);
                }

                var order = _index.FindOrder(stop.OrderId);
                Machine? machine = null;
                if (order is null)
                {
                    // R23: only orders from the input may appear in the plan.
                    Add("R23", truckId, k, $"Step {step} of {label} refers to order '{stop.OrderId}', which is not in the scenario.");
                }
                else
                {
                    machine = _index.FindMachine(order.MachineId);
                    CheckOrderData(order, truckId, k, label);
                }

                if (!Enum.IsDefined(stop.Action))
                {
                    // R8: an action other than pickup / delivery cannot match any order location.
                    Add("R8", truckId, k, $"Step {step} of {label} has undefined action {(int)stop.Action}.");
                }
                else if (order is not null)
                {
                    // R8: pickup at the order's pickup location, delivery at its delivery location.
                    var expectedLocation = stop.Action == StopAction.Pickup ? order.PickupLocationId : order.DeliveryLocationId;
                    if (!string.Equals(stop.LocationId, expectedLocation, StringComparison.Ordinal))
                    {
                        var what = stop.Action == StopAction.Pickup ? "pickup" : "delivery";
                        Add("R8", truckId, k,
                            $"Step {step} of {label}: {what} of order {order.Id} is at {stop.LocationId}, expected {what} location {expectedLocation}.");
                    }

                    if (stop.Action == StopAction.Pickup)
                    {
                        SimulatePickup(k, order, machine, scenarioTruck, truckId, label, onBoard, pickedUp, ref loadKg, ref machines);
                    }
                    else
                    {
                        SimulateDelivery(r, k, order, truckId, label, onBoard, delivered, ref loadKg, ref machines);
                    }
                }

                // R10 (with R9, R34: the truck leaves the base empty): reported load must match the replay.
                if (stop.LoadKgAfter != loadKg)
                {
                    Add("R10", truckId, k,
                        $"Reported load {stop.LoadKgAfter} kg after step {step} of {label} differs from computed {loadKg} kg.");
                }

                if (stop.MachinesAfter != machines)
                {
                    Add("R10", truckId, k,
                        $"Reported {stop.MachinesAfter} machines after step {step} of {label} differs from computed {machines}.");
                }

                if (scenarioTruck is not null)
                {
                    // R11: load after every action <= capacity (equality allowed).
                    if (loadKg > scenarioTruck.CapacityKg)
                    {
                        Add("R11", truckId, k,
                            $"Load {loadKg} kg > capacity {scenarioTruck.CapacityKg} kg after step {step} of {label}.");
                    }

                    // R12: machines after every action <= max machines.
                    if (machines > scenarioTruck.MaxMachines)
                    {
                        Add("R12", truckId, k,
                            $"{machines} machines > max {scenarioTruck.MaxMachines} after step {step} of {label}.");
                    }
                }

                if (leg is { } legKm)
                {
                    // R14, R15, R16: each leg is rounded on its own, km are sums of rounded legs.
                    if (Differs(stop.LegKm, legKm))
                    {
                        Add("R16", truckId, k,
                            $"Reported leg {N(stop.LegKm)} km to step {step} of {label} differs from computed {N(legKm)} km.");
                    }

                    if (Differs(stop.CumulativeKm, km))
                    {
                        Add("R16", truckId, k,
                            $"Reported cumulative {N(stop.CumulativeKm)} km at step {step} of {label} differs from computed {N(km)} km.");
                    }

                    // R17 + R18: elapsed time when the action is finished = driving so far + actions so far.
                    var cumulativeMinutes = drivingMinutes + step * minutesPerAction;
                    if (Differs(stop.CumulativeMinutes, cumulativeMinutes))
                    {
                        Add("R17", truckId, k,
                            $"Reported cumulative {N(stop.CumulativeMinutes)} min at step {step} of {label} differs from computed {N(cumulativeMinutes)} min (driving {N(drivingMinutes)} + {step} actions x {N(minutesPerAction)}).");
                    }
                }
            }

            var lastIndex = stops.Count - 1;

            // R4: every picked-up order must also be delivered.
            foreach (var (orderId, (_, pickupStep)) in onBoard)
            {
                Add("R4", truckId, pickupStep,
                    $"Order {orderId} is picked up at step {pickupStep + 1} of {label} but never delivered.");
            }

            // R28: after the last action the truck is empty.
            if (loadKg != 0 || machines != 0)
            {
                Add("R28", truckId, lastIndex,
                    $"{label} is not empty after its last step {lastIndex + 1}: {loadKg} kg, {machines} machines.");
            }

            // R18, R19: every stop is one action of MinutesPerAction; the return to the base is not an action.
            var actionMinutes = stops.Count * minutesPerAction;
            if (Differs(route.ActionMinutes, actionMinutes))
            {
                Add("R18", truckId, null,
                    $"Reported action time {N(route.ActionMinutes)} min of {label} differs from computed {N(actionMinutes)} min ({stops.Count} actions x {N(minutesPerAction)}).");
            }

            // R26: order count = number of distinct orders handled by the truck.
            if (route.OrderCount != ordersOnTruck.Count)
            {
                Add("R26", truckId, null,
                    $"Reported order count {route.OrderCount} of {label} differs from {ordersOnTruck.Count} orders in its stops.");
            }

            var endsEmpty = loadKg == 0 && machines == 0 && onBoard.Count == 0;
            if (!distancesKnown || previous is null || _index.Base is null)
            {
                return new RouteOutcome(scenarioTruck, false, 0, 0, stops.Count, null, 0, endsEmpty);
            }

            var returnLeg = Distance.Leg(previous, _index.Base);
            var totalKm = km + returnLeg;
            var totalDriving = drivingMinutes + _options.DrivingMinutes(returnLeg);

            if (Differs(route.TotalKm, totalKm))
            {
                if (returnLeg > ValueTolerance && !Differs(route.TotalKm, km))
                {
                    // R2: the reported km stop at the last action - the return leg to the base is missing.
                    Add("R2", truckId, null,
                        $"Route of {label} does not return to base {_index.BaseLocationId}: reported {N(route.TotalKm)} km equals the km up to the last step ({N(km)} km); the return leg {previous.Id} -> {_index.BaseLocationId} ({N(returnLeg)} km) is missing, expected {N(totalKm)} km.");
                }
                else
                {
                    // R16: route km = all legs including the return leg.
                    Add("R16", truckId, null,
                        $"Reported total {N(route.TotalKm)} km of {label} differs from computed {N(totalKm)} km (including return leg {N(returnLeg)} km).");
                }
            }

            // R17: driving minutes of all legs including the return leg.
            if (Differs(route.DrivingMinutes, totalDriving))
            {
                Add("R17", truckId, null,
                    $"Reported driving time {N(route.DrivingMinutes)} min of {label} differs from computed {N(totalDriving)} min.");
            }

            CheckWorkday(truckId, label, totalDriving, actionMinutes);

            return new RouteOutcome(scenarioTruck, true, totalKm, totalDriving, stops.Count, previous, returnLeg, endsEmpty);
        }

        // R1: the truck of a route must exist in the scenario with the same limits, and have only one route.
        private Truck? CheckTruck(int r, TruckRoute route, Dictionary<string, int> firstRouteByTruck)
        {
            if (route.Truck is null)
            {
                Add("R1", null, null, $"Route {r + 1} has no truck.");
                return null;
            }

            var planTruck = route.Truck;
            var scenarioTruck = _index.FindTruck(planTruck.Id);
            if (scenarioTruck is null)
            {
                Add("R1", planTruck.Id, null, $"Truck '{planTruck.Id}' of route {r + 1} does not exist in the scenario.");
            }
            else if (scenarioTruck.CapacityKg != planTruck.CapacityKg || scenarioTruck.MaxMachines != planTruck.MaxMachines)
            {
                Add("R1", planTruck.Id, null,
                    $"Truck {planTruck.Id} limits in the plan ({planTruck.CapacityKg} kg, {planTruck.MaxMachines} machines) differ from the scenario ({scenarioTruck.CapacityKg} kg, {scenarioTruck.MaxMachines} machines).");
            }

            if (planTruck.Id is not null)
            {
                if (firstRouteByTruck.TryGetValue(planTruck.Id, out var first))
                {
                    Add("R1", planTruck.Id, null, $"Truck {planTruck.Id} has more than one route (routes {first + 1} and {r + 1}).");
                }
                else
                {
                    firstRouteByTruck[planTruck.Id] = r;
                }
            }

            return scenarioTruck;
        }

        // R3, R36: an unused truck has no stops, 0 km, 0 minutes and 0 orders.
        private RouteOutcome ValidateUnusedRoute(TruckRoute route, string? truckId, string label, Truck? scenarioTruck)
        {
            if (Differs(route.TotalKm, 0))
            {
                Add("R36", truckId, null, $"Unused {label} (no stops) reports {N(route.TotalKm)} km, expected 0 km.");
            }

            if (Differs(route.DrivingMinutes, 0))
            {
                Add("R36", truckId, null, $"Unused {label} (no stops) reports {N(route.DrivingMinutes)} driving minutes, expected 0.");
            }

            if (Differs(route.ActionMinutes, 0))
            {
                Add("R36", truckId, null, $"Unused {label} (no stops) reports {N(route.ActionMinutes)} action minutes, expected 0.");
            }

            if (route.OrderCount != 0)
            {
                Add("R36", truckId, null, $"Unused {label} (no stops) reports {route.OrderCount} orders, expected 0.");
            }

            return new RouteOutcome(scenarioTruck, _index.Base is not null, 0, 0, 0, _index.Base, 0, true);
        }

        // R37: an order with invalid data must be infeasible (K4), not planned. Reported once per order.
        private void CheckOrderData(TransportOrder order, string? truckId, int k, string label)
        {
            var problems = _index.OrderDataProblems(order);
            if (problems.Count > 0 && _invalidDataReported.Add(order.Id))
            {
                Add("R37", truckId, k,
                    $"Order {order.Id} is planned on {label} (step {k + 1}) but has invalid data ({string.Join("; ", problems)}); it must be infeasible with category K4.");
            }
        }

        private void SimulatePickup(
            int k, TransportOrder order, Machine? machine, Truck? scenarioTruck, string? truckId, string label,
            Dictionary<string, (int WeightKg, int Step)> onBoard, Dictionary<string, int> pickedUp,
            ref int loadKg, ref int machines)
        {
            var step = k + 1;

            // R4: exactly one pickup per order (a second pickup in another route is R7).
            if (pickedUp.TryGetValue(order.Id, out var firstPickup))
            {
                Add("R4", truckId, k,
                    $"Order {order.Id} is picked up again at step {step} of {label} (already picked up at step {firstPickup + 1}).");
                return;
            }

            pickedUp[order.Id] = k;
            var weightKg = machine?.WeightKg ?? 0;
            onBoard[order.Id] = (weightKg, k);
            loadKg += weightKg;
            machines += 1;

            // R13: a machine heavier than the truck's capacity must not be assigned to it.
            if (machine is not null && scenarioTruck is not null && !scenarioTruck.CanCarry(machine))
            {
                Add("R13", truckId, k,
                    $"Machine {machine.Id} ({machine.WeightKg} kg) of order {order.Id} at step {step} is heavier than capacity {scenarioTruck.CapacityKg} kg of {label}.");
            }

        }

        private void SimulateDelivery(
            int r, int k, TransportOrder order, string? truckId, string label,
            Dictionary<string, (int WeightKg, int Step)> onBoard, HashSet<string> delivered,
            ref int loadKg, ref int machines)
        {
            var step = k + 1;
            if (onBoard.Remove(order.Id, out var cargo))
            {
                loadKg -= cargo.WeightKg;
                machines -= 1;
                delivered.Add(order.Id);
                return;
            }

            // R4: exactly one delivery per order.
            if (delivered.Contains(order.Id))
            {
                Add("R4", truckId, k, $"Order {order.Id} is delivered again at step {step} of {label}.");
                return;
            }

            var pickups = _occurrences.GetValueOrDefault(order.Id) ?? [];
            var laterPickup = pickups.FirstOrDefault(p => p.Route == r && p.Action == StopAction.Pickup && p.Step > k);
            if (laterPickup is not null)
            {
                // R6: pickup strictly before delivery in the same route.
                Add("R6", truckId, k,
                    $"Order {order.Id} is delivered at step {step} of {label} before its pickup at step {laterPickup.Step + 1}.");
                return;
            }

            var otherPickup = pickups.FirstOrDefault(p => p.Route != r && p.Action == StopAction.Pickup);
            if (otherPickup is not null)
            {
                // R5: pickup and delivery of an order by the same truck.
                Add("R5", truckId, k,
                    $"Order {order.Id} is delivered by {label} at step {step} but picked up by {_labels[otherPickup.Route]} (step {otherPickup.Step + 1}).");
                return;
            }

            // R6 (and R4): a delivery without any pickup.
            Add("R6", truckId, k, $"Order {order.Id} is delivered at step {step} of {label} without any pickup.");
        }

        // R20 / R21 / R29 / R35: per truck, whole route from base to base, inclusive limit.
        private void CheckWorkday(string? truckId, string label, double drivingMinutes, double actionMinutes)
        {
            var max = _options.MaxMinutes;
            if (DrivingOnly)
            {
                if (Exceeds(drivingMinutes, max))
                {
                    Add("R21", truckId, null,
                        $"Driving time {N(drivingMinutes)} min of {label} > max {N(max)} min (DrivingOnly).");
                }
            }
            else
            {
                var total = drivingMinutes + actionMinutes;
                if (Exceeds(total, max))
                {
                    Add("R20", truckId, null,
                        $"Working time {N(total)} min of {label} (driving {N(drivingMinutes)} + actions {N(actionMinutes)}) > max {N(max)} min (Total).");
                }
            }
        }

        // R7: an order is assigned to at most one truck (one route).
        private void CheckOrdersOnSeveralRoutes()
        {
            foreach (var (orderId, refs) in _occurrences)
            {
                var routes = refs.Select(x => x.Route).Distinct().ToList();
                if (routes.Count < 2)
                {
                    continue;
                }

                var second = routes[1];
                var step = refs.First(x => x.Route == second).Step;
                Add("R7", _routes[second]?.Truck?.Id, step,
                    $"Order {orderId} appears on more than one route: {string.Join(", ", routes.Select(x => _labels[x]))}.");
            }
        }

        // R23: every input order is either planned or listed as infeasible - exactly one of the two, once.
        private void CheckOrderCoverage()
        {
            var infeasibleCount = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _infeasible.Count; i++)
            {
                var entry = _infeasible[i];
                if (entry?.Order is null)
                {
                    Add("R23", null, null, $"Infeasible entry {i + 1} has no order.");
                    continue;
                }

                var id = entry.Order.Id;
                if (_index.FindOrder(id) is null)
                {
                    Add("R23", null, null, $"Infeasible order '{id}' is not in the scenario.");
                    continue;
                }

                infeasibleCount[id] = infeasibleCount.GetValueOrDefault(id) + 1;
            }

            foreach (var order in _index.Orders)
            {
                var planned = _occurrences.TryGetValue(order.Id, out var refs);
                var count = infeasibleCount.GetValueOrDefault(order.Id);
                if (!planned && count == 0)
                {
                    Add("R23", null, null, $"Order {order.Id} is neither planned nor listed as infeasible.");
                }

                if (planned && count > 0)
                {
                    var routes = string.Join(", ", refs!.Select(x => x.Route).Distinct().Select(x => _labels[x]));
                    Add("R23", _routes[refs![0].Route]?.Truck?.Id, refs[0].Step,
                        $"Order {order.Id} is planned ({routes}) and also listed as infeasible.");
                }

                if (count > 1)
                {
                    Add("R23", null, null, $"Order {order.Id} is listed as infeasible {count} times.");
                }
            }
        }

        // R33: message and a category consistent with the data; R32: a feasible order must not be left out.
        private void CheckInfeasibleOrders()
        {
            var reportedR32 = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _infeasible)
            {
                if (entry?.Order is null)
                {
                    continue;
                }

                var id = entry.Order.Id;
                if (string.IsNullOrWhiteSpace(entry.Message))
                {
                    Add("R33", null, null, $"Infeasible order {id} has no message.");
                }

                var reasonDefined = Enum.IsDefined(entry.Reason);
                if (!reasonDefined)
                {
                    Add("R33", null, null, $"Infeasible order {id} has undefined reason category {(int)entry.Reason}.");
                }

                var order = _index.FindOrder(id);
                if (order is null)
                {
                    continue;
                }

                var expected = ExpectCategory(order);
                if (reasonDefined && !expected.Allowed.Contains(entry.Reason))
                {
                    var allowed = string.Join(" or ", expected.Allowed.Select(Code));
                    Add("R33", null, null,
                        $"Infeasible order {id} is reported as {Code(entry.Reason)} ({entry.Reason}), but the data give {allowed}: {expected.Detail}.");
                }

                if (expected.FeasibleAlone && !_occurrences.ContainsKey(id) && reportedR32.Add(id))
                {
                    CheckCouldBeAppended(order);
                }
            }
        }

        private ExpectedCategory ExpectCategory(TransportOrder order)
        {
            var problems = _index.OrderDataProblems(order);
            if (problems.Count > 0)
            {
                return new ExpectedCategory(new HashSet<InfeasibilityReason> { InfeasibilityReason.InvalidOrderData },
                    string.Join("; ", problems), false);
            }

            var allowed = new HashSet<InfeasibilityReason>();
            if (_index.SharesMachine(order))
            {
                allowed.Add(InfeasibilityReason.InvalidOrderData);
            }

            var machine = _index.FindMachine(order.MachineId)!;
            var capable = _index.Trucks.Where(t => t.CanCarry(machine) && t.MaxMachines >= 1).ToList();
            if (capable.Count == 0)
            {
                allowed.Add(InfeasibilityReason.ExceedsAllCapacities);
                var maxCapacity = _index.Trucks.Count == 0 ? 0 : _index.Trucks.Max(t => t.CapacityKg);
                return new ExpectedCategory(allowed,
                    $"machine {machine.Id} {machine.WeightKg} kg > capacity of every truck (max {maxCapacity} kg)", false);
            }

            var needed = StandaloneMinutes(order);
            if (needed is null)
            {
                allowed.Add(InfeasibilityReason.ExceedsWorkday);
                allowed.Add(InfeasibilityReason.DoesNotFitWithOtherOrders);
                return new ExpectedCategory(allowed, "the base location is unknown, so the time cannot be computed", false);
            }

            var max = _options.MaxMinutes;
            if (Exceeds(needed.Value, max))
            {
                allowed.Add(InfeasibilityReason.ExceedsWorkday);
                return new ExpectedCategory(allowed,
                    $"alone (base -> pickup -> delivery -> base) the order needs {N(needed.Value)} min > {N(max)} min ({ModeName})", false);
            }

            allowed.Add(InfeasibilityReason.DoesNotFitWithOtherOrders);
            return new ExpectedCategory(allowed,
                $"machine {machine.Id} {machine.WeightKg} kg fits truck {capable[0].Id} ({capable[0].CapacityKg} kg) and alone the order needs {N(needed.Value)} min <= {N(max)} min ({ModeName})",
                true);
        }

        /// <summary>Workday minutes of the route base -> pickup -> delivery -> base, counted per the workday mode.</summary>
        private double? StandaloneMinutes(TransportOrder order)
        {
            var baseLocation = _index.Base;
            var pickup = _index.FindLocation(order.PickupLocationId);
            var delivery = _index.FindLocation(order.DeliveryLocationId);
            if (baseLocation is null || pickup is null || delivery is null)
            {
                return null;
            }

            var driving = _options.DrivingMinutes(Distance.Leg(baseLocation, pickup))
                + _options.DrivingMinutes(Distance.Leg(pickup, delivery))
                + _options.DrivingMinutes(Distance.Leg(delivery, baseLocation));
            return DrivingOnly ? driving : driving + 2 * _options.MinutesPerAction;
        }

        // R32: an order listed as infeasible that fits as an extra trip at the end of a route (before the
        // return to the base) proves that the plan does not have the maximum number of executed orders.
        private void CheckCouldBeAppended(TransportOrder order)
        {
            var baseLocation = _index.Base;
            var machine = _index.FindMachine(order.MachineId);
            var pickup = _index.FindLocation(order.PickupLocationId);
            var delivery = _index.FindLocation(order.DeliveryLocationId);
            if (baseLocation is null || machine is null || pickup is null || delivery is null)
            {
                return;
            }

            foreach (var truck in _index.Trucks)
            {
                if (!truck.CanCarry(machine) || truck.MaxMachines < 1)
                {
                    continue;
                }

                var outcomes = _outcomes.Where(o => o?.ScenarioTruck?.Id == truck.Id).ToList();
                if (outcomes.Count > 1)
                {
                    continue;
                }

                var outcome = outcomes.Count == 1
                    ? outcomes[0]!
                    : new RouteOutcome(truck, true, 0, 0, 0, baseLocation, 0, true);
                if (!outcome.DistancesKnown || !outcome.EndsEmpty || outcome.LastLocation is null)
                {
                    continue;
                }

                var driving = outcome.DrivingMinutes
                    - _options.DrivingMinutes(outcome.ReturnLegKm)
                    + _options.DrivingMinutes(Distance.Leg(outcome.LastLocation, pickup))
                    + _options.DrivingMinutes(Distance.Leg(pickup, delivery))
                    + _options.DrivingMinutes(Distance.Leg(delivery, baseLocation));
                var actions = outcome.ActionCount + 2;
                var needed = DrivingOnly ? driving : driving + actions * _options.MinutesPerAction;
                if (Exceeds(needed, _options.MaxMinutes))
                {
                    continue;
                }

                Add("R32", truck.Id, null,
                    $"Order {order.Id} is listed as infeasible, but it fits at the end of truck {truck.Id}'s route: {N(needed)} min <= {N(_options.MaxMinutes)} min ({ModeName}), machine {machine.WeightKg} kg <= {truck.CapacityKg} kg; a feasible order must not be left out.");
                return;
            }
        }

        // R16: plan km = sum of the route km.
        private void CheckPlanTotalKm()
        {
            var reportedSum = _routes.Where(r => r is not null).Sum(r => r!.TotalKm);
            var allKnown = _outcomes.All(o => o is { DistancesKnown: true });
            var recomputedSum = _outcomes.Where(o => o is not null).Sum(o => o!.TotalKm);

            // A reported sum that differs from the recomputed one is already reported per route (R2 / R16),
            // so the plan total is wrong only if it matches neither.
            var wrong = allKnown
                ? Differs(_plan.TotalKm, recomputedSum) && Differs(_plan.TotalKm, reportedSum)
                : Differs(_plan.TotalKm, reportedSum);
            if (wrong)
            {
                var expected = allKnown ? $"computed {N(recomputedSum)} km" : $"sum of reported route km {N(reportedSum)} km";
                Add("R16", null, null, $"Reported plan total {N(_plan.TotalKm)} km differs from the {expected}.");
            }
        }
    }
}
