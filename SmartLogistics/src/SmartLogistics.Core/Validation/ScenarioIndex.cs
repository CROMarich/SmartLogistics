using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Validation;

/// <summary>
/// Null-safe lookups into a <see cref="Scenario"/> for the plan validator, plus the facts about invalid input
/// that the amended spec R37 (decision T31, T32) derives from the data. Building the index never throws:
/// duplicate IDs are kept as separate occurrences, null lists and null elements are skipped.
/// </summary>
/// <remarks>
/// Since phase 5 invalid input never stops planning (R37): an invalid order is infeasible with category K4,
/// an unusable truck has no actions, and without a base no truck has actions. The index therefore no longer
/// produces "global problems"; it classifies every order, truck and the base instead.
/// </remarks>
internal sealed class ScenarioIndex
{
    private readonly Dictionary<string, List<Location>> _locations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Machine>> _machines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TruckSlot>> _trucks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OrderGroup> _orderGroups = new(StringComparer.Ordinal);
    private readonly List<OrderGroup> _orderGroupList = [];
    private readonly List<TruckSlot> _usableTrucks = [];
    private readonly Dictionary<string, HashSet<string>> _machineOrderIds = new(StringComparer.Ordinal);

    public ScenarioIndex(Scenario scenario)
    {
        BaseLocationId = scenario.BaseLocationId ?? string.Empty;

        foreach (var location in (scenario.Locations ?? []).Where(l => l is not null))
        {
            Occurrences(_locations, location.Id).Add(location);
        }

        foreach (var machine in (scenario.Machines ?? []).Where(m => m is not null))
        {
            Occurrences(_machines, machine.Id).Add(machine);
        }

        foreach (var truck in (scenario.Trucks ?? []).Where(t => t is not null))
        {
            var slots = Occurrences(_trucks, truck.Id);
            var slot = new TruckSlot(truck, slots.Count, UnusableReason(truck, slots.Count));
            slots.Add(slot);
            if (slot.Usable)
            {
                _usableTrucks.Add(slot);
            }
        }

        Base = FindLocation(BaseLocationId);
        BaseCoordinatesReliable = HasReliableCoordinates(BaseLocationId);
        BaseBlockingProblem = Base is null
            ? "does not exist"
            : !double.IsFinite(Base.X) || !double.IsFinite(Base.Y) ? "has non-numeric coordinates" : null;

        IndexOrders((scenario.Orders ?? []).Where(o => o is not null).ToList());
    }

    public string BaseLocationId { get; }

    /// <summary>The depot (first location with the base ID); null when the base location does not exist.</summary>
    public Location? Base { get; }

    /// <summary>True when the base exists and its coordinates are numeric and unambiguous, so legs from / to it can be verified.</summary>
    public bool BaseCoordinatesReliable { get; }

    /// <summary>
    /// Why no truck may have actions because of the base (R37: no base; T32-style: base without numeric
    /// coordinates, so no kilometre can be computed); null when the base allows planning.
    /// </summary>
    public string? BaseBlockingProblem { get; }

    /// <summary>Trucks that may be used (R37, T32): first occurrence of their ID, capacity > 0 and max machines > 0, in input order.</summary>
    public IReadOnlyList<TruckSlot> UsableTrucks => _usableTrucks;

    /// <summary>Scenario orders grouped by ID, in the order of the first occurrence of each ID.</summary>
    public IReadOnlyList<OrderGroup> OrderGroups => _orderGroupList;

    public Location? FindLocation(string? id) =>
        _locations.TryGetValue(Key(id), out var list) ? list[0] : null;

    public Machine? FindMachine(string? id) =>
        _machines.TryGetValue(Key(id), out var list) ? list[0] : null;

    /// <summary>All scenario trucks with this ID, in input order (more than one only for duplicate IDs).</summary>
    public IReadOnlyList<TruckSlot> TruckOccurrences(string? id) =>
        _trucks.TryGetValue(Key(id), out var list) ? list : [];

    /// <summary>The order a stop refers to: always the first scenario order with that ID.</summary>
    public TransportOrder? FindOrder(string? id) => FindOrderGroup(id)?.Occurrences[0];

    public OrderGroup? FindOrderGroup(string? id) =>
        _orderGroups.TryGetValue(Key(id), out var group) ? group : null;

    /// <summary>True when the location exists exactly with numeric coordinates (all occurrences of a duplicated ID agree).</summary>
    public bool HasReliableCoordinates(string? id)
    {
        if (!_locations.TryGetValue(Key(id), out var list))
        {
            return false;
        }

        var first = list[0];
        return double.IsFinite(first.X) && double.IsFinite(first.Y)
            && list.All(l => l.X.Equals(first.X) && l.Y.Equals(first.Y));
    }

    /// <summary>
    /// True when the order's machine also appears in an order with a different ID (T19, T28: K4 is allowed for
    /// any of them). Later orders with the same ID do not count: they are K4 as duplicates, not this order.
    /// </summary>
    public bool SharesMachine(TransportOrder order) =>
        _machineOrderIds.TryGetValue(Key(order.MachineId), out var ids) && ids.Any(id => id != Key(order.Id));

    private static string Key(string? id) => id ?? string.Empty;

    private static List<T> Occurrences<T>(Dictionary<string, List<T>> map, string? id)
    {
        if (!map.TryGetValue(Key(id), out var list))
        {
            list = [];
            map[Key(id)] = list;
        }

        return list;
    }

