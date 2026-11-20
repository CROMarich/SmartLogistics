using System.Globalization;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>One option of the workday-mode switch (a plain link, no JavaScript).</summary>
public sealed record ModeOptionViewModel(string Text, string QueryValue, bool IsActive);

/// <summary>One selectable scenario example (data/primjeri/primjeri.json).</summary>
public sealed record ScenarioOptionViewModel(string Id, string Title, bool IsActive);

/// <summary>One row of a truck's route table, including the departure and return rows.</summary>
public sealed record RouteRowViewModel(
    string Step,
    string Location,
    string ActionText,
    string ActionCssClass,
    string Order,
    string Load,
    string Machines,
    string LegKm,
    string CumulativeKm,
    string Time,
    bool IsBaseRow);

/// <summary>The plan of one truck.</summary>
public sealed record TruckPlanViewModel(
    TruckBadgeViewModel Badge,
    string Title,
    bool IsUnused,
    string Km,
    string Orders,
    string DrivingTime,
    string ActionTime,
    string TotalTime,
    string WorkdayLabel,
    string WorkdayValue,
    string WorkdayMax,
    string WorkdayText,
    IReadOnlyList<RouteRowViewModel> Rows);

public sealed record InfeasibleOrderViewModel(string Order, string Category, string CategoryText, string Message);

/// <summary>Shared by the plan page and the map page.</summary>
public sealed record PlanPageViewModel(
    PageHeaderViewModel Header,
    IReadOnlyList<ScenarioOptionViewModel> Scenarios,
    string ScenarioDescription,
    IReadOnlyList<ModeOptionViewModel> Modes,
    string? Error,
    IReadOnlyList<string> Facts,
    string StatusText,
    string StatusCssClass,
    string StatusIcon,
    IReadOnlyList<StatTileViewModel> Stats,
    IReadOnlyList<TruckPlanViewModel> Trucks,
    IReadOnlyList<InfeasibleOrderViewModel> InfeasibleOrders,
    IReadOnlyList<ViolationRowViewModel> Violations,
    MapViewModel? Map,
    IReadOnlyList<string>? ScenarioWarnings = null)
{
    /// <summary>Query value of the selected mode, used by links that keep the mode.</summary>
    public string ActiveModeQuery => Modes.First(m => m.IsActive).QueryValue;

    /// <summary>Id of the selected scenario example, used by links that keep the scenario.</summary>
    public string ActiveScenarioId => Scenarios.First(s => s.IsActive).Id;
}

public static class PlanViewModelFactory
{
    public const string DrivingOnlyQueryValue = "voznja";

    /// <summary>"voznja" → DrivingOnly; anything else → Total (the default, R22).</summary>
    public static WorkdayMode ParseMode(string? queryValue) =>
        string.Equals(queryValue, DrivingOnlyQueryValue, StringComparison.OrdinalIgnoreCase)
            ? WorkdayMode.DrivingOnly
            : WorkdayMode.Total;

