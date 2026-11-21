using System.Globalization;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Web.ViewModels.Transport;

/// <summary>
/// Maps a planning result (domain) to the transport view models. The only place where plan values are formatted
/// (via <see cref="Format"/>): km "162,9 km", kg "10.000 kg", time "08:43", shares "99 %".
/// <para>
/// Pure functions: no I/O, no clock, no planner call, so the mapping is easy to test. Robust to any data (spec R37):
/// unknown trucks, orders, machines or locations and duplicate IDs are shown as "–" or skipped on the map, never thrown.
/// Only null arguments throw <see cref="ArgumentNullException"/>.
/// </para>
/// <para>
/// Truck colours follow the truck's position in <see cref="Scenario.Trucks"/> (first distinct ID → "truck-a",
/// second → "truck-b", any other → "any"), not hard-coded IDs.
/// </para>
/// </summary>
public static class PlanMapper
{
    private const string Dash = "–";

    // Step labels and parallel routes in km units of the map; the rest of the geometry is in MapGeometry.
    private const double StepOffsetY = 1.7;
    private const double StepLineHeight = 1.1;
    private const double ParallelRouteOffset = 0.4;

    /// <summary>The whole plan: status, key figures, trucks, infeasible orders, validator violations and the map.</summary>
    /// <param name="violations">Result of <see cref="PlanValidator.Validate"/>; null or empty = valid.</param>
    /// <param name="elapsed">Computation time, shown as <see cref="PlanViewModel.ElapsedText"/>.</param>
    public static PlanViewModel ToPlan(
        Plan plan,
        Scenario scenario,
        PlanningOptions options,
        IReadOnlyList<PlanViolation>? violations = null,
        TimeSpan? elapsed = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        var context = new MappingContext(scenario);
        var trucks = plan.Routes.Select(r => ToTruckRoute(r, context, options)).ToList();
        var infeasible = plan.InfeasibleOrders.Select(i => ToInfeasibleOrder(i, context)).ToList();
        var violationRows = (violations ?? [])
            .Select(v => new ViolationRowViewModel(
                v.RuleId,
                v.TruckId ?? Dash,
                v.StepIndex is { } step ? (step + 1).ToString(CultureInfo.InvariantCulture) : Dash,
                v.Message))
            .ToList();

        var planned = plan.Routes.Sum(r => r.OrderCount);
        var orderCount = scenario.Orders.Count;

        return new PlanViewModel(
            WorkdayModeText(options),
            Format.Km(plan.TotalKm),
            planned,
            orderCount,
            $"{planned} / {orderCount}",
            AllOrdersPlanned: infeasible.Count == 0 && planned == orderCount,
            IsValid: violationRows.Count == 0,
            elapsed is { } e ? Format.Elapsed(e) : null,
            Status(planned, orderCount, infeasible.Count, violationRows.Count),
            Stats(plan, trucks, planned, orderCount, infeasible.Count),
            trucks,
            infeasible,
            violationRows,
            ToMap(plan, context));
    }

