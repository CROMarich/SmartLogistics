using System.Globalization;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core;

/// <summary>
/// An order that passed every input check (not K4, K1 or K2) and has to be planned.
/// </summary>
/// <param name="ScenarioIndex">Position of the order in <see cref="Scenario.Orders"/> (fixed tie-break order, spec R39).</param>
/// <param name="Order">The order as given in the scenario.</param>
/// <param name="Machine">The (unique, positive-weight) machine of the order.</param>
/// <param name="Pickup">The (unique, valid) pickup location.</param>
/// <param name="Delivery">The (unique, valid) delivery location; never the same ID as <paramref name="Pickup"/>.</param>
/// <param name="CapableTrucks">Usable trucks that can carry the machine (<see cref="Truck.CanCarry"/>), in scenario order; never empty.</param>
/// <param name="StandaloneKm">Rounded km of the stand-alone route base -> pickup -> delivery -> base (legs rounded first, spec R14).</param>
/// <param name="StandaloneMinutes">Working-day minutes of the stand-alone route, counted per <see cref="PlanningOptions.WorkdayMode"/>; within the limit.</param>
public sealed record FeasibleOrder(
    int ScenarioIndex,
    TransportOrder Order,
    Machine Machine,
    Location Pickup,
    Location Delivery,
    IReadOnlyList<Truck> CapableTrucks,
    double StandaloneKm,
    double StandaloneMinutes);

/// <summary>An order that cannot be planned because of its own data (K4, K1 or K2), with its scenario position.</summary>
/// <param name="ScenarioIndex">Position of the order in <see cref="Scenario.Orders"/>.</param>
/// <param name="Infeasible">Category and Croatian message with the numbers (spec R33, R40).</param>
public sealed record RejectedOrder(int ScenarioIndex, InfeasibleOrder Infeasible);

/// <summary>
/// Result of <see cref="Feasibility.Check"/>: what the planner may use and what it must report.
/// Every non-null order of the scenario is either in <see cref="OrdersToPlan"/> or in <see cref="Rejected"/>
/// (exactly once); <c>null</c> entries of the scenario lists are skipped and reported in <see cref="Warnings"/>.
/// </summary>
/// <param name="Base">The depot, or <c>null</c> when it does not exist or is unusable (then every order is K4).</param>
/// <param name="UsableTrucks">
/// Trucks that may get actions, in scenario order: first occurrence of each ID with capacity &gt; 0 and max machines &gt; 0
/// (spec R37). The planner returns one route per usable truck.
/// </param>
/// <param name="OrdersToPlan">Orders that are feasible on their own, in scenario order.</param>
/// <param name="Rejected">Orders that are infeasible on their own (K4, then K1, then K2), in scenario order.</param>
/// <param name="Warnings">
/// Croatian messages about defects of the scenario itself (unusable trucks, duplicate or invalid machines and
/// locations, missing base, skipped null entries), in input order; meant for the UI.
/// </param>
public sealed record FeasibilityReport(
    Location? Base,
    IReadOnlyList<Truck> UsableTrucks,
    IReadOnlyList<FeasibleOrder> OrdersToPlan,
    IReadOnlyList<RejectedOrder> Rejected,
    IReadOnlyList<string> Warnings)
{
    /// <summary>The <see cref="InfeasibleOrder"/> part of <see cref="Rejected"/>, in scenario order.</summary>
    public IReadOnlyList<InfeasibleOrder> InfeasibleOrders { get; } = Rejected.Select(r => r.Infeasible).ToArray();
}