    public static PlanPageViewModel Create(PlanRunResult run, IReadOnlyList<ScenarioExample> examples, PageHeaderViewModel header)
    {
        var limit = $"{Format.Coordinate(run.Options.MaxMinutes / 60)} h";
        var modes = new List<ModeOptionViewModel>
        {
            new($"Vožnja + radnje ≤ {limit} (zadano)", "total", run.Options.WorkdayMode == WorkdayMode.Total),
            new($"Samo vožnja ≤ {limit}", DrivingOnlyQueryValue, run.Options.WorkdayMode == WorkdayMode.DrivingOnly),
        };
        var scenarios = examples
            .Select(e => new ScenarioOptionViewModel(e.Id, e.Title, e.Id == run.Example.Id))
            .ToList();
        if (!scenarios.Any(s => s.IsActive))
        {
            scenarios.Insert(0, new ScenarioOptionViewModel(run.Example.Id, run.Example.Title, IsActive: true));
        }

        if (run.Scenario is null || run.Plan is null)
        {
            return new PlanPageViewModel(header, scenarios, run.Example.Description, modes, run.Error ?? "Plan nije izračunat.", [], string.Empty,
                string.Empty, string.Empty, [], [], [], [], null);
        }

        var scenario = run.Scenario;
        var plan = run.Plan;
        var options = run.Options;
        var matrix = new DistanceMatrix(scenario.Locations.DistinctBy(l => l.Id).ToList());
        var ordersById = scenario.Orders.DistinctBy(o => o.Id).ToDictionary(o => o.Id);

        var trucks = plan.Routes.Select(r => CreateTruck(r, scenario, options, matrix, ordersById)).ToList();
        var infeasible = plan.InfeasibleOrders.Select(CreateInfeasible).ToList();
        var violations = run.Violations
            .Select(v => new ViolationRowViewModel(v.RuleId, v.TruckId ?? "–",
                v.StepIndex is { } s ? (s + 1).ToString(CultureInfo.InvariantCulture) : "–", v.Message))
            .ToList();

        var planned = plan.Routes.Sum(r => r.OrderCount);
        var facts = new List<string>
        {
            $"planer: {run.PlannerName} (egzaktno)",
            $"izračun: {Format.Elapsed(run.Elapsed)}",
            $"radni dan: {Format.Clock(options.MaxMinutes)} h, radnja {Format.Coordinate(options.MinutesPerAction)} min, {Format.Coordinate(options.SpeedKmh)} km/h",
        };
        if (run.Statistics is { } s)
        {
            facts.Add($"pretraga: {Format.Integer(s.AssignmentsTried)} raspodjela, {Format.Integer(s.DpRuns)} DP izračuna, {Format.Integer(s.DpStates)} DP stanja{(s.ExtendedSearchUsed ? ", proširena pretraga (T23)" : string.Empty)}");
        }

        var (statusText, statusCss, statusIcon) = violations.Count > 0
            ? ($"Validator je pronašao {violations.Count} {Format.Plural(violations.Count, "prekršaj", "prekršaja", "prekršaja")} – plan nije ispravan.", "callout is-danger", "alert")
            : infeasible.Count > 0
                ? ($"Isplanirano {planned} od {scenario.Orders.Count} naloga. {infeasible.Count} {Format.Plural(infeasible.Count, "nalog nije izvediv", "naloga nisu izvediva", "naloga nije izvedivo")} – razlozi su u tablici ispod.", "callout is-warning", "alert")
                : ($"Svi nalozi ({planned}) su isplanirani i validator nije pronašao nijedan prekršaj.", "callout is-success", "check");

        var stats = new List<StatTileViewModel>
        {
            new("Ukupno", Format.Km(plan.TotalKm), "svi kamioni, s povratkom u bazu", "route", "tone-accent"),
        };
        stats.AddRange(trucks.Select(t => new StatTileViewModel(
            t.Title, t.Km, t.IsUnused ? "neiskorišten" : $"{t.Orders} · {t.WorkdayText}", "truck",
            t.Badge.CssClass.Contains("truck-a") ? "tone-truck-a" : t.Badge.CssClass.Contains("truck-b") ? "tone-truck-b" : "tone-neutral")));
        stats.Add(new StatTileViewModel("Nalozi", $"{planned} / {scenario.Orders.Count}",
            infeasible.Count == 0 ? "svi isplanirani" : $"{infeasible.Count} neizvedivo", "list",
            infeasible.Count == 0 ? "tone-success" : "tone-warning"));

        return new PlanPageViewModel(header, scenarios, run.Example.Description, modes, null, facts, statusText, statusCss, statusIcon, stats, trucks,
            infeasible, violations, CreateMap(plan, scenario), run.ScenarioWarnings ?? []);
    }

