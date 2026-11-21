using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels.Transport;

namespace SmartLogistics.Web.ViewModels;

/// <summary>Small coloured label for a truck (A, B, ...).</summary>
public sealed record TruckBadgeViewModel(string Text, string CssClass);

/// <summary>Row of _TrucksTable.</summary>
/// <param name="CarriableMachines">"5 od 10 strojeva".</param>
public sealed record TruckRowViewModel(TruckBadgeViewModel Badge, string Capacity, string MaxMachines, string CarriableMachines);

/// <summary>Row of _MachinesTable.</summary>
/// <param name="UsedIn">Orders that transport the machine, e.g. "N03" or "N03, N11"; "–" when unused.</param>
public sealed record MachineRowViewModel(string Id, string Weight, IReadOnlyList<TruckBadgeViewModel> FitsOn, bool FitsNowhere, string UsedIn);

/// <summary>Row of _LocationsTable.</summary>
public sealed record LocationRowViewModel(string Id, string X, string Y, string Note, string DistanceFromBase, bool IsBase);

/// <summary>Row of _OrdersTable.</summary>
/// <param name="DataWarning">
/// Why the planner rejects the order as K4 – the message of <see cref="Feasibility.Check"/>, the same check the planner
/// runs – or null for a valid order.
/// </param>
/// <param name="CanEdit">False for an order without ID (the store cannot address it).</param>
public sealed record OrderRowViewModel(
    string Id,
    string Machine,
    string Weight,
    string Pickup,
    string Delivery,
    string DirectLeg,
    IReadOnlyList<TruckBadgeViewModel> Trucks,
    string? DataWarning,
    bool CanEdit)
{
    /// <summary>Category label shown with <see cref="DataWarning"/> ("K4"), from the same mapping as the plan page.</summary>
    public string DataWarningCode => Transport.PlanMapper.ReasonCode(InfeasibilityReason.InvalidOrderData);
}

/// <summary>Model of _OrdersTable: read-only on the input data page, with edit / delete buttons on the orders page.</summary>
public sealed record OrdersTableViewModel(IReadOnlyList<OrderRowViewModel> Rows, bool ShowActions);

public sealed record DistanceCellViewModel(string Text, bool IsDiagonal);

public sealed record DistanceMatrixRowViewModel(string LocationId, IReadOnlyList<DistanceCellViewModel> Cells);

public sealed record DistanceMatrixViewModel(IReadOnlyList<string> Headers, IReadOnlyList<DistanceMatrixRowViewModel> Rows);

/// <summary>An order on the input data map: an arrow from pickup to delivery, coloured by which trucks can carry it.</summary>
/// <param name="CssClass">"sl-map-order sl-map-order-any", "-truck-a", "-truck-b" or "-none".</param>
public sealed record MapArrowViewModel(string Id, string X1, string Y1, string X2, string Y2, string CssClass, string Title);

/// <summary>Map of locations and orders (_LocationMap); geometry from <see cref="MapGeometry"/>.</summary>
public sealed record LocationMapViewModel(
    MapFrameViewModel Frame,
    string PointRadius,
    IReadOnlyList<MapLocationViewModel> Locations,
    IReadOnlyList<MapArrowViewModel> Arrows,
    IReadOnlyList<MapLegendEntryViewModel> Legend);

/// <summary>Whether the current scenario differs from data/scenario.json (_ScenarioStatus).</summary>
/// <param name="Text">"Scenarij je izmijenjen u aplikaciji: 2 izmjene u odnosu na data/scenario.json." or the unchanged text.</param>
/// <param name="CssClass">"sl-callout sl-callout-warning" when modified, otherwise "sl-callout sl-callout-info".</param>
/// <param name="ResetPage">Razor page that handles the "Vrati izvorne podatke" POST (handler "Reset").</param>
public sealed record ScenarioStatusViewModel(
    bool IsModified,
    int ChangeCount,
    string Text,
    string Detail,
    string CssClass,
    string Icon,
    string ResetPage);

