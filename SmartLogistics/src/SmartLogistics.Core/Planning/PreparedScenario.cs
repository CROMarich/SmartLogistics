using System.Globalization;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Planning;

/// <summary>An order that passed all data checks (not K4, K1 or K2) and takes part in the assignment search.</summary>
/// <param name="ScenarioIndex">Position of the order in <see cref="Scenario.Orders"/> (fixed tie-break order, R39).</param>
/// <param name="PickupIndex">Index of the pickup location in <see cref="Scenario.Locations"/>.</param>
/// <param name="DeliveryIndex">Index of the delivery location in <see cref="Scenario.Locations"/>.</param>
/// <param name="CapableTrucks">Indices of the trucks that may carry the machine (<see cref="Truck.CanCarry"/>), in scenario order.</param>
/// <param name="StandaloneMinutes">Workday minutes of base -> pickup -> delivery -> base, counted per the workday mode.</param>
internal sealed record PlannableOrder(
    int ScenarioIndex,
    TransportOrder Order,
    Machine Machine,
    int PickupIndex,
    int DeliveryIndex,
    IReadOnlyList<int> CapableTrucks,
    double StandaloneMinutes);

/// <summary>An order that is infeasible because of its own data (K4, K1, K2), with its scenario position.</summary>
internal sealed record RejectedOrder(int ScenarioIndex, InfeasibleOrder Infeasible);

/// <summary>
/// Input checks and precomputation for the planner (spec R33, R37, docs/odluke.md T24, T25):
/// global errors throw <see cref="ArgumentException"/>; every order is classified as K4 / K1 / K2 or plannable;
/// all legs are precomputed in whole tenths of a kilometre.
/// </summary>
internal sealed class PreparedScenario
{
    /// <summary>Floating-point guard for the inclusive workday limit (spec R35); same value as the validator uses.</summary>
    internal const double LimitEpsilon = 1e-6;

    private PreparedScenario(
        Scenario scenario,
        PlanningOptions options,
        int baseIndex,
        int[,] legs10,
        IReadOnlyList<PlannableOrder> plannable,
        IReadOnlyList<RejectedOrder> rejected)
    {
        Scenario = scenario;
        Options = options;
        BaseIndex = baseIndex;
        Legs10 = legs10;
        Plannable = plannable;
        Rejected = rejected;
    }

    public Scenario Scenario { get; }

    public PlanningOptions Options { get; }

    public IReadOnlyList<Truck> Trucks => Scenario.Trucks;

    public IReadOnlyList<Location> Locations => Scenario.Locations;

    /// <summary>Index of the base location in <see cref="Scenario.Locations"/>.</summary>
    public int BaseIndex { get; }

    /// <summary>Rounded legs in whole tenths of a km (8.5 km = 85), indexed like <see cref="Scenario.Locations"/>.</summary>
    public int[,] Legs10 { get; }

    /// <summary>Orders that take part in the search, in scenario order.</summary>
    public IReadOnlyList<PlannableOrder> Plannable { get; }

    /// <summary>Orders infeasible on their own (K4, K1, K2), in scenario order.</summary>
    public IReadOnlyList<RejectedOrder> Rejected { get; }

    public string ModeName => Options.WorkdayMode == WorkdayMode.DrivingOnly
        ? nameof(WorkdayMode.DrivingOnly)
        : nameof(WorkdayMode.Total);

    /// <summary>Converts a rounded leg (one decimal) to whole tenths; exact because the leg has one decimal.</summary>
    public static int ToTenths(double km) => checked((int)Math.Round(km * 10, MidpointRounding.AwayFromZero));

    public static string Format(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);

    /// <summary>Workday minutes for a route with the given driving km (tenths) and number of actions.</summary>
    public double WorkdayMinutes(int km10, int actions) => Options.WorkdayMode == WorkdayMode.DrivingOnly
        ? Options.DrivingMinutes(km10 / 10.0)
        : Options.DrivingMinutes(km10 / 10.0) + actions * Options.MinutesPerAction;