/// <summary>
/// Input checks before planning (spec R33, R37 as amended on 21.11.2026., R40; docs/odluke.md T28, T31, T32).
/// Invalid scenario data never throws: it makes single orders infeasible or trucks unusable. Only invalid
/// <see cref="PlanningOptions"/> (a programming error, not user input) and <c>null</c> arguments throw
/// <see cref="ArgumentException"/>.
/// <para>
/// Order classification, in scenario order (the first matching category wins):
/// K4 – invalid data: missing or unusable base (a base duplicated with differing coordinates is unusable, one
/// duplicated with identical coordinates is used, T35), missing/unknown/duplicated machine or machine with weight
/// &lt;= 0, missing/unknown/duplicated location or location with non-numeric coordinates as pickup or delivery,
/// pickup == delivery, missing order ID, duplicated order ID (the second and every later order with that ID),
/// machine already used by an earlier order (the first order with a distinct ID keeps the machine, T28);
/// K1 – no usable truck can carry the machine;
/// K2 – the stand-alone route base -> pickup -> delivery -> base exceeds the working day;
/// otherwise the order is returned in <see cref="FeasibilityReport.OrdersToPlan"/>.
/// </para>
/// <para>Complexity: O(T + M + L + N) dictionary operations for T trucks, M machines, L locations and N orders.</para>
/// </summary>
public static class Feasibility
{
    /// <summary>
    /// Longest leg (km) the exact planner computes in whole tenths of a km: with at most 63 plannable orders a plan has
    /// at most 189 legs, so all sums stay below <see cref="int.MaxValue"/> tenths (docs/odluke.md T30, T37). With
    /// the default options such a leg can never be part of a plannable order (it alone exceeds the working day).
    /// </summary>
    public const double MaxExactLegKm = 1_000_000;

    /// <summary>Floating-point guard for the inclusive working-day limit (spec R35); same value as the validator uses.</summary>
    internal const double LimitEpsilon = 1e-6;

