using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Planning;

/// <summary>A <see cref="FeasibleOrder"/> with the planner's local indices.</summary>
/// <param name="ScenarioIndex">Position of the order in <see cref="Scenario.Orders"/> (fixed tie-break order, R39).</param>
/// <param name="PickupIndex">Index of the pickup location in <see cref="PreparedScenario.Locations"/>.</param>
/// <param name="DeliveryIndex">Index of the delivery location in <see cref="PreparedScenario.Locations"/>.</param>
/// <param name="CapableTrucks">Indices into <see cref="PreparedScenario.Trucks"/> of the trucks that may carry the machine, in scenario order.</param>
/// <param name="StandaloneMinutes">Workday minutes of base -> pickup -> delivery -> base, counted per the workday mode.</param>
internal sealed record PlannableOrder(
    int ScenarioIndex,
    TransportOrder Order,
    Machine Machine,
    int PickupIndex,
    int DeliveryIndex,
    IReadOnlyList<int> CapableTrucks,
    double StandaloneMinutes);

/// <summary>
/// Precomputation for the planner on top of <see cref="Feasibility.Check"/> (which does all input checks and the
/// K4 / K1 / K2 classification): the base and every location used by a plannable order get a local index, and all
/// legs between them are precomputed in whole tenths of a km (docs/odluke.md T24).
/// </summary>
internal sealed class PreparedScenario
{
    private PreparedScenario(
        FeasibilityReport report,
        PlanningOptions options,
        IReadOnlyList<Location> locations,
        int baseIndex,
        int[,] legs10,
        IReadOnlyList<PlannableOrder> plannable)
    {
        Report = report;
        Options = options;
        Locations = locations;
        BaseIndex = baseIndex;
        Legs10 = legs10;
        Plannable = plannable;
    }

    /// <summary>The input checks this precomputation is built on.</summary>
    public FeasibilityReport Report { get; }

    public PlanningOptions Options { get; }

    /// <summary>Usable trucks (one route each), in scenario order.</summary>
    public IReadOnlyList<Truck> Trucks => Report.UsableTrucks;

    /// <summary>Base first, then the locations of the plannable orders in first-use order; empty when there is no base.</summary>
    public IReadOnlyList<Location> Locations { get; }

    /// <summary>Index of the base in <see cref="Locations"/> (0), or -1 when there is no base (then nothing is plannable).</summary>
    public int BaseIndex { get; }

    /// <summary>Rounded legs in whole tenths of a km (8.5 km = 85), indexed like <see cref="Locations"/>.</summary>
    public int[,] Legs10 { get; }

    /// <summary>Orders that take part in the search, in scenario order.</summary>
    public IReadOnlyList<PlannableOrder> Plannable { get; }

    /// <summary>Workday minutes for a route with the given driving km (tenths) and number of actions.</summary>
    public double WorkdayMinutes(int km10, int actions) => Feasibility.WorkdayMinutes(Options, km10, actions);

    public bool FitsWorkday(double minutes) => Feasibility.FitsWorkday(Options, minutes);

    public static PreparedScenario Create(FeasibilityReport report, PlanningOptions options)
    {
        var locations = new List<Location>();
        var localIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        int Local(Location location)
        {
            if (!localIndex.TryGetValue(location.Id, out var index))
            {
                index = locations.Count;
                localIndex[location.Id] = index;
                locations.Add(location);
            }

            return index;
        }

        var baseIndex = report.Base is null ? -1 : Local(report.Base);
        var truckIndex = new Dictionary<Truck, int>(ReferenceEqualityComparer.Instance);
        for (var t = 0; t < report.UsableTrucks.Count; t++)
        {
            truckIndex[report.UsableTrucks[t]] = t;
        }

        var plannable = new List<PlannableOrder>(report.OrdersToPlan.Count);
        foreach (var order in report.OrdersToPlan)
        {
            plannable.Add(new PlannableOrder(
                order.ScenarioIndex,
                order.Order,
                order.Machine,
                Local(order.Pickup),
                Local(order.Delivery),
                order.CapableTrucks.Select(t => truckIndex[t]).ToArray(),
                order.StandaloneMinutes));
        }

        var legs10 = new int[locations.Count, locations.Count];
        for (var i = 0; i < locations.Count; i++)
        {
            for (var j = 0; j < locations.Count; j++)
            {
                var leg = Distance.Leg(locations[i], locations[j]);
                if (leg > Feasibility.MaxExactLegKm)
                {
                    // Size limit like T30 (T37): only reachable with extreme options, e.g. a very high speed.
                    throw new NotSupportedException(
                        $"The exact planner supports legs of at most {Feasibility.Format(Feasibility.MaxExactLegKm)} km; "
                        + $"{locations[i].Id} -> {locations[j].Id} is {Feasibility.Format(leg)} km.");
                }

                legs10[i, j] = Feasibility.ToTenths(leg);
            }
        }

        return new PreparedScenario(report, options, locations, baseIndex, legs10, plannable);
    }
}