    /// <summary>One truck: totals, working-day use and all rows including departure and return.</summary>
    public static TruckRouteViewModel ToTruckRoute(TruckRoute route, Scenario scenario, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);
        return ToTruckRoute(route, new MappingContext(scenario), options);
    }

    /// <summary>
    /// Departure from the base, every stop and the return to the base. Empty for a route without stops.
    /// The return leg is <see cref="TruckRoute.TotalKm"/> minus the last stop's cumulative km (the model has no return stop).
    /// </summary>
    public static IReadOnlyList<StopRowViewModel> ToStopRows(TruckRoute route, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(scenario);
        return ToStopRows(route, new MappingContext(scenario));
    }

    /// <summary>An infeasible order with its reason code (K1–K4), short reason and the planner's message.</summary>
    public static InfeasibleOrderViewModel ToInfeasibleOrder(InfeasibleOrder item, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(scenario);
        return ToInfeasibleOrder(item, new MappingContext(scenario));
    }

    /// <summary>
    /// Frame (view box, grid, axes), locations, route legs, step numbers and legend; null when the scenario has no
    /// locations. Coordinates are projected by <see cref="MapGeometry.Project(double, double)"/> (y = -Y).
    /// </summary>
    public static MapViewModel? ToMap(Plan plan, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scenario);
        return ToMap(plan, new MappingContext(scenario));
    }

    /// <summary>
    /// Colour suffix of a truck: "truck-a" for the first distinct truck ID in the scenario, "truck-b" for the second,
    /// "any" for all others and for trucks that are not in the scenario.
    /// </summary>
    public static string TruckColor(Truck truck, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(truck);
        ArgumentNullException.ThrowIfNull(scenario);
        return new MappingContext(scenario).ColorOf(truck.Id);
    }

    /// <summary>"K1" … "K4" (spec R33).</summary>
    public static string ReasonCode(InfeasibilityReason reason) => reason switch
    {
        InfeasibilityReason.ExceedsAllCapacities => "K1",
        InfeasibilityReason.ExceedsWorkday => "K2",
        InfeasibilityReason.DoesNotFitWithOtherOrders => "K3",
        InfeasibilityReason.InvalidOrderData => "K4",
        _ => "?",
    };

    /// <summary>"vožnja + radnje ≤ 09:00" (Total) or "samo vožnja ≤ 09:00" (DrivingOnly).</summary>
    public static string WorkdayModeText(PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limit = Format.Clock(options.MaxMinutes);
        return options.WorkdayMode == WorkdayMode.DrivingOnly ? $"samo vožnja ≤ {limit}" : $"vožnja + radnje ≤ {limit}";
    }

    // ---------- trucks and rows ----------

    private static TruckRouteViewModel ToTruckRoute(TruckRoute route, MappingContext context, PlanningOptions options)
    {
        var truck = route.Truck;
        var color = context.ColorOf(truck.Id);
        var isTotal = options.WorkdayMode != WorkdayMode.DrivingOnly;
        var counted = isTotal ? route.TotalMinutes : route.DrivingMinutes;
        var label = isTotal ? "vožnja + radnje" : "vožnja";
        var percent = Format.Percent(counted, options.MaxMinutes);

        return new TruckRouteViewModel(
            truck.Id,
            $"Kamion {truck.Id}",
            $"sl-badge sl-badge-{(color == "any" ? "neutral" : color)}",
            color,
            $"{Format.Kg(truck.CapacityKg)} · {truck.MaxMachines} {Format.Plural(truck.MaxMachines, "stroj", "stroja", "strojeva")}",
            IsUnused: route.Stops.Count == 0,
            route.OrderCount,
            $"{route.OrderCount} {Format.Plural(route.OrderCount, "nalog", "naloga", "naloga")}",
            Format.Km(route.TotalKm),
            Format.Clock(route.DrivingMinutes),
            Format.Clock(route.ActionMinutes),
            Format.Clock(route.TotalMinutes),
            label,
            Format.Clock(counted),
            Format.Clock(options.MaxMinutes),
            percent,
            $"{label} {Format.Clock(counted)} od {Format.Clock(options.MaxMinutes)} ({percent})",
            Format.Svg(Math.Clamp(counted, 0, Math.Max(0, options.MaxMinutes))),
            Format.Svg(options.MaxMinutes),
            ExceedsWorkday: counted > options.MaxMinutes + 1e-6,
            ToStopRows(route, context));
    }

    private static IReadOnlyList<StopRowViewModel> ToStopRows(TruckRoute route, MappingContext context)
    {
        if (route.Stops.Count == 0)
        {
            return [];
        }

        var truck = route.Truck;
        var baseId = context.Scenario.BaseLocationId;
        var rows = new List<StopRowViewModel>(route.Stops.Count + 2)
        {
            new(StopRowKind.Departure, Dash, baseId, "Polazak", "sl-badge sl-badge-neutral", Dash, Dash,
                Dash, Format.Km(0), Format.Clock(0), Format.Kg(0), Machines(0, truck), Format.Percent(0, truck.CapacityKg),
                "sl-route-table-base-row"),
        };

        for (var i = 0; i < route.Stops.Count; i++)
        {
            var stop = route.Stops[i];
            var isPickup = stop.Action == StopAction.Pickup;
            rows.Add(new StopRowViewModel(
                isPickup ? StopRowKind.Pickup : StopRowKind.Delivery,
                (i + 1).ToString(CultureInfo.InvariantCulture),
                stop.LocationId,
                isPickup ? "Preuzimanje" : "Isporuka",
                isPickup ? "sl-badge sl-badge-accent" : "sl-badge sl-badge-ok",
                stop.OrderId,
                context.MachineIdOf(stop.OrderId) ?? Dash,
                Format.Km(stop.LegKm),
                Format.Km(stop.CumulativeKm),
                Format.Clock(stop.CumulativeMinutes),
                Format.Kg(stop.LoadKgAfter),
                Machines(stop.MachinesAfter, truck),
                Format.Percent(stop.LoadKgAfter, truck.CapacityKg),
                RowCssClass: null));
        }

        var last = route.Stops[^1];
        var returnLeg = Math.Round(route.TotalKm - last.CumulativeKm, 1, MidpointRounding.AwayFromZero);
        rows.Add(new StopRowViewModel(
            StopRowKind.Return, Dash, baseId, "Povratak", "sl-badge sl-badge-neutral", Dash, Dash,
            Format.Km(returnLeg), Format.Km(route.TotalKm), Format.Clock(route.TotalMinutes),
            Format.Kg(last.LoadKgAfter), Machines(last.MachinesAfter, truck), Format.Percent(last.LoadKgAfter, truck.CapacityKg),
            "sl-route-table-base-row"));

        return rows;
    }

    private static string Machines(int count, Truck truck) => $"{count} / {truck.MaxMachines}";

    // ---------- infeasible orders ----------

    private static InfeasibleOrderViewModel ToInfeasibleOrder(InfeasibleOrder item, MappingContext context)
    {
        var order = item.Order;
        var machine = order?.MachineId is { } machineId ? context.Machine(machineId) : null;
        var reasonText = item.Reason switch
        {
            InfeasibilityReason.ExceedsAllCapacities => "stroj je teži od nosivosti svih kamiona",
            InfeasibilityReason.ExceedsWorkday => "ni sam ne stane u radni dan",
            InfeasibilityReason.DoesNotFitWithOtherOrders => "ne stane u radni dan uz ostale naloge",
            InfeasibilityReason.InvalidOrderData => "neispravni podaci naloga",
            _ => "nepoznat razlog",
        };

        return new InfeasibleOrderViewModel(
            Text(order?.Id),
            Text(order?.MachineId),
            machine is null ? Dash : Format.Kg(machine.WeightKg),
            Text(order?.PickupLocationId),
            Text(order?.DeliveryLocationId),
            ReasonCode(item.Reason),
            reasonText,
            item.Message ?? string.Empty,
            item.Reason == InfeasibilityReason.InvalidOrderData ? "sl-badge sl-badge-danger" : "sl-badge sl-badge-warning");
    }

    private static string Text(string? value) => string.IsNullOrEmpty(value) ? Dash : value;

    // ---------- status and key figures ----------

    private static PlanStatusViewModel Status(int planned, int orderCount, int infeasible, int violations)
    {
        if (violations > 0)
        {
            return new($"Validator je pronašao {violations} {Format.Plural(violations, "prekršaj", "prekršaja", "prekršaja")} – plan nije ispravan.",
                "sl-callout sl-callout-danger", "alert");
        }

        if (infeasible > 0)
        {
            return new($"Isplanirano {planned} od {orderCount} naloga. {infeasible} {Format.Plural(infeasible, "nalog nije izvediv", "naloga nisu izvediva", "naloga nije izvedivo")} – razlozi su u tablici neizvedivih naloga.",
                "sl-callout sl-callout-warning", "alert");
        }

        if (orderCount == 0)
        {
            return new("Scenarij nema nijedan nalog – nema što planirati.", "sl-callout sl-callout-info", "info");
        }

        return new($"Svi nalozi ({planned}) su isplanirani i validator nije pronašao nijedan prekršaj.", "sl-callout sl-callout-ok", "check");
    }

    private static IReadOnlyList<StatTileViewModel> Stats(
        Plan plan, IReadOnlyList<TruckRouteViewModel> trucks, int planned, int orderCount, int infeasible)
    {
        var used = trucks.Count(t => !t.IsUnused);
        var stats = new List<StatTileViewModel>
        {
            new("Ukupno", Format.Km(plan.TotalKm),
                $"{used} {Format.Plural(used, "kamion", "kamiona", "kamiona")} u upotrebi, s povratkom u bazu", "route", "sl-stat-tile-accent"),
        };

        stats.AddRange(trucks.Select(t => new StatTileViewModel(
            t.Title,
            t.Km,
            t.IsUnused ? "neiskorišten" : $"{t.OrdersText} · {t.WorkdayPercent} radnog dana",
            "truck",
            $"sl-stat-tile-{(t.ColorClass == "any" ? "neutral" : t.ColorClass)}")));

        stats.Add(new StatTileViewModel(
            "Nalozi",
            $"{planned} / {orderCount}",
            infeasible == 0 ? "svi isplanirani" : $"{infeasible} {Format.Plural(infeasible, "neizvediv", "neizvediva", "neizvedivih")}",
            "list",
            infeasible == 0 ? "sl-stat-tile-ok" : "sl-stat-tile-warning"));

        return stats;
    }

    // ---------- map ----------

    private static MapViewModel? ToMap(Plan plan, MappingContext context)
    {
        var locations = context.Locations;
        if (locations.Count == 0)
        {
            return null;
        }

        var frame = MapGeometry.Frame(locations);
        var baseId = context.Scenario.BaseLocationId;
        var points = MapGeometry.Locations(locations, baseId);

        var segments = new List<MapSegmentViewModel>();
        var steps = new List<MapStepViewModel>();
        var legend = new List<MapLegendEntryViewModel> { new("baza", "sl-map-swatch sl-map-swatch-base") };

        var usedRoutes = plan.Routes.Where(r => r.Stops.Count > 0).ToList();
        var labelsAtLocation = new Dictionary<string, int>();
        var baseLocation = context.Location(baseId);

        for (var routeIndex = 0; routeIndex < usedRoutes.Count; routeIndex++)
        {
            var route = usedRoutes[routeIndex];
            var truckId = route.Truck.Id;
            var color = context.ColorOf(truckId);
            legend.Add(new MapLegendEntryViewModel($"Kamion {truckId} – {Format.Km(route.TotalKm)}", $"sl-map-swatch sl-map-swatch-{color}"));

            // Parallel offset so that legs driven by several trucks stay visible side by side.
            var offset = (routeIndex - (usedRoutes.Count - 1) / 2.0) * ParallelRouteOffset;

            // Base -> every stop -> base; points with unknown locations are skipped.
            var path = new List<(Location? Location, string Label)> { (baseLocation, "polazak") };
            path.AddRange(route.Stops.Select((s, i) => (context.Location(s.LocationId), $"korak {i + 1} ({ActionWord(s.Action)} {s.OrderId})")));
            path.Add((baseLocation, "povratak"));

            for (var leg = 1; leg < path.Count; leg++)
            {
                var from = path[leg - 1].Location;
                var to = path[leg].Location;
                if (from is null || to is null)
                {
                    continue;
                }

                var km = Distance.Leg(from, to);
                if (MapGeometry.Shorten(from, to, offset) is { } line)
                {
                    segments.Add(new MapSegmentViewModel(
                        truckId, leg, Format.Svg(line.X1), Format.Svg(line.Y1), Format.Svg(line.X2), Format.Svg(line.Y2),
                        $"sl-map-route sl-map-route-{color}",
                        $"Kamion {truckId}, {path[leg].Label}: {from.Id} → {to.Id}, {Format.Km(km)}"));
                }
            }

            // One label per (location, truck) with all step numbers there, stacked under the marker.
            foreach (var group in route.Stops
                         .Select((s, i) => (Stop: s, Number: i + 1))
                         .GroupBy(x => x.Stop.LocationId))
            {
                var location = context.Location(group.Key);
                if (location is null)
                {
                    continue;
                }

                var line = labelsAtLocation.GetValueOrDefault(location.Id);
                labelsAtLocation[location.Id] = line + 1;

                steps.Add(new MapStepViewModel(
                    truckId,
                    location.Id,
                    string.Join(", ", group.Select(x => x.Number.ToString(CultureInfo.InvariantCulture))),
                    Format.Svg(MapGeometry.Project(location).X + MapGeometry.LabelOffset),
                    Format.Svg(MapGeometry.Project(location).Y + StepOffsetY + line * StepLineHeight),
                    $"sl-map-step sl-map-step-{color}",
                    $"Kamion {truckId}: " + string.Join(", ", group.Select(x => $"{x.Number} {ActionWord(x.Stop.Action)} {x.Stop.OrderId}"))));
            }
        }

        return new MapViewModel(frame, Format.Svg(MapGeometry.PointRadius), points, segments, steps, legend);
    }

    private static string ActionWord(StopAction action) => action == StopAction.Pickup ? "preuzimanje" : "isporuka";

    // ---------- lookups ----------

    /// <summary>Lookups by ID built once per mapping; the first occurrence of a duplicated ID wins, nulls are ignored.</summary>
    private sealed class MappingContext
    {
        private readonly Dictionary<string, Location> _locations;
        private readonly Dictionary<string, Machine> _machines;
        private readonly Dictionary<string, TransportOrder> _orders;
        private readonly Dictionary<string, string> _colors;

        public MappingContext(Scenario scenario)
        {
            Scenario = scenario;
            Locations = (scenario.Locations ?? []).Where(l => l?.Id is not null).DistinctBy(l => l.Id).ToList();
            _locations = Locations.ToDictionary(l => l.Id);
            _machines = (scenario.Machines ?? []).Where(m => m?.Id is not null).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
            _orders = (scenario.Orders ?? []).Where(o => o?.Id is not null).DistinctBy(o => o.Id).ToDictionary(o => o.Id);

            string[] palette = ["truck-a", "truck-b"];
            _colors = (scenario.Trucks ?? [])
                .Where(t => t?.Id is not null)
                .Select(t => t.Id)
                .Distinct()
                .Select((id, index) => (id, color: index < palette.Length ? palette[index] : "any"))
                .ToDictionary(x => x.id, x => x.color);
        }

        public Scenario Scenario { get; }

        public IReadOnlyList<Location> Locations { get; }

        public Location? Location(string? id) => id is not null ? _locations.GetValueOrDefault(id) : null;

        public Machine? Machine(string id) => _machines.GetValueOrDefault(id);

        public string? MachineIdOf(string? orderId) =>
            orderId is not null && _orders.TryGetValue(orderId, out var order) ? order.MachineId : null;

        public string ColorOf(string? truckId) => truckId is not null ? _colors.GetValueOrDefault(truckId, "any") : "any";
    }
}