    /// <summary>Checks the options and the scenario and classifies every order.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> are invalid (see <see cref="ValidateOptions"/>).</exception>
    public static FeasibilityReport Check(Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ValidateOptions(options);

        var warnings = new List<string>();
        var locations = IndexLocations(scenario.Locations, warnings);
        var machines = IndexMachines(scenario.Machines, warnings);
        var trucks = SelectUsableTrucks(scenario.Trucks, warnings);

        Location? baseLocation = null;
        string? baseProblem = null;
        if (scenario.BaseLocationId is null)
        {
            baseProblem = "baza nije zadana";
        }
        else if (locations.Consistent.TryGetValue(scenario.BaseLocationId, out baseLocation))
        {
            warnings.Add($"Baza {scenario.BaseLocationId} definirana je više puta s istim koordinatama – koristi se kao baza, "
                + "ali nalozi s preuzimanjem ili isporukom u njoj se ne planiraju.");
        }
        else if (locations.Bad.TryGetValue(scenario.BaseLocationId, out var reason))
        {
            baseProblem = $"baza {scenario.BaseLocationId} je neispravna ({reason})";
        }
        else if (!locations.Valid.TryGetValue(scenario.BaseLocationId, out baseLocation))
        {
            baseProblem = $"baza {scenario.BaseLocationId} ne postoji među lokacijama";
        }

        if (baseProblem is not null)
        {
            warnings.Add($"Neispravna baza: {baseProblem} – nijedan nalog se ne planira, kamioni nemaju radnji.");
        }

        if (trucks.Count == 0)
        {
            warnings.Add("Nema nijednog upotrebljivog kamiona – nijedan nalog se ne može planirati.");
        }

        var toPlan = new List<FeasibleOrder>();
        var rejected = new List<RejectedOrder>();
        var orders = scenario.Orders;
        if (orders is null)
        {
            warnings.Add("Popis naloga nedostaje – nema naloga za planiranje.");
            orders = [];
        }

        // Same keys as the validator's scenario index: the first order with a given ID (null counts as "") is the
        // "real" one; only such orders claim a machine (T28).
        var orderIds = new HashSet<string>(StringComparer.Ordinal);
        var firstOrderByMachine = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            if (order is null)
            {
                warnings.Add($"Nalog na mjestu {i + 1} popisa nedostaje (null) – preskočen.");
                continue;
            }

            var problems = new List<string>();
            if (baseProblem is not null)
            {
                problems.Add(baseProblem);
            }

            // A missing ID makes the order impossible to reference from a stop (Stop.OrderId is not nullable): K4 (T35).
            // For duplicates and machine claims it counts as the ID "", the same key the validator uses.
            if (order.Id is null)
            {
                problems.Add("nalog nema ID");
            }

            var isFirstWithId = orderIds.Add(order.Id ?? string.Empty);
            if (!isFirstWithId && order.Id is not null)
            {
                problems.Add($"ID naloga {OrderName(order)} već je korišten u ranijem nalogu (duplikat)");
            }

            var machine = ResolveMachine(order.MachineId, machines, problems);
            var pickup = ResolveLocation(order.PickupLocationId, "preuzimanja", locations, problems);
            var delivery = ResolveLocation(order.DeliveryLocationId, "isporuke", locations, problems);
            if (string.Equals(order.PickupLocationId, order.DeliveryLocationId, StringComparison.Ordinal))
            {
                problems.Add($"lokacija preuzimanja jednaka je lokaciji isporuke ({order.PickupLocationId ?? "nije zadana"})");
            }

            if (order.MachineId is not null)
            {
                if (firstOrderByMachine.TryGetValue(order.MachineId, out var firstOrderId))
                {
                    problems.Add($"stroj {order.MachineId} već je korišten u ranijem nalogu {firstOrderId}");
                }
                else if (isFirstWithId)
                {
                    firstOrderByMachine[order.MachineId] = order.Id ?? string.Empty;
                }
            }

            if (problems.Count > 0)
            {
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.InvalidOrderData,
                    $"Neispravni podaci naloga {OrderName(order)}: {string.Join("; ", problems)}.")));
                continue;
            }

            var capable = trucks.Where(t => t.CanCarry(machine!)).ToArray();
            if (capable.Length == 0)
            {
                var limits = trucks.Count == 0
                    ? "nema upotrebljivih kamiona"
                    : string.Join(", ", trucks.Select(t => $"{t.Id} {t.CapacityKg} kg"));
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.ExceedsAllCapacities,
                    $"Stroj {machine!.Id} ({machine.WeightKg} kg) teži je od nosivosti svih kamiona ({limits}).")));
                continue;
            }

            var standaloneKm = StandaloneKm(baseLocation!, pickup!, delivery!);
            var minutes = WorkdayMinutesForKm(options, standaloneKm, 2);
            if (!FitsWorkday(options, minutes))
            {
                var trucksText = string.Join(", ", capable.Select(t => $"{t.Id} {t.CapacityKg} kg"));
                rejected.Add(new RejectedOrder(i, new InfeasibleOrder(order, InfeasibilityReason.ExceedsWorkday,
                    $"Ni sam ne stane u radni dan ({ModeDescription(options)}, limit {Format(options.MaxMinutes)} min): "
                    + $"samostalna ruta {baseLocation!.Id} -> {pickup!.Id} -> {delivery!.Id} -> {baseLocation.Id} ima {Format(standaloneKm)} km "
                    + $"i traži {Format(minutes)} min na svakom kamionu koji može nositi stroj {machine!.Id} ({machine.WeightKg} kg; {trucksText}).")));
                continue;
            }

            toPlan.Add(new FeasibleOrder(i, order, machine!, pickup!, delivery!, capable, standaloneKm, minutes));
        }

        return new FeasibilityReport(baseLocation, trucks, toPlan, rejected, warnings);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> listing every problem of the options: undefined workday mode,
    /// speed not a positive number, minutes per action or working day not a number &gt;= 0 (docs/odluke.md T27, T32).
    /// A speed &gt; 0 is what makes the shortest route also the fastest one (the planner's working-day proof).
    /// </summary>
    public static void ValidateOptions(PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
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

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid planning options (programming error, spec R37), planning not started:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => "- " + e)),
                nameof(options));
        }
    }

    /// <summary>Converts a rounded leg (one decimal) to whole tenths of a km; exact because the leg has one decimal (T24).</summary>
    internal static int ToTenths(double km) => checked((int)Math.Round(km * 10, MidpointRounding.AwayFromZero));

    /// <summary>Working-day minutes of a route with the given driving km (tenths) and number of actions (spec R20, R21).</summary>
    internal static double WorkdayMinutes(PlanningOptions options, int km10, int actions) =>
        WorkdayMinutesForKm(options, km10 / 10.0, actions);

    /// <summary>Working-day minutes of a route with the given driving km and number of actions (spec R20, R21).</summary>
    internal static double WorkdayMinutesForKm(PlanningOptions options, double km, int actions) =>
        options.WorkdayMode == WorkdayMode.DrivingOnly
            ? options.DrivingMinutes(km)
            : options.DrivingMinutes(km) + actions * options.MinutesPerAction;

    /// <summary>
    /// Km of base -> pickup -> delivery -> base, legs rounded first (R14). Summed exactly in tenths when every leg is
    /// at most <see cref="MaxExactLegKm"/>; otherwise in <c>double</c> (possibly infinite), which is only used to show
    /// that the order exceeds the working day. Finite coordinates never give NaN.
    /// </summary>
    private static double StandaloneKm(Location baseLocation, Location pickup, Location delivery)
    {
        var legs = new[] { Distance.Leg(baseLocation, pickup), Distance.Leg(pickup, delivery), Distance.Leg(delivery, baseLocation) };
        return legs.All(l => l <= MaxExactLegKm)
            ? legs.Sum(ToTenths) / 10.0
            : legs.Sum();
    }

    /// <summary>Inclusive working-day check with a floating-point guard (spec R35).</summary>
    internal static bool FitsWorkday(PlanningOptions options, double minutes) => minutes <= options.MaxMinutes + LimitEpsilon;

    /// <summary>Croatian description of the workday mode for messages, e.g. "Total: vožnja + radnje".</summary>
    internal static string ModeDescription(PlanningOptions options) => options.WorkdayMode == WorkdayMode.DrivingOnly
        ? $"{nameof(WorkdayMode.DrivingOnly)}: samo vožnja"
        : $"{nameof(WorkdayMode.Total)}: vožnja + radnje";

    /// <summary>Culture-independent number format for messages (at most three decimals).</summary>
    internal static string Format(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);

    /// <summary>Valid entries by ID (unique and usable) and the reason for every ID that exists but must not be used.</summary>
    /// <param name="Consistent">
    /// Duplicated IDs whose occurrences are all identical (with valid data): the first occurrence. Used only for the
    /// base, which is then unambiguous as the start and end of every route (docs/odluke.md T35).
    /// </param>
    private sealed record Index<T>(Dictionary<string, T> Valid, Dictionary<string, string> Bad, Dictionary<string, T> Consistent);

    private static Index<Location> IndexLocations(IReadOnlyList<Location>? list, List<string> warnings)
    {
        var valid = new Dictionary<string, Location>(StringComparer.Ordinal);
        var bad = new Dictionary<string, string>(StringComparer.Ordinal);
        var consistent = new Dictionary<string, Location>(StringComparer.Ordinal);
        if (list is null)
        {
            warnings.Add("Popis lokacija nedostaje – nalozi i baza nemaju lokacija.");
            return new Index<Location>(valid, bad, consistent);
        }

        var counts = CountIds(list, l => l?.Id);
        foreach (var group in list.Where(l => l?.Id is not null && counts[l.Id] > 1).GroupBy(l => l.Id, StringComparer.Ordinal))
        {
            var first = group.First();
            if (IsValidCoordinate(first.X) && IsValidCoordinate(first.Y) && group.All(l => l.X.Equals(first.X) && l.Y.Equals(first.Y)))
            {
                consistent[group.Key] = first;
            }
        }

        for (var i = 0; i < list.Count; i++)
        {
            var location = list[i];
            if (location?.Id is null)
            {
                warnings.Add($"Lokacija na mjestu {i + 1} popisa nedostaje ili nema ID – preskočena.");
                continue;
            }

            if (counts[location.Id] > 1)
            {
                if (bad.TryAdd(location.Id, $"lokacija {location.Id} definirana je {counts[location.Id]} puta"))
                {
                    warnings.Add($"Lokacija {location.Id} definirana je {counts[location.Id]} puta – nalozi koji je koriste se ne planiraju.");
                }

                continue;
            }

            if (!IsValidCoordinate(location.X) || !IsValidCoordinate(location.Y))
            {
                bad[location.Id] = $"lokacija {location.Id} ima nebrojčane koordinate ({Format(location.X)}, {Format(location.Y)})";
                warnings.Add($"Lokacija {location.Id} ima nebrojčane koordinate ({Format(location.X)}, {Format(location.Y)}) – nalozi koji je koriste se ne planiraju.");
                continue;
            }

            valid[location.Id] = location;
        }

        return new Index<Location>(valid, bad, consistent);
    }

    private static Index<Machine> IndexMachines(IReadOnlyList<Machine>? list, List<string> warnings)
    {
        var valid = new Dictionary<string, Machine>(StringComparer.Ordinal);
        var bad = new Dictionary<string, string>(StringComparer.Ordinal);
        if (list is null)
        {
            warnings.Add("Popis strojeva nedostaje – nijedan nalog nema stroj.");
            return new Index<Machine>(valid, bad, []);
        }

        var counts = CountIds(list, m => m?.Id);
        for (var i = 0; i < list.Count; i++)
        {
            var machine = list[i];
            if (machine?.Id is null)
            {
                warnings.Add($"Stroj na mjestu {i + 1} popisa nedostaje ili nema ID – preskočen.");
                continue;
            }

            if (counts[machine.Id] > 1)
            {
                if (bad.TryAdd(machine.Id, $"stroj {machine.Id} definiran je {counts[machine.Id]} puta"))
                {
                    warnings.Add($"Stroj {machine.Id} definiran je {counts[machine.Id]} puta – nalozi koji ga koriste se ne planiraju.");
                }

                continue;
            }

            if (machine.WeightKg <= 0)
            {
                bad[machine.Id] = $"masa stroja {machine.Id} je {machine.WeightKg} kg (mora biti > 0)";
                warnings.Add($"Stroj {machine.Id} ima masu {machine.WeightKg} kg (mora biti > 0) – nalozi koji ga koriste se ne planiraju.");
                continue;
            }

            valid[machine.Id] = machine;
        }

        return new Index<Machine>(valid, bad, []);
    }

    private static List<Truck> SelectUsableTrucks(IReadOnlyList<Truck>? list, List<string> warnings)
    {
        var usable = new List<Truck>();
        if (list is null)
        {
            warnings.Add("Popis kamiona nedostaje.");
            return usable;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            var truck = list[i];
            if (truck?.Id is null)
            {
                warnings.Add($"Kamion na mjestu {i + 1} popisa nedostaje ili nema ID – ne koristi se.");
                continue;
            }

            if (!seen.Add(truck.Id))
            {
                warnings.Add($"Kamion {truck.Id} naveden je više puta – koristi se samo prvi, kamion na mjestu {i + 1} popisa se ne koristi.");
                continue;
            }

            if (truck.CapacityKg <= 0 || truck.MaxMachines <= 0)
            {
                warnings.Add($"Kamion {truck.Id} ima nosivost {truck.CapacityKg} kg i najveći broj strojeva {truck.MaxMachines} (oboje mora biti > 0) – ne koristi se.");
                continue;
            }

            usable.Add(truck);
        }

        return usable;
    }

    private static Machine? ResolveMachine(string? id, Index<Machine> machines, List<string> problems)
    {
        if (id is null)
        {
            problems.Add("stroj nije zadan");
            return null;
        }

        if (machines.Bad.TryGetValue(id, out var reason))
        {
            problems.Add(reason);
            return null;
        }

        if (!machines.Valid.TryGetValue(id, out var machine))
        {
            problems.Add($"stroj {id} ne postoji");
            return null;
        }

        return machine;
    }

    private static Location? ResolveLocation(string? id, string role, Index<Location> locations, List<string> problems)
    {
        if (id is null)
        {
            problems.Add($"lokacija {role} nije zadana");
            return null;
        }

        if (locations.Bad.TryGetValue(id, out var reason))
        {
            problems.Add($"lokacija {role}: {reason}");
            return null;
        }

        if (!locations.Valid.TryGetValue(id, out var location))
        {
            problems.Add($"lokacija {role} {id} ne postoji");
            return null;
        }

        return location;
    }

    private static Dictionary<string, int> CountIds<T>(IReadOnlyList<T> list, Func<T, string?> id)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in list)
        {
            var key = id(item);
            if (key is not null)
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return counts;
    }

    private static bool IsValidCoordinate(double value) => double.IsFinite(value);

    private static string OrderName(TransportOrder order) => string.IsNullOrEmpty(order.Id) ? "(bez ID-a)" : order.Id;
}