/// <summary>The "Ulazni podaci" page (Pages/Index).</summary>
public sealed record InputDataPageViewModel(
    PageHeaderViewModel Header,
    string? Error,
    ScenarioStatusViewModel Status,
    IReadOnlyList<string> Facts,
    IReadOnlyList<StatTileViewModel> Stats,
    IReadOnlyList<TruckRowViewModel> Trucks,
    IReadOnlyList<MachineRowViewModel> Machines,
    IReadOnlyList<LocationRowViewModel> Locations,
    OrdersTableViewModel Orders,
    DistanceMatrixViewModel? Matrix,
    LocationMapViewModel? Map);

/// <summary>
/// Maps a scenario to the input data views (tables, summary tiles, distance matrix, map of orders). No planning; invalid
/// orders (K4) and usable trucks come from <see cref="Feasibility.Check"/> with the default options, the single source the
/// planner uses too (docs/odluke.md T63 a). Robust to any data (spec R37): missing references are shown as text, never thrown.
/// Truck colours follow the truck's position in the scenario, like on the plan (<see cref="PlanMapper.TruckColor"/>).
/// </summary>
public static class InputDataViewModelFactory
{
    private const string Dash = "–";

    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Ulazni podaci",
        "Kamioni, strojevi, lokacije i nalozi trenutnog scenarija (data/scenario.json i izmjene u aplikaciji), matrica udaljenosti i karta naloga.");

    /// <summary>The input data page for the current state of the store.</summary>
    public static InputDataPageViewModel Create(ScenarioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var status = CreateStatus(snapshot, "/Index");
        if (snapshot.LoadError is not null)
        {
            return new InputDataPageViewModel(Header, snapshot.LoadError, status, [], [], [], [], [], new OrdersTableViewModel([], false), null, null);
        }

        var scenario = snapshot.Scenario;
        var lookup = new Lookup(scenario);

        return new InputDataPageViewModel(
            Header,
            Error: null,
            status,
            CreateFacts(PlanningOptions.Default),
            CreateStats(scenario, lookup),
            CreateTruckRows(lookup),
            CreateMachineRows(lookup),
            CreateLocationRows(scenario, lookup),
            new OrdersTableViewModel(CreateOrderRows(lookup), ShowActions: false),
            CreateMatrix(lookup.Matrix),
            CreateMap(scenario, lookup));
    }

    /// <summary>
    /// Modified / unchanged callout with source and load time, e.g. "2 izmjene u odnosu na data/scenario.json".
    /// </summary>
    /// <param name="resetPage">Page whose "Reset" POST handler the button calls ("/Index" or "/Orders/Index").</param>
    public static ScenarioStatusViewModel CreateStatus(ScenarioSnapshot snapshot, string resetPage)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(resetPage);

        var detail = $"Izvor: {snapshot.Source} · učitano {snapshot.LoadedAt:dd.MM.yyyy. HH:mm} · izmjene vrijede do ponovnog pokretanja aplikacije.";
        if (!snapshot.IsModified)
        {
            return new ScenarioStatusViewModel(false, 0, $"Podaci odgovaraju datoteci {snapshot.Source} (bez izmjena).", detail,
                "sl-callout sl-callout-info", "info", resetPage);
        }

        var count = snapshot.ChangeCount;
        return new ScenarioStatusViewModel(true, count,
            $"Scenarij je izmijenjen u aplikaciji: {count} {Format.Plural(count, "izmjena", "izmjene", "izmjena")} u odnosu na {snapshot.Source}.",
            detail, "sl-callout sl-callout-warning", "alert", resetPage);
    }

    /// <summary>Order rows with edit / delete buttons for the orders page.</summary>
    public static OrdersTableViewModel CreateOrdersTable(Scenario scenario, bool showActions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new OrdersTableViewModel(CreateOrderRows(new Lookup(scenario)), showActions);
    }

    // Rules that turn the coordinates into km and minutes (defaults of the planner).
    private static IReadOnlyList<string> CreateFacts(PlanningOptions options) =>
    [
        "dionica = euklidska udaljenost zaokružena na 0,1 km (R14)",
        $"{Format.Coordinate(options.SpeedKmh)} km/h · 1 km = {Format.Coordinate(options.DrivingMinutes(1))} min",
        $"svaka radnja {Format.Coordinate(options.MinutesPerAction)} min",
        $"radni dan {Format.Clock(options.MaxMinutes)} (zadano, mijenja se na planu)",
    ];

    private static IReadOnlyList<StatTileViewModel> CreateStats(Scenario scenario, Lookup lookup)
    {
        var totalCapacity = lookup.Trucks.Sum(t => (long)t.CapacityKg);
        var totalWeight = lookup.Machines.Sum(m => (long)m.WeightKg);

        // How many valid orders can be carried by only one usable truck, e.g. "5 samo kamion B"; K4 orders are counted apart.
        var valid = lookup.Orders.Where(o => !lookup.IsInvalid(o)).ToList();
        var restricted = valid
            .Select(o => lookup.Machine(o.MachineId))
            .Where(m => m is not null)
            .Select(m => lookup.CarriersOf(m!))
            .Where(carriers => carriers.Count == 1)
            .GroupBy(carriers => carriers[0].Id)
            .Select(g => $"{g.Count()} samo kamion {g.Key}")
            .ToList();
        var nowhere = valid.Count(o => lookup.Machine(o.MachineId) is { } m && lookup.CarriersOf(m).Count == 0);
        if (nowhere > 0)
        {
            restricted.Add($"{nowhere} ni na jedan kamion");
        }

        var invalid = lookup.Orders.Count - valid.Count;
        if (invalid > 0)
        {
            restricted.Add($"{invalid} s neispravnim podacima (K4)");
        }

        return
        [
            new("Kamioni", Format.Integer(lookup.Trucks.Count), $"ukupna nosivost {Format.Kg((int)Math.Min(int.MaxValue, totalCapacity))}", "truck", "sl-stat-tile-accent"),
            new("Strojevi", Format.Integer(lookup.Machines.Count), $"ukupna masa {Format.Kg((int)Math.Min(int.MaxValue, totalWeight))}", "layers", "sl-stat-tile-accent"),
            new("Lokacije", Format.Integer(lookup.Locations.Count), $"baza {scenario.BaseLocationId}", "map", "sl-stat-tile-neutral"),
            new("Nalozi", Format.Integer(lookup.Orders.Count),
                restricted.Count == 0 ? "svaki nalog može svaki kamion" : string.Join(", ", restricted), "route",
                restricted.Count == 0 ? "sl-stat-tile-ok" : "sl-stat-tile-warning"),
        ];
    }

    private static IReadOnlyList<TruckRowViewModel> CreateTruckRows(Lookup lookup) =>
        lookup.Trucks
            .Select(truck =>
            {
                // A count, not a list: the per-machine detail is in the machines table ("5 od 10 strojeva").
                var carriable = lookup.Machines.Count(truck.CanCarry);
                var all = lookup.Machines.Count;
                return new TruckRowViewModel(
                    lookup.Badge(truck),
                    Format.Kg(truck.CapacityKg),
                    Format.Integer(truck.MaxMachines),
                    $"{carriable} od {all} {Format.Plural(all, "stroja", "stroja", "strojeva")}");
            })
            .ToList();

    private static IReadOnlyList<MachineRowViewModel> CreateMachineRows(Lookup lookup) =>
        lookup.Machines
            .Select(machine =>
            {
                var fitsOn = lookup.CarriersOf(machine).Select(lookup.Badge).ToList();
                var usedIn = lookup.Orders.Where(o => o.MachineId == machine.Id).Select(o => o.Id ?? Dash).ToList();
                return new MachineRowViewModel(machine.Id, Format.Kg(machine.WeightKg), fitsOn, FitsNowhere: fitsOn.Count == 0,
                    usedIn.Count == 0 ? Dash : string.Join(", ", usedIn));
            })
            .ToList();

    private static IReadOnlyList<LocationRowViewModel> CreateLocationRows(Scenario scenario, Lookup lookup)
    {
        var baseKnown = lookup.Location(scenario.BaseLocationId) is not null;
        return lookup.Locations
            .Select(location =>
            {
                var isBase = location.Id == scenario.BaseLocationId;
                return new LocationRowViewModel(
                    location.Id, Format.Coordinate(location.X), Format.Coordinate(location.Y),
                    isBase ? "baza" : string.Empty,
                    baseKnown && location.Id is not null ? Format.Km(lookup.Matrix[scenario.BaseLocationId, location.Id]) : Dash,
                    isBase);
            })
            .ToList();
    }

    private static IReadOnlyList<OrderRowViewModel> CreateOrderRows(Lookup lookup) =>
        lookup.Orders
            .Select(order =>
            {
                var machine = lookup.Machine(order.MachineId);
                var pickupKnown = lookup.Location(order.PickupLocationId) is not null;
                var deliveryKnown = lookup.Location(order.DeliveryLocationId) is not null;

                // K4 exactly as the planner reports it (spec R37, T28, T63 a); an invalid order has no possible truck.
                var invalid = lookup.InvalidReason(order);
                IReadOnlyList<TruckBadgeViewModel> trucks = machine is null || invalid is not null
                    ? []
                    : lookup.CarriersOf(machine).Select(lookup.Badge).ToList();
                return new OrderRowViewModel(
                    string.IsNullOrEmpty(order.Id) ? Dash : order.Id,
                    order.MachineId ?? Dash,
                    machine is null ? Dash : Format.Kg(machine.WeightKg),
                    order.PickupLocationId ?? Dash,
                    order.DeliveryLocationId ?? Dash,
                    pickupKnown && deliveryKnown ? Format.Km(lookup.Matrix[order.PickupLocationId!, order.DeliveryLocationId!]) : Dash,
                    trucks,
                    invalid,
                    CanEdit: !string.IsNullOrEmpty(order.Id));
            })
            .ToList();

    private static DistanceMatrixViewModel? CreateMatrix(DistanceMatrix matrix)
    {
        if (matrix.Locations.Count == 0)
        {
            return null;
        }

        var headers = matrix.Locations.Select(l => l.Id).ToList();
        var rows = matrix.Locations
            .Select(from => new DistanceMatrixRowViewModel(
                from.Id,
                matrix.Locations
                    .Select(to => new DistanceCellViewModel(Format.Number(matrix[from.Id, to.Id]), IsDiagonal: from.Id == to.Id))
                    .ToList()))
            .ToList();

        return new DistanceMatrixViewModel(headers, rows);
    }

    // Every order as an arrow from pickup to delivery, coloured by which trucks can carry it.
    private static LocationMapViewModel? CreateMap(Scenario scenario, Lookup lookup)
    {
        if (lookup.Locations.Count == 0)
        {
            return null;
        }

        var arrows = new List<MapArrowViewModel>();
        foreach (var order in lookup.Orders)
        {
            if (lookup.Location(order.PickupLocationId) is not { } from
                || lookup.Location(order.DeliveryLocationId) is not { } to
                || MapGeometry.Shorten(from, to) is not { } line)
            {
                continue;
            }

            arrows.Add(new MapArrowViewModel(
                order.Id ?? Dash,
                Format.Svg(line.X1), Format.Svg(line.Y1), Format.Svg(line.X2), Format.Svg(line.Y2),
                $"sl-map-order sl-map-order-{OrderColor(lookup.Machine(order.MachineId), lookup)}",
                $"{order.Id}: {order.MachineId} {order.PickupLocationId} → {order.DeliveryLocationId}"));
        }

        var legend = new List<MapLegendEntryViewModel>
        {
            new("baza", "sl-map-swatch sl-map-swatch-base"),
            new("nalog – više kamiona", "sl-map-swatch sl-map-swatch-any"),
        };
        legend.AddRange(lookup.Trucks
            .Where(t => lookup.Color(t) != "any")
            .Select(t => new MapLegendEntryViewModel($"nalog – samo kamion {t.Id}", $"sl-map-swatch sl-map-swatch-{lookup.Color(t)}")));
        if (arrows.Any(a => a.CssClass.EndsWith("-none", StringComparison.Ordinal)))
        {
            legend.Add(new MapLegendEntryViewModel("nalog – nijedan kamion ili nepoznat stroj", "sl-map-swatch sl-map-swatch-none"));
        }

        return new LocationMapViewModel(
            MapGeometry.Frame(lookup.Locations),
            Format.Svg(MapGeometry.PointRadius),
            MapGeometry.Locations(lookup.Locations, scenario.BaseLocationId),
            arrows,
            legend);
    }

    private static string OrderColor(Machine? machine, Lookup lookup)
    {
        if (machine is null)
        {
            return "none";
        }

        var carriers = lookup.Trucks.Where(t => t.CanCarry(machine)).ToList();
        return carriers.Count switch
        {
            0 => "none",
            1 => lookup.Color(carriers[0]),
            _ => "any",
        };
    }

    /// <summary>
    /// Lookups built once per page; nulls are skipped. For display (weight, coordinates, km) the first occurrence of a
    /// duplicated ID is shown; whether an order is valid and which trucks may be used comes only from
    /// <see cref="Feasibility.Check"/> (a duplicated machine or location makes its orders K4 there).
    /// </summary>
    private sealed class Lookup
    {
        private readonly Scenario _scenario;
        private readonly Dictionary<string, Machine> _machines;
        private readonly Dictionary<string, Location> _locations;
        private readonly Dictionary<string, string> _colors = [];
        private readonly Dictionary<TransportOrder, string> _invalid = new(ReferenceEqualityComparer.Instance);
        private readonly IReadOnlyList<Truck> _usableTrucks;

        public Lookup(Scenario scenario)
        {
            _scenario = scenario;
            Trucks = (scenario.Trucks ?? []).Where(t => t is not null).ToList();
            Machines = (scenario.Machines ?? []).Where(m => m is not null).ToList();
            Locations = (scenario.Locations ?? []).Where(l => l is not null).ToList();
            Orders = (scenario.Orders ?? []).Where(o => o is not null).ToList();
            _machines = Machines.Where(m => m.Id is not null).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
            _locations = Locations.Where(l => l.Id is not null).DistinctBy(l => l.Id).ToDictionary(l => l.Id);
            Matrix = new DistanceMatrix(_locations.Values.ToList());

            // The planner's own input check. Rejected is indexed by the position in scenario.Orders (nulls included), so
            // two orders with the same ID are told apart; the order object is the key.
            var report = Feasibility.Check(scenario, PlanningOptions.Default);
            var scenarioOrders = scenario.Orders ?? [];
            _usableTrucks = report.UsableTrucks;
            foreach (var rejected in report.Rejected.Where(r => r.Infeasible.Reason == InfeasibilityReason.InvalidOrderData))
            {
                _invalid[scenarioOrders[rejected.ScenarioIndex]] = rejected.Infeasible.Message;
            }
        }

        public IReadOnlyList<Truck> Trucks { get; }

        public IReadOnlyList<Machine> Machines { get; }

        public IReadOnlyList<Location> Locations { get; }

        public IReadOnlyList<TransportOrder> Orders { get; }

        public DistanceMatrix Matrix { get; }

        public Machine? Machine(string? id) => id is not null ? _machines.GetValueOrDefault(id) : null;

        public Location? Location(string? id) => id is not null ? _locations.GetValueOrDefault(id) : null;

        /// <summary>The K4 message of <see cref="Feasibility.Check"/> for this order (by position, not by ID), or null.</summary>
        public string? InvalidReason(TransportOrder order) => _invalid.GetValueOrDefault(order);

        public bool IsInvalid(TransportOrder order) => _invalid.ContainsKey(order);

        /// <summary>Usable trucks (spec R37: first of each ID, capacity and max machines &gt; 0) that can carry the machine.</summary>
        public IReadOnlyList<Truck> CarriersOf(Machine machine) => _usableTrucks.Where(t => t.CanCarry(machine)).ToList();

        // Same colour rule as the plan; cached because PlanMapper.TruckColor builds its lookups on every call.
        public string Color(Truck truck)
        {
            var key = truck.Id ?? string.Empty;
            if (!_colors.TryGetValue(key, out var color))
            {
                color = PlanMapper.TruckColor(truck, _scenario);
                _colors[key] = color;
            }

            return color;
        }

        public TruckBadgeViewModel Badge(Truck truck)
        {
            var color = Color(truck);
            return new TruckBadgeViewModel(truck.Id, $"sl-badge sl-badge-{(color == "any" ? "neutral" : color)}");
        }
    }
}
