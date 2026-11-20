namespace SmartLogistics.Core.Domain;

/// <summary>A location with coordinates in kilometres.</summary>
public sealed record Location(string Id, double X, double Y);

/// <summary>A truck with its load limits.</summary>
/// <param name="CapacityKg">Maximum total weight on the truck after any action.</param>
/// <param name="MaxMachines">Maximum number of machines on the truck after any action.</param>
public sealed record Truck(string Id, int CapacityKg, int MaxMachines)
{
    /// <summary>True when the machine alone fits the truck's capacity (spec R13).</summary>
    public bool CanCarry(Machine machine) => machine.WeightKg <= CapacityKg;
}

/// <summary>A construction machine to be transported.</summary>
public sealed record Machine(string Id, int WeightKg);

/// <summary>
/// A transport order: move one machine from the pickup location to the delivery location.
/// Machine and locations are referenced by ID so that invalid input (e.g. an unknown machine)
/// can still be represented and reported (spec R37).
/// </summary>
public sealed record TransportOrder(string Id, string MachineId, string PickupLocationId, string DeliveryLocationId);

/// <summary>All input data for one planning run.</summary>
/// <param name="BaseLocationId">The depot where every route starts and ends (L0).</param>
public sealed record Scenario(
    string BaseLocationId,
    IReadOnlyList<Truck> Trucks,
    IReadOnlyList<Machine> Machines,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<TransportOrder> Orders);
