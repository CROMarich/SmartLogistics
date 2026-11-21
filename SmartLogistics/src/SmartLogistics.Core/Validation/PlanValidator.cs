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

    /// <summary>Messages are shown in the Croatian UI: decimal comma, dot as thousands separator, never the thread culture.</summary>
    private static readonly NumberFormatInfo Croatian =
        new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." }; // no ICU needed (InvariantGlobalization-safe)

    /// <summary>km / minutes with at most three decimals: 8.5 -> "8,5".</summary>
    private static string N(double value) => value.ToString("0.0##", Croatian);

    /// <summary>Whole number with thousands separator: 10000 -> "10.000".</summary>
    private static string I(long value) => value.ToString("N0", Croatian);

    /// <summary>10000 -> "10.000 kg".</summary>
    private static string Kg(long value) => $"{I(value)} kg";

    /// <summary>Capitalises a label used at the start of a sentence: "kamion A" -> "Kamion A".</summary>
    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Code(InfeasibilityReason reason) => reason switch
    {
        InfeasibilityReason.ExceedsAllCapacities => "K1",
        InfeasibilityReason.ExceedsWorkday => "K2",
        InfeasibilityReason.DoesNotFitWithOtherOrders => "K3",
        InfeasibilityReason.InvalidOrderData => "K4",
        _ => $"nedefinirano ({I((int)reason)})",
    };

    /// <summary>One action of an order somewhere in the plan.</summary>
    private sealed record StopRef(int Route, int Step, StopAction Action);

    /// <summary>Recomputed facts about one route, used by the plan-level checks.</summary>
    /// <param name="Slot">The scenario truck the route was matched to (null when it is not in the scenario).</param>
    private sealed record RouteOutcome(
        TruckSlot? Slot,
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

        /// <summary>Infeasible entry index -> 0-based occurrence of its order ID in the scenario (R23 matching, R33).</summary>
        private readonly Dictionary<int, int> _entryOccurrence = [];

        /// <summary>Scenario truck slots already matched to a route: (truck ID, occurrence).</summary>
        private readonly HashSet<(string Id, int Occurrence)> _matchedSlots = [];

        public ValidationRun(Plan plan, Scenario scenario, PlanningOptions options)
        {
            _plan = plan;
            _index = new ScenarioIndex(scenario);
            _options = options;
            _routes = plan.Routes ?? [];
            _infeasible = plan.InfeasibleOrders ?? [];
            var routesPerTruck = _routes
                .Where(route => route?.Truck?.Id is not null)
                .GroupBy(route => route!.Truck.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            _labels = _routes
                .Select((route, r) => route?.Truck?.Id is { } id
                    ? routesPerTruck[id] > 1 ? $"kamion {id} (ruta {r + 1})" : $"kamion {id}"
                    : $"ruta {r + 1}")
                .ToArray();
        }

        private bool DrivingOnly => _options.WorkdayMode == WorkdayMode.DrivingOnly;

        private string ModeName => DrivingOnly
            ? $"{nameof(WorkdayMode.DrivingOnly)}: samo vožnja"
            : $"{nameof(WorkdayMode.Total)}: vožnja + radnje";

        public IReadOnlyList<PlanViolation> Execute()
        {
            // Since phase 5 (amended R37, T31) invalid scenario data alone is no violation: the planner must
            // still return a plan. R37 is reported only for what the plan does with invalid data (see
            // CheckRouteMayHaveActions, CheckOrderData and the K4 expectations in ExpectCategory).
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
                Add("R1", null, null, $"Ruta {r + 1} nedostaje (null).");
                return null;
            }

            var truckId = route.Truck?.Id;
            var label = _labels[r];
            var slot = CheckTruck(r, route, firstRouteByTruck);
            var stops = route.Stops ?? [];

            if (stops.Count == 0)
            {
                return ValidateUnusedRoute(route, truckId, label, slot);
            }

            CheckRouteMayHaveActions(truckId, label, slot, stops.Count);

            // R11-R13 use the scenario limits (T15), but only of a usable truck: an unusable truck must not
            // have actions at all (R37 above), and its limits (e.g. 0 kg) would only repeat that violation.
            var scenarioTruck = slot is { Usable: true } ? slot.Truck : null;
            var minutesPerAction = _options.MinutesPerAction;
            var previous = _index.Base;
            var distancesKnown = previous is not null && _index.BaseCoordinatesReliable;
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
                    Add("R8", truckId, k, $"Korak {step} ({label}) nedostaje (null).");
                    distancesKnown = false;
                    continue;
                }

                // R8: the stop location must exist; R14/R15/R16 legs are recomputed from coordinates.
                var location = _index.FindLocation(stop.LocationId);
                if (location is null)
                {
                    Add("R8", truckId, k, $"{Cap(label)}, korak {step}: nepoznata lokacija '{stop.LocationId}'.");
                    distancesKnown = false;
                }
                else if (!_index.HasReliableCoordinates(stop.LocationId))
                {
                    // Non-numeric or ambiguous (duplicated ID) coordinates: the order using this location is
                    // invalid (R37, reported with the order); km after this point cannot be recomputed.
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
                    Add("R23", truckId, k, $"{Cap(label)}, korak {step}: nalog '{stop.OrderId}' ne postoji u scenariju.");
                }
                else
                {
                    machine = _index.FindMachine(order.MachineId);
                    CheckOrderData(order, truckId, k, label);
                }

                if (!Enum.IsDefined(stop.Action))
                {
                    // R8: an action other than pickup / delivery cannot match any order location.
                    Add("R8", truckId, k, $"{Cap(label)}, korak {step}: nedefinirana radnja {I((int)stop.Action)}.");
                }
                else if (order is not null)
                {
                    // R8: pickup at the order's pickup location, delivery at its delivery location.
                    var expectedLocation = stop.Action == StopAction.Pickup ? order.PickupLocationId : order.DeliveryLocationId;
                    if (!string.Equals(stop.LocationId, expectedLocation, StringComparison.Ordinal))
                    {
                        var (what, whose) = stop.Action == StopAction.Pickup ? ("preuzimanje", "preuzimanja") : ("isporuka", "isporuke");
                        Add("R8", truckId, k,
                            $"{Cap(label)}, korak {step}: {what} naloga {order.Id} je na lokaciji {stop.LocationId}, a očekivana lokacija {whose} je {expectedLocation}.");
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
                        $"{Cap(label)}, nakon koraka {step}: prijavljeni teret {Kg(stop.LoadKgAfter)} razlikuje se od izračunatog {Kg(loadKg)}.");
                }

                if (stop.MachinesAfter != machines)
                {
                    Add("R10", truckId, k,
                        $"{Cap(label)}, nakon koraka {step}: prijavljeni broj strojeva {I(stop.MachinesAfter)} razlikuje se od izračunatog {I(machines)}.");
                }

                if (scenarioTruck is not null)
                {
                    // R11: load after every action <= capacity (equality allowed).
                    if (loadKg > scenarioTruck.CapacityKg)
                    {
                        Add("R11", truckId, k,
                            $"{Cap(label)}, nakon koraka {step}: teret {Kg(loadKg)} > nosivost {Kg(scenarioTruck.CapacityKg)}.");
                    }

                    // R12: machines after every action <= max machines.
                    if (machines > scenarioTruck.MaxMachines)
                    {
                        Add("R12", truckId, k,
                            $"{Cap(label)}, nakon koraka {step}: broj strojeva {I(machines)} > dopušteno {I(scenarioTruck.MaxMachines)}.");
                    }
                }

                if (leg is { } legKm)
                {
                    // R14, R15, R16: each leg is rounded on its own, km are sums of rounded legs.
                    if (Differs(stop.LegKm, legKm))
                    {
                        Add("R16", truckId, k,
                            $"{Cap(label)}, korak {step}: prijavljena dionica {N(stop.LegKm)} km razlikuje se od izračunate {N(legKm)} km.");
                    }

                    if (Differs(stop.CumulativeKm, km))
                    {
                        Add("R16", truckId, k,
                            $"{Cap(label)}, korak {step}: prijavljena udaljenost do tog koraka {N(stop.CumulativeKm)} km razlikuje se od izračunate {N(km)} km.");
                    }

                    // R17 + R18: elapsed time when the action is finished = driving so far + actions so far.
                    var cumulativeMinutes = drivingMinutes + step * minutesPerAction;
                    if (Differs(stop.CumulativeMinutes, cumulativeMinutes))
                    {
                        Add("R17", truckId, k,
                            $"{Cap(label)}, korak {step}: prijavljeno vrijeme do kraja radnje {N(stop.CumulativeMinutes)} min razlikuje se od izračunatog {N(cumulativeMinutes)} min (vožnja {N(drivingMinutes)} min + {step} × {N(minutesPerAction)} min radnji).");
                    }
                }
            }

            var lastIndex = stops.Count - 1;

            // R4: every picked-up order must also be delivered.
            foreach (var (orderId, (_, pickupStep)) in onBoard)
            {
                Add("R4", truckId, pickupStep,
                    $"Nalog {orderId} preuzet je u koraku {pickupStep + 1} ({label}), ali nikad nije isporučen.");
            }

            // R28: after the last action the truck is empty.
            if (loadKg != 0 || machines != 0)
            {
                Add("R28", truckId, lastIndex,
                    $"{Cap(label)}: kamion nije prazan nakon zadnjeg koraka {lastIndex + 1} ({Kg(loadKg)}, broj strojeva {I(machines)}).");
            }

            // R18, R19: every stop is one action of MinutesPerAction; the return to the base is not an action.
            var actionMinutes = stops.Count * minutesPerAction;
            if (Differs(route.ActionMinutes, actionMinutes))
            {
                Add("R18", truckId, null,
                    $"{Cap(label)}: prijavljeno vrijeme radnji {N(route.ActionMinutes)} min razlikuje se od izračunatog {N(actionMinutes)} min ({stops.Count} × {N(minutesPerAction)} min).");
            }

            // R26: order count = number of distinct orders handled by the truck.
            if (route.OrderCount != ordersOnTruck.Count)
            {
                Add("R26", truckId, null,
                    $"{Cap(label)}: prijavljeni broj naloga {I(route.OrderCount)} razlikuje se od broja naloga u koracima ({ordersOnTruck.Count}).");
            }

            var endsEmpty = loadKg == 0 && machines == 0 && onBoard.Count == 0;
            if (!distancesKnown || previous is null || _index.Base is null)
            {
                return new RouteOutcome(slot, false, 0, 0, stops.Count, null, 0, endsEmpty);
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
                        $"{Cap(label)}: ruta se ne vraća u bazu {_index.BaseLocationId}: prijavljena udaljenost {N(route.TotalKm)} km jednaka je udaljenosti do zadnjeg koraka ({N(km)} km); nedostaje povratna dionica {previous.Id} -> {_index.BaseLocationId} ({N(returnLeg)} km), očekivano {N(totalKm)} km.");
                }
                else
                {
                    // R16: route km = all legs including the return leg.
                    Add("R16", truckId, null,
                        $"{Cap(label)}: prijavljena ukupna udaljenost {N(route.TotalKm)} km razlikuje se od izračunate {N(totalKm)} km (uključujući povratnu dionicu {N(returnLeg)} km).");
                }
            }

            // R17: driving minutes of all legs including the return leg.
            if (Differs(route.DrivingMinutes, totalDriving))
            {
                Add("R17", truckId, null,
                    $"{Cap(label)}: prijavljeno vrijeme vožnje {N(route.DrivingMinutes)} min razlikuje se od izračunatog {N(totalDriving)} min.");
            }

            CheckWorkday(truckId, label, totalDriving, actionMinutes);

            return new RouteOutcome(slot, true, totalKm, totalDriving, stops.Count, previous, returnLeg, endsEmpty);
        }

        // R1: the truck of a route must exist in the scenario with the same limits, and have only one route.
        // With duplicate truck IDs in the scenario (R37, T32) each scenario truck may have one route: routes with
        // that ID are matched to the scenario trucks in order, preferring a truck with exactly the same limits.
        private TruckSlot? CheckTruck(int r, TruckRoute route, Dictionary<string, int> firstRouteByTruck)
        {
            if (route.Truck is null)
            {
                Add("R1", null, null, $"Ruta {r + 1} nema kamion.");
                return null;
            }

            var planTruck = route.Truck;
            var key = planTruck.Id ?? string.Empty;
            var occurrences = _index.TruckOccurrences(planTruck.Id);
            var first = firstRouteByTruck.TryGetValue(key, out var firstRoute) ? firstRoute : (int?)null;
            firstRouteByTruck.TryAdd(key, r);

            if (occurrences.Count == 0)
            {
                Add("R1", planTruck.Id, null, $"Kamion '{planTruck.Id}' (ruta {r + 1}) ne postoji u scenariju.");
                if (first is not null)
                {
                    Add("R1", planTruck.Id, null, $"Kamion {planTruck.Id} ima više od jedne rute (rute {first + 1} i {r + 1}).");
                }

                return null;
            }

            var free = occurrences.Where(s => !_matchedSlots.Contains((key, s.Occurrence))).ToList();
            var slot = free.FirstOrDefault(s => s.Truck == planTruck) ?? free.FirstOrDefault();
            if (slot is null)
            {
                Add("R1", planTruck.Id, null, occurrences.Count == 1
                    ? $"Kamion {planTruck.Id} ima više od jedne rute (rute {first + 1} i {r + 1})."
                    : $"ID kamiona {planTruck.Id} ima više ruta nego kamiona s tim ID-om u scenariju ({occurrences.Count}); višak: ruta {r + 1}.");

                // The extra route is still checked against the limits of the first truck with that ID.
                slot = occurrences[0];
            }

            _matchedSlots.Add((key, slot.Occurrence));
            var scenarioTruck = slot.Truck;
            if (scenarioTruck.CapacityKg != planTruck.CapacityKg || scenarioTruck.MaxMachines != planTruck.MaxMachines)
            {
                Add("R1", planTruck.Id, null,
                    $"Ograničenja kamiona {planTruck.Id} u planu ({Kg(planTruck.CapacityKg)}, najviše strojeva {I(planTruck.MaxMachines)}) razlikuju se od scenarija ({Kg(scenarioTruck.CapacityKg)}, najviše strojeva {I(scenarioTruck.MaxMachines)}).");
            }

            return slot;
        }

        // R37 (amended, T31/T32): an unusable truck (capacity or max machines <= 0, or an ID that already
        // appeared) must have no actions; without a base (or with a base whose coordinates are not numeric)
        // no truck may have actions. Reported once per route.
        private void CheckRouteMayHaveActions(string? truckId, string label, TruckSlot? slot, int actionCount)
        {
            if (slot is { Usable: false })
            {
                Add("R37", truckId, null,
                    $"{Cap(label)} ne smije se koristiti ({slot.UnusableReason}), a broj radnji je {actionCount}.");
            }

            if (_index.BaseBlockingProblem is { } baseProblem)
            {
                Add("R37", truckId, null,
                    $"Lokacija baze '{_index.BaseLocationId}' {baseProblem}, pa nijedan kamion ne smije imati radnje, a {label} ima broj radnji {actionCount}.");
            }
        }

        // R3, R36: an unused truck has no stops, 0 km, 0 minutes and 0 orders.
        private RouteOutcome ValidateUnusedRoute(TruckRoute route, string? truckId, string label, TruckSlot? slot)
        {
            if (Differs(route.TotalKm, 0))
            {
                Add("R36", truckId, null, $"{Cap(label)} nema koraka, a prijavljuje {N(route.TotalKm)} km; očekivano 0 km.");
            }

            if (Differs(route.DrivingMinutes, 0))
            {
                Add("R36", truckId, null, $"{Cap(label)} nema koraka, a prijavljuje {N(route.DrivingMinutes)} min vožnje; očekivano 0.");
            }

            if (Differs(route.ActionMinutes, 0))
            {
                Add("R36", truckId, null, $"{Cap(label)} nema koraka, a prijavljuje {N(route.ActionMinutes)} min radnji; očekivano 0.");
            }

            if (route.OrderCount != 0)
            {
                Add("R36", truckId, null, $"{Cap(label)} nema koraka, a prijavljuje broj naloga {I(route.OrderCount)}; očekivano 0.");
            }

            return new RouteOutcome(slot, _index.BaseCoordinatesReliable, 0, 0, 0, _index.Base, 0, true);
        }

        // R37: an order with invalid data must be infeasible (K4), not planned. Reported once per order.
        // Stops refer to the first scenario order with their ID, so a duplicate order ID alone is no problem here
        // (the later orders with that ID are checked in CheckOrderCoverage / ExpectCategory); a missing base is
        // reported once per route (CheckRouteMayHaveActions), not once per order.
        private void CheckOrderData(TransportOrder order, string? truckId, int k, string label)
        {
            var problems = _index.FindOrderGroup(order.Id)?.Problems ?? [];
            if (problems.Count > 0 && _invalidDataReported.Add(order.Id ?? string.Empty))
            {
                Add("R37", truckId, k,
                    $"Nalog {order.Id} planiran je ({label}, korak {k + 1}), ali ima neispravne podatke ({string.Join("; ", problems)}); mora biti neizvediv s kategorijom K4.");
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
                    $"{Cap(label)}, korak {step}: nalog {order.Id} ponovno je preuzet (već preuzet u koraku {firstPickup + 1}).");
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
                    $"{Cap(label)}, korak {step}: stroj {machine.Id} ({Kg(machine.WeightKg)}) naloga {order.Id} teži je od nosivosti kamiona ({Kg(scenarioTruck.CapacityKg)}).");
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
                Add("R4", truckId, k, $"{Cap(label)}, korak {step}: nalog {order.Id} ponovno je isporučen.");
                return;
            }

            var pickups = _occurrences.GetValueOrDefault(order.Id) ?? [];
            var laterPickup = pickups.FirstOrDefault(p => p.Route == r && p.Action == StopAction.Pickup && p.Step > k);
            if (laterPickup is not null)
            {
                // R6: pickup strictly before delivery in the same route.
                Add("R6", truckId, k,
                    $"{Cap(label)}, korak {step}: nalog {order.Id} isporučen je prije preuzimanja (korak {laterPickup.Step + 1}).");
                return;
            }

            var otherPickup = pickups.FirstOrDefault(p => p.Route != r && p.Action == StopAction.Pickup);
            if (otherPickup is not null)
            {
                // R5: pickup and delivery of an order by the same truck.
                Add("R5", truckId, k,
                    $"Nalog {order.Id} isporučuje {label} (korak {step}), a preuzima ga {_labels[otherPickup.Route]} (korak {otherPickup.Step + 1}).");
                return;
            }

            // R6 (and R4): a delivery without any pickup.
            Add("R6", truckId, k, $"{Cap(label)}, korak {step}: nalog {order.Id} isporučen je bez preuzimanja.");
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
                        $"{Cap(label)}: vrijeme vožnje {N(drivingMinutes)} min > radni dan {N(max)} min ({ModeName}).");
                }
            }
            else
            {
                var total = drivingMinutes + actionMinutes;
                if (Exceeds(total, max))
                {
                    Add("R20", truckId, null,
                        $"{Cap(label)}: radno vrijeme {N(total)} min (vožnja {N(drivingMinutes)} + radnje {N(actionMinutes)}) > radni dan {N(max)} min ({ModeName}).");
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
                    $"Nalog {orderId} nalazi se na više ruta: {string.Join(", ", routes.Select(x => _labels[x]))}.");
            }
        }

        // R23: every input order is either planned or listed as infeasible - exactly one of the two, once.
        // Orders that share an ID (R37 duplicate) are counted per occurrence: for an ID that occurs n times
        // there must be exactly n records - the plan (stops refer to the first occurrence) plus infeasible
        // entries. Each infeasible entry is matched to one occurrence (see MatchEntries), which fixes the
        // category it must have (R33): the second and every later occurrence is K4.
        private void CheckOrderCoverage()
        {
            var entriesById = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < _infeasible.Count; i++)
            {
                var entry = _infeasible[i];
                if (entry?.Order is null)
                {
                    Add("R23", null, null, $"Neizvediva stavka {i + 1} nema nalog.");
                    continue;
                }

                var id = entry.Order.Id;
                var group = _index.FindOrderGroup(id);
                if (group is null)
                {
                    Add("R23", null, null, $"Neizvediv nalog '{id}' ne postoji u scenariju.");
                    continue;
                }

                if (!group.Occurrences.Contains(entry.Order))
                {
                    // R23: the entry must be an input order, not a different order that only shares the ID.
                    Add("R23", null, null,
                        $"Neizvediva stavka {i + 1} (stroj {entry.Order.MachineId}, {entry.Order.PickupLocationId} -> {entry.Order.DeliveryLocationId}) ne odgovara podacima nijednog naloga s ID-om '{id}' u scenariju.");
                }

                if (!entriesById.TryGetValue(group.Id, out var list))
                {
                    list = [];
                    entriesById[group.Id] = list;
                }

                list.Add(i);
            }

            foreach (var group in _index.OrderGroups)
            {
                var planned = _occurrences.TryGetValue(group.Id, out var refs);
                var entries = entriesById.GetValueOrDefault(group.Id) ?? [];
                MatchEntries(group, planned, entries);

                var n = group.Occurrences.Count;
                var count = entries.Count;
                if (n == 1)
                {
                    if (!planned && count == 0)
                    {
                        Add("R23", null, null, $"Nalog {group.Id} nije ni planiran ni naveden kao neizvediv.");
                    }

                    if (planned && count > 0)
                    {
                        var routes = string.Join(", ", refs!.Select(x => x.Route).Distinct().Select(x => _labels[x]));
                        Add("R23", _routes[refs![0].Route]?.Truck?.Id, refs[0].Step,
                            $"Nalog {group.Id} planiran je ({routes}) i ujedno naveden kao neizvediv.");
                    }

                    if (count > 1)
                    {
                        Add("R23", null, null, $"Nalog {group.Id} naveden je kao neizvediv {count} puta.");
                    }

                    continue;
                }

                var covered = (planned ? 1 : 0) + count;
                if (covered != n)
                {
                    var how = planned
                        ? $"planiran je jednom, a broj neizvedivih zapisa je {count}"
                        : $"nije planiran, a broj neizvedivih zapisa je {count}";
                    var missing = covered < n
                        ? $"bez plana i bez neizvedivog zapisa ostaje ih {n - covered}"
                        : $"zapisa je previše: {covered - n}";
                    Add("R23", planned ? _routes[refs![0].Route]?.Truck?.Id : null, planned ? refs![0].Step : null,
                        $"ID naloga {group.Id} pojavljuje se u scenariju {n} puta, ali {how}: {missing} (svako pojavljivanje treba točno jedan zapis; drugo i svako sljedeće kao K4).");
                }
            }
        }

        // Matches infeasible entries of one order ID to its scenario occurrences (R23, R33). A planned order is
        // the first occurrence, so the entries then belong to the later ones. Each occurrence, in input order,
        // first takes an entry with exactly its data; among several such entries (identical duplicates) the
        // first occurrence prefers one whose category fits it and is not K4 (the later ones must be K4), so
        // that "K4, K3" listed in any order is not misread. Entries whose data match no occurrence then fill
        // the occurrences still open, in order. Surplus entries stay unmatched (already an R23 violation) and
        // get no category check.
        private void MatchEntries(OrderGroup group, bool planned, List<int> entries)
        {
            var free = new List<int>(entries);
            var open = new List<int>();
            for (var o = planned ? 1 : 0; o < group.Occurrences.Count; o++)
            {
                var occurrence = group.Occurrences[o];
                var sameData = free.Where(e => _infeasible[e]!.Order == occurrence).ToList();
                if (sameData.Count == 0)
                {
                    open.Add(o);
                    continue;
                }

                var allowed = ExpectCategory(group, o).Allowed;
                var pick = sameData
                    .OrderBy(e => allowed.Contains(_infeasible[e]!.Reason) ? 0 : 1)
                    .ThenBy(e => o == 0 && _infeasible[e]!.Reason == InfeasibilityReason.InvalidOrderData ? 1 : 0)
                    .ThenBy(e => o > 0 && _infeasible[e]!.Reason != InfeasibilityReason.InvalidOrderData ? 1 : 0)
                    .First();
                _entryOccurrence[pick] = o;
                free.Remove(pick);
            }

            var foreign = free.Where(e => !group.Occurrences.Contains(_infeasible[e]!.Order)).ToList();
            for (var i = 0; i < Math.Min(open.Count, foreign.Count); i++)
            {
                _entryOccurrence[foreign[i]] = open[i];
            }
        }

        // R33: message and a category consistent with the data; R32: a feasible order must not be left out.
        // R40 (Croatian messages with numbers) is not checked beyond "message is not empty": its wording is
        // tested by the tests, not by the plan validator.
        private void CheckInfeasibleOrders()
        {
            var reportedR32 = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < _infeasible.Count; i++)
            {
                var entry = _infeasible[i];
                if (entry?.Order is null)
                {
                    continue;
                }

                var id = entry.Order.Id;
                if (string.IsNullOrWhiteSpace(entry.Message))
                {
                    Add("R33", null, null, $"Neizvediv nalog {id} nema poruku.");
                }

                var reasonDefined = Enum.IsDefined(entry.Reason);
                if (!reasonDefined)
                {
                    Add("R33", null, null, $"Neizvediv nalog {id} ima nedefiniranu kategoriju razloga {I((int)entry.Reason)}.");
                }

                var group = _index.FindOrderGroup(id);
                if (group is null || !_entryOccurrence.TryGetValue(i, out var occurrence))
                {
                    continue;
                }

                var expected = ExpectCategory(group, occurrence);
                if (reasonDefined && !expected.Allowed.Contains(entry.Reason))
                {
                    var allowed = string.Join(" ili ", expected.Allowed.Select(Code));
                    var which = group.Occurrences.Count > 1 ? $" (pojavljivanje {occurrence + 1} od {group.Occurrences.Count})" : string.Empty;
                    Add("R33", null, null,
                        $"Neizvediv nalog {id}{which} prijavljen je kao {Code(entry.Reason)} ({entry.Reason}), ali prema podacima treba biti {allowed}: {expected.Detail}.");
                }

                if (expected.FeasibleAlone && occurrence == 0 && !_occurrences.ContainsKey(group.Id) && reportedR32.Add(group.Id))
                {
                    CheckCouldBeAppended(group.Occurrences[0]);
                }
            }
        }

        // R33 with R37 (amended), R13: the category an infeasible order must have according to the data.
        private ExpectedCategory ExpectCategory(OrderGroup group, int occurrence)
        {
            var k4 = new HashSet<InfeasibilityReason> { InfeasibilityReason.InvalidOrderData };

            // R37: without a base every order is K4.
            if (_index.Base is null)
            {
                return new ExpectedCategory(k4, $"lokacija baze '{_index.BaseLocationId}' ne postoji, pa je svaki nalog K4", false);
            }

            // R37: the second and every later order with the same ID is K4.
            if (occurrence > 0)
            {
                return new ExpectedCategory(k4,
                    $"ID naloga '{group.Id}' je dupliciran; pojavljivanje {occurrence + 1} od {group.Occurrences.Count} mora biti K4", false);
            }

            // R37: unknown / duplicated / weightless machine, unknown / duplicated / non-numeric location,
            // pickup == delivery, machine already used by an earlier order.
            if (group.Problems.Count > 0)
            {
                return new ExpectedCategory(k4, string.Join("; ", group.Problems), false);
            }

            var order = group.Occurrences[0];
            var allowed = new HashSet<InfeasibilityReason>();
            if (!_index.BaseCoordinatesReliable)
            {
                // The base exists but its coordinates are not numeric or are ambiguous (duplicated base ID with
                // different coordinates): the spec does not say what happens, no time can be verified - any category.
                allowed.UnionWith(Enum.GetValues<InfeasibilityReason>());
                return new ExpectedCategory(allowed,
                    $"lokacija baze '{_index.BaseLocationId}' ima nenumeričke ili dvosmislene koordinate", false);
            }

            // T19, T28 and the ambiguous "machine already used" reading: K4 is allowed, not required.
            if (_index.SharesMachine(order) || group.PossiblyReusesMachine)
            {
                allowed.Add(InfeasibilityReason.InvalidOrderData);
            }

            // R13, R33 K1 (amended R37): heavier than every USABLE truck; unusable trucks do not count.
            var machine = _index.FindMachine(order.MachineId)!;
            var capable = _index.UsableTrucks.Where(t => t.Truck.CanCarry(machine)).Select(t => t.Truck).ToList();
            if (capable.Count == 0)
            {
                allowed.Add(InfeasibilityReason.ExceedsAllCapacities);
                var capacities = _index.UsableTrucks.Count == 0
                    ? "nema upotrebljivih kamiona"
                    : $"najveća nosivost upotrebljivog kamiona {Kg(_index.UsableTrucks.Max(t => t.Truck.CapacityKg))}";
                return new ExpectedCategory(allowed,
                    $"stroj {machine.Id} ({Kg(machine.WeightKg)}) teži je od nosivosti svih upotrebljivih kamiona ({capacities})", false);
            }

            // R33 K2: alone (base -> pickup -> delivery -> base) longer than the working day (R20 / R21).
            var needed = StandaloneMinutes(order);
            if (needed is null)
            {
                allowed.Add(InfeasibilityReason.ExceedsWorkday);
                allowed.Add(InfeasibilityReason.DoesNotFitWithOtherOrders);
                return new ExpectedCategory(allowed, "lokacija baze nije poznata, pa se vrijeme ne može izračunati", false);
            }

            var max = _options.MaxMinutes;
            if (Exceeds(needed.Value, max))
            {
                allowed.Add(InfeasibilityReason.ExceedsWorkday);
                return new ExpectedCategory(allowed,
                    $"sam (baza -> preuzimanje -> isporuka -> baza) nalog traži {N(needed.Value)} min > {N(max)} min ({ModeName})", false);
            }

            // R33 K3: feasible alone. R32 is checked only when no reading of the data makes the order K4.
            allowed.Add(InfeasibilityReason.DoesNotFitWithOtherOrders);
            return new ExpectedCategory(allowed,
                $"stroj {machine.Id} ({Kg(machine.WeightKg)}) stane u kamion {capable[0].Id} ({Kg(capable[0].CapacityKg)}), a sam nalog traži {N(needed.Value)} min <= {N(max)} min ({ModeName})",
                !group.PossiblyReusesMachine);
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
        // Only usable trucks are considered (amended R37, T32).
        private void CheckCouldBeAppended(TransportOrder order)
        {
            var baseLocation = _index.Base;
            var machine = _index.FindMachine(order.MachineId);
            var pickup = _index.FindLocation(order.PickupLocationId);
            var delivery = _index.FindLocation(order.DeliveryLocationId);
            if (baseLocation is null || !_index.BaseCoordinatesReliable || machine is null || pickup is null || delivery is null)
            {
                return;
            }

            foreach (var slot in _index.UsableTrucks)
            {
                var truck = slot.Truck;
                if (!truck.CanCarry(machine))
                {
                    continue;
                }

                var outcomes = _outcomes
                    .Where(o => o?.Slot is { } s && string.Equals(s.Truck.Id, truck.Id, StringComparison.Ordinal) && s.Occurrence == slot.Occurrence)
                    .ToList();
                if (outcomes.Count > 1)
                {
                    continue;
                }

                var outcome = outcomes.Count == 1
                    ? outcomes[0]!
                    : new RouteOutcome(slot, true, 0, 0, 0, baseLocation, 0, true);
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
                    $"Nalog {order.Id} naveden je kao neizvediv, ali stane na kraj rute kamiona {truck.Id}: {N(needed)} min <= {N(_options.MaxMinutes)} min ({ModeName}), stroj {Kg(machine.WeightKg)} <= nosivost {Kg(truck.CapacityKg)}; izvediv nalog ne smije biti izostavljen.");
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
                var expected = allKnown ? $"izračunate {N(recomputedSum)} km" : $"zbroja prijavljenih udaljenosti ruta {N(reportedSum)} km";
                Add("R16", null, null, $"Prijavljena ukupna udaljenost plana {N(_plan.TotalKm)} km razlikuje se od {expected}.");
            }
        }
    }
}
