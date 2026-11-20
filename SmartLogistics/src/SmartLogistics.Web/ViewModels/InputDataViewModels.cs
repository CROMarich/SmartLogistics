using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>Small coloured label for a truck (A, B, ...).</summary>
public sealed record TruckBadgeViewModel(string Text, string CssClass);

public sealed record TruckRowViewModel(TruckBadgeViewModel Badge, string Capacity, string MaxMachines, string CarriableMachines);

public sealed record MachineRowViewModel(string Id, string Weight, IReadOnlyList<TruckBadgeViewModel> FitsOn, bool FitsNowhere);

public sealed record LocationRowViewModel(string Id, string X, string Y, string Note, string DistanceFromBase, bool IsBase);

public sealed record OrderRowViewModel(
    string Id,
    string Machine,
    string Weight,
    string Pickup,
    string Delivery,
    string DirectLeg,
    IReadOnlyList<TruckBadgeViewModel> Trucks,
    string? DataWarning);

public sealed record DistanceCellViewModel(string Text, bool IsDiagonal);

public sealed record DistanceMatrixRowViewModel(string LocationId, IReadOnlyList<DistanceCellViewModel> Cells);

public sealed record DistanceMatrixViewModel(IReadOnlyList<string> Headers, IReadOnlyList<DistanceMatrixRowViewModel> Rows);

/// <summary>A location on the SVG map; coordinates are already in SVG space (y flipped) and formatted.</summary>
public sealed record MapPointViewModel(string Id, string Cx, string Cy, string LabelX, string LabelY, bool IsBase);

/// <summary>An order drawn as an arrow from pickup to delivery.</summary>
public sealed record MapArrowViewModel(string OrderId, string X1, string Y1, string X2, string Y2, string CssClass, string Title);

public sealed record MapViewModel(string ViewBox, string PointRadius, IReadOnlyList<MapPointViewModel> Points, IReadOnlyList<MapArrowViewModel> Arrows);

/// <summary>The "Ulazni podaci" page.</summary>
public sealed record InputDataPageViewModel(
    PageHeaderViewModel Header,
    string? Error,
    string? SourceText,
    IReadOnlyList<StatTileViewModel> Stats,
    IReadOnlyList<TruckRowViewModel> Trucks,
    IReadOnlyList<MachineRowViewModel> Machines,
    IReadOnlyList<LocationRowViewModel> Locations,
    IReadOnlyList<OrderRowViewModel> Orders,
    DistanceMatrixViewModel? Matrix,
    MapViewModel? Map);

