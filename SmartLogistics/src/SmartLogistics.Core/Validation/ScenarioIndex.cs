using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Validation;

/// <summary>
/// Null-safe lookups into a <see cref="Scenario"/> for the plan validator. Duplicate IDs never throw:
/// the first occurrence wins and the duplicate is recorded as a global input problem (spec R37).
/// </summary>
internal sealed class ScenarioIndex
{
    private readonly Dictionary<string, Location> _locations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Machine> _machines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Truck> _trucks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TransportOrder> _orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _firstOrderByMachine = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ordersPerMachine = new(StringComparer.Ordinal);
    private readonly List<string> _globalProblems = [];
    private readonly List<Truck> _truckList = [];
    private readonly List<TransportOrder> _orderList = [];

    public ScenarioIndex(Scenario scenario)
    {
        BaseLocationId = scenario.BaseLocationId ?? string.Empty;

        foreach (var location in (scenario.Locations ?? []).Where(l => l is not null))
        {
            if (!_locations.TryAdd(location.Id ?? string.Empty, location))
            {
                _globalProblems.Add($"duplicate location ID '{location.Id}'");
            }

            if (!double.IsFinite(location.X) || !double.IsFinite(location.Y))
            {
                _globalProblems.Add($"location '{location.Id}' has non-numeric coordinates");
            }
        }

        foreach (var machine in (scenario.Machines ?? []).Where(m => m is not null))
        {
            if (!_machines.TryAdd(machine.Id ?? string.Empty, machine))
            {
                _globalProblems.Add($"duplicate machine ID '{machine.Id}'");
            }

            if (machine.WeightKg <= 0)
            {
                _globalProblems.Add($"machine '{machine.Id}' has weight {machine.WeightKg} kg <= 0");
            }
        }

        foreach (var truck in (scenario.Trucks ?? []).Where(t => t is not null))
        {
            if (_trucks.TryAdd(truck.Id ?? string.Empty, truck))
            {
                _truckList.Add(truck);
            }
            else
            {
                _globalProblems.Add($"duplicate truck ID '{truck.Id}'");
            }

            if (truck.CapacityKg <= 0 || truck.MaxMachines <= 0)
            {
                _globalProblems.Add(
                    $"truck '{truck.Id}' has capacity {truck.CapacityKg} kg / max {truck.MaxMachines} machines (must be > 0)");
            }
        }

        if (_truckList.Count == 0)
        {
            _globalProblems.Add("the scenario has no trucks");
        }

        foreach (var order in (scenario.Orders ?? []).Where(o => o is not null))
        {
            if (!_orders.TryAdd(order.Id ?? string.Empty, order))
            {
                _globalProblems.Add($"duplicate order ID '{order.Id}'");
                continue;
            }

            _orderList.Add(order);
            if (order.MachineId is not null)
            {
                _firstOrderByMachine.TryAdd(order.MachineId, order.Id ?? string.Empty);
                _ordersPerMachine[order.MachineId] = _ordersPerMachine.GetValueOrDefault(order.MachineId) + 1;
            }
        }

        Base = FindLocation(BaseLocationId);
        if (Base is null)
        {
            _globalProblems.Add($"base location '{BaseLocationId}' does not exist");
        }
    }

    public string BaseLocationId { get; }

    /// <summary>The depot; null when the base location ID is unknown (then no distance can be verified).</summary>
    public Location? Base { get; }

    /// <summary>Global input errors that must have stopped planning (spec R37).</summary>
    public IReadOnlyList<string> GlobalProblems => _globalProblems;

    /// <summary>Scenario trucks with distinct IDs, in input order.</summary>
    public IReadOnlyList<Truck> Trucks => _truckList;

    /// <summary>Scenario orders with distinct IDs, in input order.</summary>
    public IReadOnlyList<TransportOrder> Orders => _orderList;

    public Location? FindLocation(string? id) =>
        id is not null && _locations.TryGetValue(id, out var location) ? location : null;

    public Machine? FindMachine(string? id) =>
        id is not null && _machines.TryGetValue(id, out var machine) ? machine : null;

    public Truck? FindTruck(string? id) =>
        id is not null && _trucks.TryGetValue(id, out var truck) ? truck : null;

    public TransportOrder? FindOrder(string? id) =>
        id is not null && _orders.TryGetValue(id, out var order) ? order : null;

    /// <summary>
    /// Order-level data errors (spec R37): unknown machine or location, pickup == delivery,
    /// machine already used by an earlier order. Such an order must be infeasible with category K4.
    /// </summary>
    public IReadOnlyList<string> OrderDataProblems(TransportOrder order)
    {
        var problems = new List<string>();
        if (FindMachine(order.MachineId) is null)
        {
            problems.Add($"unknown machine '{order.MachineId}'");
        }

        if (FindLocation(order.PickupLocationId) is null)
        {
            problems.Add($"unknown pickup location '{order.PickupLocationId}'");
        }

        if (FindLocation(order.DeliveryLocationId) is null)
        {
            problems.Add($"unknown delivery location '{order.DeliveryLocationId}'");
        }

        if (string.Equals(order.PickupLocationId, order.DeliveryLocationId, StringComparison.Ordinal))
        {
            problems.Add($"pickup location equals delivery location '{order.PickupLocationId}'");
        }

        if (order.MachineId is not null
            && _firstOrderByMachine.TryGetValue(order.MachineId, out var firstOrderId)
            && !string.Equals(firstOrderId, order.Id, StringComparison.Ordinal))
        {
            problems.Add($"machine '{order.MachineId}' is already used by order '{firstOrderId}'");
        }

        return problems;
    }

    /// <summary>True when the order's machine appears in more than one order (any of them may be reported as K4).</summary>
    public bool SharesMachine(TransportOrder order) =>
        order.MachineId is not null && _ordersPerMachine.GetValueOrDefault(order.MachineId) > 1;
}