    private static TruckPlanViewModel CreateTruck(
        TruckRoute route,
        Scenario scenario,
        PlanningOptions options,
        DistanceMatrix matrix,
        Dictionary<string, TransportOrder> ordersById)
    {
        var truck = route.Truck;
        var badge = InputDataViewModelFactory.Badge(truck);
        var counted = options.WorkdayMode == WorkdayMode.Total ? route.TotalMinutes : route.DrivingMinutes;
        var workdayLabel = options.WorkdayMode == WorkdayMode.Total ? "vožnja + radnje" : "vožnja";
        var workdayText = $"{workdayLabel} {Format.Clock(counted)} od {Format.Clock(options.MaxMinutes)} ({Format.Percent(counted / options.MaxMinutes)})";

        var rows = new List<RouteRowViewModel>();
        if (route.Stops.Count > 0)
        {
            rows.Add(new RouteRowViewModel("–", scenario.BaseLocationId, "polazak", "badge-soft is-neutral", "–",
                "0 kg", $"0 / {truck.MaxMachines}", "–", Format.Number(0), Format.Clock(0), IsBaseRow: true));

            for (var i = 0; i < route.Stops.Count; i++)
            {
                var stop = route.Stops[i];
                var order = ordersById.GetValueOrDefault(stop.OrderId);
                var isPickup = stop.Action == StopAction.Pickup;
                rows.Add(new RouteRowViewModel(
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    stop.LocationId,
                    isPickup ? "preuzimanje" : "isporuka",
                    isPickup ? "badge-soft is-accent" : "badge-soft is-success",
                    order is null ? stop.OrderId : $"{stop.OrderId} · {order.MachineId}",
                    Format.Kg(stop.LoadKgAfter),
                    $"{stop.MachinesAfter} / {truck.MaxMachines}",
                    Format.Number(stop.LegKm),
                    Format.Number(stop.CumulativeKm),
                    Format.Clock(stop.CumulativeMinutes),
                    IsBaseRow: false));
            }

            var last = route.Stops[^1];
            var returnLeg = route.TotalKm - last.CumulativeKm;
            rows.Add(new RouteRowViewModel("–", scenario.BaseLocationId, "povratak", "badge-soft is-neutral", "–",
                Format.Kg(last.LoadKgAfter), $"{last.MachinesAfter} / {truck.MaxMachines}",
                Format.Number(returnLeg), Format.Number(route.TotalKm), Format.Clock(route.TotalMinutes), IsBaseRow: true));
        }

        var orderCount = $"{route.OrderCount} {Format.Plural(route.OrderCount, "nalog", "naloga", "naloga")}";

        return new TruckPlanViewModel(
            badge,
            $"Kamion {truck.Id}",
            IsUnused: route.Stops.Count == 0,
            Format.Km(route.TotalKm),
            orderCount,
            Format.Clock(route.DrivingMinutes),
            Format.Clock(route.ActionMinutes),
            Format.Clock(route.TotalMinutes),
            workdayLabel,
            Format.Svg(Math.Min(counted, options.MaxMinutes)),
            Format.Svg(options.MaxMinutes),
            workdayText,
            rows);
    }

    private static InfeasibleOrderViewModel CreateInfeasible(InfeasibleOrder item)
    {
        var (code, text) = item.Reason switch
        {
            InfeasibilityReason.ExceedsAllCapacities => ("K1", "stroj je teži od nosivosti svih kamiona"),
            InfeasibilityReason.ExceedsWorkday => ("K2", "ni sam ne stane u radni dan"),
            InfeasibilityReason.DoesNotFitWithOtherOrders => ("K3", "ne stane u radni dan uz ostale naloge"),
            InfeasibilityReason.InvalidOrderData => ("K4", "neispravni podaci naloga"),
            _ => ("?", "nepoznat razlog"),
        };

        return new InfeasibleOrderViewModel(item.Order.Id, code, text, item.Message);
    }

    private static MapViewModel? CreateMap(Plan plan, Scenario scenario)
    {
        var locationsById = scenario.Locations.DistinctBy(l => l.Id).ToDictionary(l => l.Id);
        if (!locationsById.TryGetValue(scenario.BaseLocationId, out var baseLocation))
        {
            return null;
        }

        var arrows = new List<MapArrowViewModel>();
        var legend = new List<MapLegendItemViewModel> { new("baza", "map-swatch is-base") };

        foreach (var route in plan.Routes.Where(r => r.Stops.Count > 0))
        {
            var css = route.Truck.Id switch
            {
                "A" => "is-truck-a",
                "B" => "is-truck-b",
                _ => "is-any",
            };
            legend.Add(new MapLegendItemViewModel($"kamion {route.Truck.Id} – {Format.Km(route.TotalKm)}", $"map-swatch {css}"));

            // Base → each stop → base; legs inside one location (0 km) are not drawn.
            var path = new List<(Location Location, string Label)> { (baseLocation, "polazak") };
            path.AddRange(route.Stops
                .Select((s, i) => (locationsById.GetValueOrDefault(s.LocationId), $"korak {i + 1}: {(s.Action == StopAction.Pickup ? "preuzimanje" : "isporuka")} {s.OrderId}"))
                .Where(p => p.Item1 is not null)
                .Select(p => (p.Item1!, p.Item2)));
            path.Add((baseLocation, "povratak"));

            for (var i = 1; i < path.Count; i++)
            {
                var (from, _) = path[i - 1];
                var (to, label) = path[i];
                var arrow = MapBuilder.Arrow(
                    $"{route.Truck.Id}-{i}", from, to, $"map-route {css}",
                    $"Kamion {route.Truck.Id}, {label}: {from.Id} → {to.Id} ({Format.Km(Distance.Leg(from, to))})");
                if (arrow is not null)
                {
                    arrows.Add(arrow);
                }
            }
        }

        return MapBuilder.Create(scenario, arrows, legend);
    }
}