/// <summary>
/// Maps a scenario to the input data page. Only presentation: no planning and no validation.
/// Missing references (unknown machine or location) are shown as text instead of throwing.
/// </summary>
public static class InputDataViewModelFactory
{
    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Ulazni podaci",
        "Kamioni, strojevi, lokacije i nalozi iz data/scenario.json te matrica udaljenosti između svih lokacija.");

    public static InputDataPageViewModel Create(ScenarioLoadResult result)
    {
        if (result.Scenario is null)
        {
            return new InputDataPageViewModel(Header, result.Error, null, [], [], [], [], [], null, null);
        }

        var scenario = result.Scenario;
        var matrix = new DistanceMatrix(scenario.Locations);
        var machinesById = scenario.Machines.ToDictionary(m => m.Id);
        var locationIds = scenario.Locations.Select(l => l.Id).ToHashSet();

        var source = $"data/{ScenarioProvider.FileName} · zadnja izmjena {result.LastModifiedLocal:dd.MM.yyyy. HH:mm}";

        return new InputDataPageViewModel(
            Header,
            Error: null,
            source,
            CreateStats(scenario, machinesById),
            scenario.Trucks.Select(t => CreateTruckRow(t, scenario)).ToList(),
            scenario.Machines.Select(m => CreateMachineRow(m, scenario)).ToList(),
            scenario.Locations.Select(l => CreateLocationRow(l, scenario, matrix, locationIds)).ToList(),
            scenario.Orders.Select(o => CreateOrderRow(o, scenario, matrix, machinesById, locationIds)).ToList(),
            CreateMatrix(matrix),
            CreateMap(scenario, machinesById));
    }

    public static TruckBadgeViewModel Badge(Truck truck) => truck.Id switch
    {
        "A" => new(truck.Id, "badge-soft is-truck-a"),
        "B" => new(truck.Id, "badge-soft is-truck-b"),
        _ => new(truck.Id, "badge-soft is-neutral"),
    };

    private static IReadOnlyList<StatTileViewModel> CreateStats(Scenario scenario, Dictionary<string, Machine> machinesById)
    {
        var totalCapacity = scenario.Trucks.Sum(t => t.CapacityKg);
        var totalWeight = scenario.Machines.Sum(m => m.WeightKg);

        // How many orders can be carried by only one truck, e.g. "5 samo kamion B".
        var restricted = scenario.Orders
            .Select(o => machinesById.GetValueOrDefault(o.MachineId))
            .Where(m => m is not null)
            .Select(m => scenario.Trucks.Where(t => t.CanCarry(m!)).ToList())
            .Where(carriers => carriers.Count == 1)
            .GroupBy(carriers => carriers[0].Id)
            .Select(g => $"{g.Count()} samo kamion {g.Key}")
            .ToList();

        return
        [
            new("Kamioni", Format.Integer(scenario.Trucks.Count), $"ukupna nosivost {Format.Kg(totalCapacity)}", "truck", "tone-accent"),
            new("Strojevi", Format.Integer(scenario.Machines.Count), $"ukupna masa {Format.Kg(totalWeight)}", "layers", "tone-accent"),
            new("Lokacije", Format.Integer(scenario.Locations.Count), $"baza {scenario.BaseLocationId}", "map", "tone-neutral"),
            new("Nalozi", Format.Integer(scenario.Orders.Count),
                restricted.Count == 0 ? "svaki nalog može svaki kamion" : string.Join(", ", restricted), "route",
                restricted.Count == 0 ? "tone-success" : "tone-warning"),
        ];
    }

    private static TruckRowViewModel CreateTruckRow(Truck truck, Scenario scenario)
    {
        // A count, not a list: the per-machine detail is in the machines table ("5 od 10 strojeva").
        var carriable = scenario.Machines.Count(truck.CanCarry);
        var carriableText = $"{carriable} od {scenario.Machines.Count} {Format.Plural(scenario.Machines.Count, "stroja", "stroja", "strojeva")}";

        return new TruckRowViewModel(Badge(truck), Format.Kg(truck.CapacityKg), Format.Integer(truck.MaxMachines), carriableText);
    }

    private static MachineRowViewModel CreateMachineRow(Machine machine, Scenario scenario)
    {
        var fitsOn = scenario.Trucks.Where(t => t.CanCarry(machine)).Select(Badge).ToList();
        return new MachineRowViewModel(machine.Id, Format.Kg(machine.WeightKg), fitsOn, FitsNowhere: fitsOn.Count == 0);
    }

    private static LocationRowViewModel CreateLocationRow(
        Location location, Scenario scenario, DistanceMatrix matrix, HashSet<string> locationIds)
    {
        var isBase = location.Id == scenario.BaseLocationId;
        var fromBase = locationIds.Contains(scenario.BaseLocationId)
            ? Format.Km(matrix[scenario.BaseLocationId, location.Id])
            : "–";

        return new LocationRowViewModel(
            location.Id, Format.Coordinate(location.X), Format.Coordinate(location.Y),
            isBase ? "baza" : string.Empty, fromBase, isBase);
    }

    private static OrderRowViewModel CreateOrderRow(
        TransportOrder order,
        Scenario scenario,
        DistanceMatrix matrix,
        Dictionary<string, Machine> machinesById,
        HashSet<string> locationIds)
    {
        var machine = machinesById.GetValueOrDefault(order.MachineId);
        var locationsKnown = locationIds.Contains(order.PickupLocationId) && locationIds.Contains(order.DeliveryLocationId);

        var warnings = new List<string>();
        if (machine is null)
        {
            warnings.Add($"nepoznat stroj {order.MachineId}");
        }

        if (!locationsKnown)
        {
            warnings.Add("nepoznata lokacija");
        }

        var trucks = machine is null
            ? []
            : scenario.Trucks.Where(t => t.CanCarry(machine)).Select(Badge).ToList();

        return new OrderRowViewModel(
            order.Id,
            order.MachineId,
            machine is null ? "–" : Format.Kg(machine.WeightKg),
            order.PickupLocationId,
            order.DeliveryLocationId,
            locationsKnown ? Format.Km(matrix[order.PickupLocationId, order.DeliveryLocationId]) : "–",
            trucks,
            warnings.Count == 0 ? null : string.Join(", ", warnings));
    }

    private static DistanceMatrixViewModel CreateMatrix(DistanceMatrix matrix)
    {
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

    // Map geometry (in km): SVG y grows downwards, so y is flipped. Arrows are shortened so that
    // the arrowhead stops at the edge of the location marker instead of under it.
    private const double Padding = 3.0;
    private const double PointRadius = 0.8;
    private const double ArrowGap = PointRadius + 0.5;

    private static MapViewModel? CreateMap(Scenario scenario, Dictionary<string, Machine> machinesById)
    {
        if (scenario.Locations.Count == 0)
        {
            return null;
        }

        var minX = scenario.Locations.Min(l => l.X) - Padding;
        var maxX = scenario.Locations.Max(l => l.X) + Padding;
        var minY = scenario.Locations.Min(l => l.Y) - Padding;
        var maxY = scenario.Locations.Max(l => l.Y) + Padding;
        var viewBox = $"{Format.Svg(minX)} {Format.Svg(-maxY)} {Format.Svg(maxX - minX)} {Format.Svg(maxY - minY)}";

        var points = scenario.Locations
            .Select(l => new MapPointViewModel(
                l.Id,
                Format.Svg(l.X), Format.Svg(-l.Y),
                Format.Svg(l.X + 1.1), Format.Svg(-l.Y - 1.1),
                IsBase: l.Id == scenario.BaseLocationId))
            .ToList();

        var locationsById = scenario.Locations.ToDictionary(l => l.Id);
        var arrows = new List<MapArrowViewModel>();
        foreach (var order in scenario.Orders)
        {
            if (!locationsById.TryGetValue(order.PickupLocationId, out var from)
                || !locationsById.TryGetValue(order.DeliveryLocationId, out var to))
            {
                continue;
            }

            var length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
            if (length <= 2 * ArrowGap)
            {
                continue; // pickup == delivery or too short to draw
            }

            var ux = (to.X - from.X) / length;
            var uy = (to.Y - from.Y) / length;
            var machine = machinesById.GetValueOrDefault(order.MachineId);

            arrows.Add(new MapArrowViewModel(
                order.Id,
                Format.Svg(from.X + ux * ArrowGap), Format.Svg(-(from.Y + uy * ArrowGap)),
                Format.Svg(to.X - ux * ArrowGap), Format.Svg(-(to.Y - uy * ArrowGap)),
                ArrowCssClass(machine, scenario.Trucks),
                $"{order.Id}: {order.MachineId} {order.PickupLocationId} → {order.DeliveryLocationId}"));
        }

        return new MapViewModel(viewBox, Format.Svg(PointRadius), points, arrows);
    }

    private static string ArrowCssClass(Machine? machine, IReadOnlyList<Truck> trucks)
    {
        if (machine is null)
        {
            return "map-order is-none";
        }

        var carriers = trucks.Where(t => t.CanCarry(machine)).ToList();
        return carriers.Count switch
        {
            0 => "map-order is-none",
            1 when carriers[0].Id == "A" => "map-order is-truck-a",
            1 when carriers[0].Id == "B" => "map-order is-truck-b",
            _ => "map-order is-any",
        };
    }
}