    public bool FitsWorkday(double minutes) => minutes <= Options.MaxMinutes + LimitEpsilon;

    public static PreparedScenario Create(Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        CheckOptions(options, errors);

        var trucks = scenario.Trucks;
        var machines = scenario.Machines;
        var locations = scenario.Locations;
        var orders = scenario.Orders;
        if (trucks is null) errors.Add("the truck list is missing (null)");
        if (machines is null) errors.Add("the machine list is missing (null)");
        if (locations is null) errors.Add("the location list is missing (null)");
        if (orders is null) errors.Add("the order list is missing (null)");

        var locationIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < (locations?.Count ?? 0); i++)
        {
            var location = locations![i];
            if (location?.Id is null)
            {
                errors.Add($"location {i + 1} is missing or has no ID");
                continue;
            }

            if (!locationIndex.TryAdd(location.Id, i))
            {
                errors.Add($"duplicate location ID '{location.Id}'");
            }

            if (!double.IsFinite(location.X) || !double.IsFinite(location.Y))
            {
                errors.Add($"location '{location.Id}' has non-numeric coordinates");
            }
        }

        var machineById = new Dictionary<string, Machine>(StringComparer.Ordinal);
        for (var i = 0; i < (machines?.Count ?? 0); i++)
        {
            var machine = machines![i];
            if (machine?.Id is null)
            {
                errors.Add($"machine {i + 1} is missing or has no ID");
                continue;
            }

            if (!machineById.TryAdd(machine.Id, machine))
            {
                errors.Add($"duplicate machine ID '{machine.Id}'");
            }

            if (machine.WeightKg <= 0)
            {
                errors.Add($"machine '{machine.Id}' has weight {machine.WeightKg} kg <= 0");
            }
        }

