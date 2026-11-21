namespace SmartLogistics.Web.ViewModels.Transport;

// View models of the transport plan (phase 6a, T41 c, T42 a; used by the plan page since phase 6c).
// Every value is already formatted (Croatian for text, invariant culture for SVG attributes); pages only print.
// Built by PlanMapper; map geometry (projection, view box, grid, axes) comes from ViewModels.MapGeometry.

/// <summary>Kind of a row in a truck's route table.</summary>
public enum StopRowKind
{
    /// <summary>Departure from the base (first row, 0 km, 00:00).</summary>
    Departure,
    Pickup,
    Delivery,

    /// <summary>Return to the base (last row; not an action, its leg is part of the truck's km).</summary>
    Return,
}

/// <summary>
/// One row of a truck's route: the departure from the base, every action (pickup / delivery) and the return.
/// </summary>
/// <param name="Step">"1", "2" ... for actions (1-based, as in the validator); "–" for departure and return.</param>
/// <param name="LocationId">Location of the action; the base for departure and return.</param>
/// <param name="ActionText">"Polazak", "Preuzimanje", "Isporuka" or "Povratak".</param>
/// <param name="ActionCssClass">Badge for the action, e.g. "sl-badge sl-badge-accent".</param>
/// <param name="OrderId">Order of the action; "–" for departure and return.</param>
/// <param name="MachineId">Machine of that order; "–" for base rows or an unknown order.</param>
/// <param name="LegKm">Leg from the previous point, e.g. "12,3 km"; "–" for the departure.</param>
/// <param name="CumulativeKm">Kilometres from the base up to this row, e.g. "37,2 km".</param>
/// <param name="Time">Elapsed time when the action is finished (return: arrival at the base), "hh:mm".</param>
/// <param name="LoadKg">Weight on the truck after the action, e.g. "9.900 kg".</param>
/// <param name="Machines">Machines on the truck after the action and the truck's limit, e.g. "2 / 3".</param>
/// <param name="CapacityPercent">Load after the action / truck capacity as a whole percentage, e.g. "99 %".</param>
/// <param name="RowCssClass">"sl-route-table-base-row" for departure and return, otherwise null.</param>
public sealed record StopRowViewModel(
    StopRowKind Kind,
    string Step,
    string LocationId,
    string ActionText,
    string ActionCssClass,
    string OrderId,
    string MachineId,
    string LegKm,
    string CumulativeKm,
    string Time,
    string LoadKg,
    string Machines,
    string CapacityPercent,
    string? RowCssClass)
{
    public bool IsBaseRow => Kind is StopRowKind.Departure or StopRowKind.Return;
}

/// <summary>The route of one truck with its totals and working-day use.</summary>
/// <param name="Title">"Kamion A".</param>
/// <param name="BadgeCssClass">"sl-badge sl-badge-truck-a" (first truck), "sl-badge-truck-b" (second), "sl-badge-neutral" (others).</param>
/// <param name="ColorClass">Colour suffix "truck-a", "truck-b" or "any" for sl-map-route-*, sl-map-swatch-*, sl-map-step-*.</param>
/// <param name="Capacity">"3.500 kg · 2 stroja".</param>
/// <param name="OrdersText">"3 naloga".</param>
/// <param name="Km">All legs including the return, "37,2 km".</param>
/// <param name="DrivingTime">"hh:mm".</param>
/// <param name="ActionTime">"hh:mm".</param>
/// <param name="TotalTime">Driving + actions, "hh:mm".</param>
/// <param name="WorkdayLabel">What counts against the working day: "vožnja + radnje" (Total) or "vožnja" (DrivingOnly).</param>
/// <param name="WorkdayTime">The counted time, "hh:mm".</param>
/// <param name="WorkdayMax">The limit, "09:00".</param>
/// <param name="WorkdayPercent">Counted time / limit as a whole percentage, "97 %".</param>
/// <param name="WorkdayText">"vožnja + radnje 08:43 od 09:00 (97 %)".</param>
/// <param name="WorkdayMeterValue">Counted minutes capped at the limit, invariant culture (for &lt;meter value&gt;).</param>
/// <param name="WorkdayMeterMax">The limit in minutes, invariant culture (for &lt;meter max&gt;).</param>
/// <param name="ExceedsWorkday">True when the counted time is over the limit (only in an invalid plan).</param>
/// <param name="Stops">Departure, all actions and the return; empty for an unused truck.</param>
public sealed record TruckRouteViewModel(
    string TruckId,
    string Title,
    string BadgeCssClass,
    string ColorClass,
    string Capacity,
    bool IsUnused,
    int OrderCount,
    string OrdersText,
    string Km,
    string DrivingTime,
    string ActionTime,
    string TotalTime,
    string WorkdayLabel,
    string WorkdayTime,
    string WorkdayMax,
    string WorkdayPercent,
    string WorkdayText,
    string WorkdayMeterValue,
    string WorkdayMeterMax,
    bool ExceedsWorkday,
    IReadOnlyList<StopRowViewModel> Stops);

