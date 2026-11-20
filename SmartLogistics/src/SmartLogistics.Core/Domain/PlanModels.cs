namespace SmartLogistics.Core.Domain;

/// <summary>What a truck does at a stop.</summary>
public enum StopAction
{
    Pickup,
    Delivery,
}

/// <summary>
/// One action of a route, in order. The final return to the base is not a stop (it is not an action, spec R19);
/// it is included in <see cref="TruckRoute.TotalKm"/>.
/// </summary>
/// <param name="LoadKgAfter">Total weight on the truck after this action.</param>
/// <param name="MachinesAfter">Number of machines on the truck after this action.</param>
/// <param name="LegKm">Rounded leg from the previous point (base or previous stop) to this stop.</param>
/// <param name="CumulativeKm">Kilometres driven from the base up to this stop.</param>
/// <param name="CumulativeMinutes">Elapsed time (driving + actions) when this action is finished.</param>
public sealed record Stop(
    string LocationId,
    StopAction Action,
    string OrderId,
    int LoadKgAfter,
    int MachinesAfter,
    double LegKm,
    double CumulativeKm,
    double CumulativeMinutes);

/// <summary>The route of one truck. An unused truck has no stops and 0 km (spec R3, R36).</summary>
/// <param name="TotalKm">All legs including the return leg to the base.</param>
/// <param name="DrivingMinutes">Driving time of all legs including the return leg.</param>
/// <param name="ActionMinutes">Number of actions x minutes per action.</param>
public sealed record TruckRoute(
    Truck Truck,
    IReadOnlyList<Stop> Stops,
    double TotalKm,
    double DrivingMinutes,
    double ActionMinutes,
    int OrderCount)
{
    public double TotalMinutes => DrivingMinutes + ActionMinutes;
}

/// <summary>Reason categories for an order that is not in the plan (spec R33).</summary>
public enum InfeasibilityReason
{
    /// <summary>K1 – the machine is heavier than every truck's capacity.</summary>
    ExceedsAllCapacities,

    /// <summary>K2 – even alone (base → pickup → delivery → base) the order exceeds the working day.</summary>
    ExceedsWorkday,

    /// <summary>K3 – feasible alone, but does not fit into the plan together with the other orders.</summary>
    DoesNotFitWithOtherOrders,

    /// <summary>K4 – invalid order data (unknown machine or location, pickup == delivery, machine reused).</summary>
    InvalidOrderData,
}

/// <summary>An order that could not be planned, with its reason category and a message containing the numbers.</summary>
public sealed record InfeasibleOrder(TransportOrder Order, InfeasibilityReason Reason, string Message);

/// <summary>The result of a planning run.</summary>
/// <param name="TotalKm">Sum of <see cref="TruckRoute.TotalKm"/> over all routes.</param>
public sealed record Plan(
    IReadOnlyList<TruckRoute> Routes,
    IReadOnlyList<InfeasibleOrder> InfeasibleOrders,
    double TotalKm);