        var truckIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < (trucks?.Count ?? 0); i++)
        {
            var truck = trucks![i];
            if (truck?.Id is null)
            {
                errors.Add($"truck {i + 1} is missing or has no ID");
                continue;
            }

            if (!truckIds.Add(truck.Id))
            {
                errors.Add($"duplicate truck ID '{truck.Id}'");
            }

            if (truck.CapacityKg <= 0 || truck.MaxMachines <= 0)
            {
                errors.Add($"truck '{truck.Id}' has capacity {truck.CapacityKg} kg / max {truck.MaxMachines} machines (must be > 0)");
            }
        }

        if (trucks is not null && trucks.Count == 0)
        {
            errors.Add("the scenario has no trucks");
        }

        var orderIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < (orders?.Count ?? 0); i++)
        {
            var order = orders![i];
            if (order?.Id is null)
            {
                errors.Add($"order {i + 1} is missing or has no ID");
                continue;
            }

            if (!orderIds.Add(order.Id))
            {
                errors.Add($"duplicate order ID '{order.Id}'");
            }
        }

        var baseIndex = -1;
        if (scenario.BaseLocationId is null || !locationIndex.TryGetValue(scenario.BaseLocationId, out baseIndex))
        {
            errors.Add($"base location '{scenario.BaseLocationId}' does not exist");
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid planning input (spec R37: scenario or planning options), planning not started:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => "- " + e)));
        }

        var legs10 = new int[locations!.Count, locations.Count];
        for (var i = 0; i < locations.Count; i++)
        {
            for (var j = 0; j < locations.Count; j++)
            {
                legs10[i, j] = ToTenths(Distance.Leg(locations[i], locations[j]));
            }
        }

        var prepared = new List<PlannableOrder>();
        var rejected = new List<RejectedOrder>();
        var firstOrderByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
        var context = new PreparedScenario(scenario, options, baseIndex, legs10, prepared, rejected);

        for (var i = 0; i < orders!.Count; i++)
        {
            var order = orders[i];
            var problems = new List<string>();
            Machine? machine = null;
            if (order.MachineId is null || !machineById.TryGetValue(order.MachineId, out machine))
            {
                problems.Add($"unknown machine '{order.MachineId}'");
            }

            var pickupIndex = -1;
            if (order.PickupLocationId is null || !locationIndex.TryGetValue(order.PickupLocationId, out pickupIndex))
            {
                problems.Add($"unknown pickup location '{order.PickupLocationId}'");
            }

            var deliveryIndex = -1;
            if (order.DeliveryLocationId is null || !locationIndex.TryGetValue(order.DeliveryLocationId, out deliveryIndex))
            {
                problems.Add($"unknown delivery location '{order.DeliveryLocationId}'");
            }

            if (string.Equals(order.PickupLocationId, order.DeliveryLocationId, StringComparison.Ordinal))
            {
                problems.Add($"pickup location equals delivery location '{order.PickupLocationId}'");
            }

            // The first order of a machine keeps it; every later order with the same machine is K4 (spec R37).
            if (order.MachineId is not null)
            {
                if (firstOrderByMachine.TryGetValue(order.MachineId, out var firstOrderId))
                {
                    problems.Add($"machine '{order.MachineId}' is already used by order '{firstOrderId}'");
                }
                else
                {
                    firstOrderByMachine[order.MachineId] = order.Id;
                }
            }

            if (problems.Count > 0)
            {
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.InvalidOrderData,
                    $"K4: order {order.Id} has invalid data: {string.Join("; ", problems)}.")));
                continue;
            }

            var capable = new List<int>();
            for (var t = 0; t < trucks!.Count; t++)
            {
                if (trucks[t].CanCarry(machine!))
                {
                    capable.Add(t);
                }
            }

            if (capable.Count == 0)
            {
                var limits = string.Join(", ", trucks.Select(t => $"{t.Id} {t.CapacityKg} kg"));
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.ExceedsAllCapacities,
                    $"K1: machine {machine!.Id} of order {order.Id} weighs {machine.WeightKg} kg, more than the capacity of every truck ({limits}).")));
                continue;
            }

            var standaloneKm10 = checked(legs10[baseIndex, pickupIndex] + legs10[pickupIndex, deliveryIndex] + legs10[deliveryIndex, baseIndex]);
            var standaloneMinutes = context.WorkdayMinutes(standaloneKm10, 2);
            if (!context.FitsWorkday(standaloneMinutes))
            {
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.ExceedsWorkday,
                    $"K2: alone ({scenario.BaseLocationId} -> {order.PickupLocationId} -> {order.DeliveryLocationId} -> {scenario.BaseLocationId}, "
                    + $"{Format(standaloneKm10 / 10.0)} km) order {order.Id} needs {Format(standaloneMinutes)} min > {Format(options.MaxMinutes)} min "
                    + $"({context.ModeName}) on every truck that can carry machine {machine!.Id} ({machine.WeightKg} kg).")));
                continue;
            }

            prepared.Add(new PlannableOrder(i, order, machine!, pickupIndex, deliveryIndex, capable, standaloneMinutes));
        }

        return context;
    }

    private static void CheckOptions(PlanningOptions options, List<string> errors)
    {
        if (!Enum.IsDefined(options.WorkdayMode))
        {
            errors.Add($"undefined workday mode {(int)options.WorkdayMode}");
        }

        if (!double.IsFinite(options.SpeedKmh) || options.SpeedKmh <= 0)
        {
            errors.Add($"speed {Format(options.SpeedKmh)} km/h must be a positive number");
        }

        if (!double.IsFinite(options.MinutesPerAction) || options.MinutesPerAction < 0)
        {
            errors.Add($"minutes per action {Format(options.MinutesPerAction)} must be a number >= 0");
        }

        if (!double.IsFinite(options.MaxMinutes) || options.MaxMinutes < 0)
        {
            errors.Add($"maximum workday {Format(options.MaxMinutes)} min must be a number >= 0");
        }
    }
}