/// <summary>An order that is not in the plan, with its reason (spec R33).</summary>
/// <param name="MachineId">Machine of the order as written in the order (may be unknown).</param>
/// <param name="Weight">"11.000 kg"; "–" for an unknown machine.</param>
/// <param name="ReasonCode">"K1" … "K4".</param>
/// <param name="ReasonText">Short Croatian reason, e.g. "stroj je teži od nosivosti svih kamiona".</param>
/// <param name="Message">The planner's message with the numbers.</param>
/// <param name="BadgeCssClass">"sl-badge sl-badge-danger" for K4 (invalid data), otherwise "sl-badge sl-badge-warning".</param>
public sealed record InfeasibleOrderViewModel(
    string OrderId,
    string MachineId,
    string Weight,
    string PickupLocationId,
    string DeliveryLocationId,
    string ReasonCode,
    string ReasonText,
    string Message,
    string BadgeCssClass);

/// <summary>The callout at the top of the plan: text, callout classes and icon name.</summary>
/// <param name="CssClass">"sl-callout sl-callout-ok", "-warning", "-danger" or "-info".</param>
public sealed record PlanStatusViewModel(string Text, string CssClass, string Icon);

/// <summary>A complete plan for display.</summary>
/// <param name="WorkdayModeText">"vožnja + radnje ≤ 09:00" or "samo vožnja ≤ 09:00".</param>
/// <param name="TotalKm">"200,1 km".</param>
/// <param name="OrdersText">"10 / 10" (planned / all orders of the scenario).</param>
/// <param name="IsValid">True when the validator found no violation.</param>
/// <param name="ElapsedText">Computation time ("12 ms", "1,4 s"); null when not measured.</param>
/// <param name="Stats">Key figures: total km, km per truck, planned orders.</param>
/// <param name="Map">Route map; null when the scenario has no locations.</param>
public sealed record PlanViewModel(
    string WorkdayModeText,
    string TotalKm,
    int PlannedOrderCount,
    int OrderCount,
    string OrdersText,
    bool AllOrdersPlanned,
    bool IsValid,
    string? ElapsedText,
    PlanStatusViewModel Status,
    IReadOnlyList<StatTileViewModel> Stats,
    IReadOnlyList<TruckRouteViewModel> Trucks,
    IReadOnlyList<InfeasibleOrderViewModel> InfeasibleOrders,
    IReadOnlyList<ViolationRowViewModel> Violations,
    MapViewModel? Map);

/// <summary>
/// SVG map of locations and truck routes. Coordinates are already in SVG space (x = X, y = -Y, km units, see
/// <see cref="MapGeometry.Project(double, double)"/>) and formatted in the invariant culture; colours come from CSS
/// classes (tokens in site.css), never from markup.
/// </summary>
/// <param name="Frame">View box (margin around all locations and the origin), grid, axes through the origin with km labels.</param>
/// <param name="PointRadius">Radius of a location marker (km units).</param>
/// <param name="Locations">Every location once (first occurrence of an ID).</param>
/// <param name="Segments">Route legs of all used trucks in route order, base → … → base; legs of 0 km are left out.</param>
/// <param name="Steps">Step numbers next to locations, one label per (location, truck), e.g. "1, 4".</param>
/// <param name="Legend">Base and every used truck with its km.</param>
public sealed record MapViewModel(
    MapFrameViewModel Frame,
    string PointRadius,
    IReadOnlyList<MapLocationViewModel> Locations,
    IReadOnlyList<MapSegmentViewModel> Segments,
    IReadOnlyList<MapStepViewModel> Steps,
    IReadOnlyList<MapLegendEntryViewModel> Legend)
{
    /// <summary>"minX minY width height" of the SVG (same as <see cref="MapFrameViewModel.ViewBox"/>).</summary>
    public string ViewBox => Frame.ViewBox;
}

/// <summary>One leg of a route, shortened at both ends so the arrowhead stays visible next to the marker.</summary>
/// <param name="Leg">1-based leg number of the truck's route (1 = base → first stop; the last one is the return).</param>
/// <param name="CssClass">"sl-map-route sl-map-route-truck-a" (colour suffix as <see cref="TruckRouteViewModel.ColorClass"/>).</param>
/// <param name="Title">Tooltip, e.g. "Kamion A, korak 1 (preuzimanje N01): L0 → L3, 12,3 km".</param>
public sealed record MapSegmentViewModel(string TruckId, int Leg, string X1, string Y1, string X2, string Y2, string CssClass, string Title);

/// <param name="Text">Step numbers of one truck at one location, e.g. "1, 4".</param>
/// <param name="CssClass">"sl-map-step sl-map-step-truck-a".</param>
/// <param name="Title">Tooltip, e.g. "Kamion A: 1 preuzimanje N01, 4 isporuka N03".</param>
public sealed record MapStepViewModel(string TruckId, string LocationId, string Text, string X, string Y, string CssClass, string Title);