    // R37, T32: a truck with capacity or max machines <= 0, or with an ID that appeared before, is not used.
    private static string? UnusableReason(Truck truck, int occurrence)
    {
        var reasons = new List<string>();
        if (occurrence > 0)
        {
            reasons.Add($"truck ID '{truck.Id}' already appeared before (occurrence {occurrence + 1})");
        }

        if (truck.CapacityKg <= 0)
        {
            reasons.Add($"capacity {truck.CapacityKg} kg <= 0");
        }

        if (truck.MaxMachines <= 0)
        {
            reasons.Add($"max machines {truck.MaxMachines} <= 0");
        }

        return reasons.Count == 0 ? null : string.Join("; ", reasons);
    }

    private void IndexOrders(List<TransportOrder> orders)
    {
        var occurrences = new Dictionary<string, List<TransportOrder>>(StringComparer.Ordinal);
        var firstPosition = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var p = 0; p < orders.Count; p++)
        {
            var order = orders[p];
            Occurrences(occurrences, order.Id).Add(order);
            firstPosition.TryAdd(Key(order.Id), p);
            if (!_machineOrderIds.TryGetValue(Key(order.MachineId), out var ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                _machineOrderIds[Key(order.MachineId)] = ids;
            }

            ids.Add(Key(order.Id));
        }

        // First occurrences with no location problem, by machine, in input order (for "machine already used").
        var cleanUsersByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var p = 0; p < orders.Count; p++)
        {
            var order = orders[p];
            var id = Key(order.Id);
            if (firstPosition[id] != p)
            {
                continue;
            }

            var machineKey = Key(order.MachineId);
            var problems = new List<string>();
            problems.AddRange(MachineProblems(order));
            var locationProblems = LocationProblems(order);
            problems.AddRange(locationProblems);

            // R37 "machine already used in an earlier order". Definite when an earlier order that is the first of
            // its ID and has no location problem uses the same machine: then the machine is taken under every
            // reading (only valid orders or any order count as "using" it). Otherwise, when only an invalid or a
            // duplicate-ID order uses the machine earlier, the reading is ambiguous: K4 is allowed, not required.
            var possiblyReused = false;
            if (cleanUsersByMachine.TryGetValue(machineKey, out var earlierOrderId))
            {
                problems.Add($"machine '{order.MachineId}' is already used by earlier order '{earlierOrderId}'");
            }
            else
            {
                possiblyReused = orders.Take(p).Any(o => string.Equals(Key(o.MachineId), machineKey, StringComparison.Ordinal));
                if (locationProblems.Count == 0)
                {
                    cleanUsersByMachine[machineKey] = order.Id ?? string.Empty;
                }
            }

            var group = new OrderGroup(id, occurrences[id], problems, possiblyReused);
            _orderGroups[id] = group;
            _orderGroupList.Add(group);
        }
    }

    // R37: unknown machine, duplicated machine ID, machine weight <= 0.
    private List<string> MachineProblems(TransportOrder order)
    {
        var problems = new List<string>();
        if (!_machines.TryGetValue(Key(order.MachineId), out var machines))
        {
            problems.Add($"unknown machine '{order.MachineId}'");
            return problems;
        }

        if (machines.Count > 1)
        {
            problems.Add($"machine ID '{order.MachineId}' is duplicated ({machines.Count} machines)");
        }

        if (machines[0].WeightKg <= 0)
        {
            problems.Add($"machine '{order.MachineId}' has weight {machines[0].WeightKg} kg <= 0");
        }

        return problems;
    }

    // R37: unknown or duplicated location, non-numeric coordinates, pickup == delivery.
    private List<string> LocationProblems(TransportOrder order)
    {
        var problems = new List<string>();
        AddLocationProblems(problems, "pickup", order.PickupLocationId);
        AddLocationProblems(problems, "delivery", order.DeliveryLocationId);
        if (string.Equals(order.PickupLocationId, order.DeliveryLocationId, StringComparison.Ordinal))
        {
            problems.Add($"pickup location equals delivery location '{order.PickupLocationId}'");
        }

        return problems;
    }

    private void AddLocationProblems(List<string> problems, string role, string? id)
    {
        if (!_locations.TryGetValue(Key(id), out var locations))
        {
            problems.Add($"unknown {role} location '{id}'");
            return;
        }

        if (locations.Count > 1)
        {
            problems.Add($"{role} location ID '{id}' is duplicated ({locations.Count} locations)");
        }

        if (locations.Any(l => !double.IsFinite(l.X) || !double.IsFinite(l.Y)))
        {
            problems.Add($"{role} location '{id}' has non-numeric coordinates");
        }
    }
}

/// <summary>One scenario truck; <see cref="Occurrence"/> is its 0-based position among trucks with the same ID.</summary>
/// <param name="UnusableReason">Why the truck must not be used (R37, T32); null for a usable truck.</param>
internal sealed record TruckSlot(Truck Truck, int Occurrence, string? UnusableReason)
{
    public bool Usable => UnusableReason is null;
}

/// <summary>All scenario orders with one ID, in input order.</summary>
/// <param name="Problems">
/// Definite data errors of the first occurrence (R37): such an order must be K4 and must not be planned.
/// The second and every later occurrence is always K4 (duplicate order ID).
/// </param>
/// <param name="PossiblyReusesMachine">
/// The first occurrence's machine is used by an earlier order that is itself invalid or a duplicate ID,
/// so "machine already used in an earlier order" depends on the reading: K4 is allowed, planning is allowed.
/// </param>
internal sealed record OrderGroup(
    string Id,
    IReadOnlyList<TransportOrder> Occurrences,
    IReadOnlyList<string> Problems,
    bool PossiblyReusesMachine);
