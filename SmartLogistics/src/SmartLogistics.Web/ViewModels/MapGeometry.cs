using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.ViewModels;

// Shared SVG map geometry of both maps (input data map of orders and the route map of the plan).
// All coordinate transformation happens here: km coordinates of the task (Y up) → SVG user units (y down, y = -Y).
// Views only print the already formatted values (invariant culture); colours come from CSS classes.

/// <summary>A location marker on a map.</summary>
/// <param name="X">SVG x (= X km), invariant culture.</param>
/// <param name="Y">SVG y (= -Y km), invariant culture; a location south of the base has a larger y than the base.</param>
/// <param name="CssClass">"sl-map-location" or "sl-map-location sl-map-location-base".</param>
public sealed record MapLocationViewModel(string Id, string X, string Y, string LabelX, string LabelY, bool IsBase, string CssClass);

/// <param name="SwatchCssClass">"sl-map-swatch sl-map-swatch-base", "… sl-map-swatch-truck-a" ….</param>
public sealed record MapLegendEntryViewModel(string Text, string SwatchCssClass);

/// <summary>A straight line in SVG space (grid line or axis).</summary>
/// <param name="CssClass">"sl-map-grid-line" or "sl-map-axis".</param>
public sealed record MapLineViewModel(string X1, string Y1, string X2, string Y2, string CssClass);

/// <summary>A text label in SVG space (axis tick or axis title).</summary>
/// <param name="CssClass">"sl-map-tick sl-map-tick-x", "sl-map-tick sl-map-tick-y" or "sl-map-axis-label …".</param>
public sealed record MapTextViewModel(string Text, string X, string Y, string CssClass);

/// <summary>
/// The frame of a map: view box, grid, axes through the origin and their km labels.
/// </summary>
/// <param name="ViewBox">"minX minY width height" in SVG space: all locations and the origin plus a margin.</param>
/// <param name="GridStep">Distance between grid lines in km (1, 2, 5, 10 …).</param>
/// <param name="GridStepText">The same for the UI, e.g. "5 km".</param>
/// <param name="GridLines">Vertical and horizontal grid lines at multiples of <paramref name="GridStep"/>.</param>
/// <param name="Axes">The x axis (Y = 0) and the y axis (X = 0) across the whole view box.</param>
/// <param name="Ticks">km labels at every grid line on both axes ("5", "-5" …) and "0" at the origin.</param>
/// <param name="AxisLabels">"X (km)" at the right end of the x axis, "Y (km)" at the top of the y axis.</param>
public sealed record MapFrameViewModel(
    string ViewBox,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    double GridStep,
    string GridStepText,
    IReadOnlyList<MapLineViewModel> GridLines,
    IReadOnlyList<MapLineViewModel> Axes,
    IReadOnlyList<MapTextViewModel> Ticks,
    IReadOnlyList<MapTextViewModel> AxisLabels);

/// <summary>A directed line in SVG space, already shortened at both ends.</summary>
public readonly record struct MapLine(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Pure geometry of the SVG maps. Units are km of the task coordinates (1 SVG user unit = 1 km).
/// </summary>
public static class MapGeometry
{
    /// <summary>Space around the outermost location (and the origin), km.</summary>
    public const double Margin = 3.0;

    /// <summary>Radius of a location marker, km.</summary>
    public const double PointRadius = 0.7;

    /// <summary>Lines stop this far from the centre of a location, so the arrowhead stays visible next to the marker.</summary>
    public const double ArrowGap = PointRadius + 0.5;

    /// <summary>Offset of a location label from its marker, km.</summary>
    public const double LabelOffset = 1.1;

    // Most grid lines per direction; the step is the smallest "nice" number (1, 2, 5 × 10^n) that respects it.
    private const int MaxGridLines = 10;
    private const double TickOffset = 1.5;
    private const double YTickGap = 0.5;

    /// <summary>Task coordinates (km, Y up) → SVG coordinates (y down): (X, Y) → (X, -Y).</summary>
    public static (double X, double Y) Project(double x, double y) => (x, -y);

    /// <summary>Projects a location; see <see cref="Project(double, double)"/>.</summary>
    public static (double X, double Y) Project(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Project(location.X, location.Y);
    }

    /// <summary>
    /// View box, grid and axes for the given locations. The origin is always inside the frame, so both axes are visible.
    /// Null entries are ignored; without locations the frame covers only the origin and the margin.
    /// </summary>
    public static MapFrameViewModel Frame(IEnumerable<Location?> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        var points = locations.Where(l => l is not null).Select(l => l!).ToList();

        var minX = Math.Min(0, points.Count == 0 ? 0 : points.Min(l => l.X)) - Margin;
        var maxX = Math.Max(0, points.Count == 0 ? 0 : points.Max(l => l.X)) + Margin;
        var minY = Math.Min(0, points.Count == 0 ? 0 : points.Min(l => l.Y)) - Margin;
        var maxY = Math.Max(0, points.Count == 0 ? 0 : points.Max(l => l.Y)) + Margin;

        // SVG: the top edge is the largest Y (y = -maxY).
        var viewBox = $"{Format.Svg(minX)} {Format.Svg(-maxY)} {Format.Svg(maxX - minX)} {Format.Svg(maxY - minY)}";
        var step = GridStep(Math.Max(maxX - minX, maxY - minY));

        var gridLines = new List<MapLineViewModel>();
        var ticks = new List<MapTextViewModel>();
        foreach (var x in Multiples(step, minX, maxX))
        {
            gridLines.Add(Line(x, minY, x, maxY, "sl-map-grid-line"));
            if (x != 0)
            {
                var (tx, ty) = Project(x, 0);
                ticks.Add(new MapTextViewModel(Label(x), Format.Svg(tx), Format.Svg(ty + TickOffset), "sl-map-tick sl-map-tick-x"));
            }
        }

        foreach (var y in Multiples(step, minY, maxY))
        {
            gridLines.Add(Line(minX, y, maxX, y, "sl-map-grid-line"));
            if (y != 0)
            {
                var (tx, ty) = Project(-YTickGap, y);
                ticks.Add(new MapTextViewModel(Label(y), Format.Svg(tx), Format.Svg(ty + YTickGap), "sl-map-tick sl-map-tick-y"));
            }
        }

        var (ox, oy) = Project(-YTickGap, 0);
        ticks.Add(new MapTextViewModel("0", Format.Svg(ox), Format.Svg(oy + TickOffset), "sl-map-tick sl-map-tick-y"));

        IReadOnlyList<MapLineViewModel> axes =
        [
            Line(minX, 0, maxX, 0, "sl-map-axis"),
            Line(0, minY, 0, maxY, "sl-map-axis"),
        ];

        // X title below the tick labels at the right end, Y title right of the axis at the top: away from the data.
        var (xLabelX, xLabelY) = Project(maxX - YTickGap, 0);
        var (yLabelX, yLabelY) = Project(YTickGap, maxY - TickOffset);
        IReadOnlyList<MapTextViewModel> axisLabels =
        [
            new("X (km)", Format.Svg(xLabelX), Format.Svg(xLabelY + 2 * TickOffset), "sl-map-axis-label sl-map-axis-label-x"),
            new("Y (km)", Format.Svg(yLabelX), Format.Svg(yLabelY), "sl-map-axis-label sl-map-axis-label-y"),
        ];

        return new MapFrameViewModel(viewBox, minX, maxX, minY, maxY, step, $"{Label(step)} km", gridLines, axes, ticks, axisLabels);
    }

    /// <summary>
    /// Every location once (first occurrence of an ID, nulls ignored) as a marker with a label; the base is highlighted.
    /// </summary>
    public static IReadOnlyList<MapLocationViewModel> Locations(IEnumerable<Location?> locations, string? baseLocationId)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return locations
            .Where(l => l?.Id is not null)
            .Select(l => l!)
            .DistinctBy(l => l.Id)
            .Select(l =>
            {
                var (x, y) = Project(l);
                var isBase = l.Id == baseLocationId;
                return new MapLocationViewModel(
                    l.Id,
                    Format.Svg(x), Format.Svg(y),
                    Format.Svg(x + LabelOffset), Format.Svg(y - LabelOffset),
                    isBase,
                    isBase ? "sl-map-location sl-map-location-base" : "sl-map-location");
            })
            .ToList();
    }

    /// <summary>
    /// A line from one location to another in SVG space, moved sideways by <paramref name="offset"/> km (to the left of
    /// the direction of travel) and shortened at both ends by <see cref="ArrowGap"/>, at most by a third of its length.
    /// Null when both locations are (almost) the same point.
    /// </summary>
    public static MapLine? Shorten(Location from, Location to, double offset = 0)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
        {
            return null;
        }

        var ux = dx / length;
        var uy = dy / length;
        var gap = Math.Min(ArrowGap, length / 3);
        var nx = -uy * offset;
        var ny = ux * offset;

        var (x1, y1) = Project(from.X + ux * gap + nx, from.Y + uy * gap + ny);
        var (x2, y2) = Project(to.X - ux * gap + nx, to.Y - uy * gap + ny);
        return new MapLine(x1, y1, x2, y2);
    }

    /// <summary>Smallest step of the form 1, 2 or 5 × 10^n km with at most 10 grid lines over <paramref name="span"/>.</summary>
    public static double GridStep(double span)
    {
        if (!(span > 0) || !double.IsFinite(span))
        {
            return 1;
        }

        for (var magnitude = 1.0; ; magnitude *= 10)
        {
            foreach (var factor in new[] { 1.0, 2.0, 5.0 })
            {
                var step = factor * magnitude;
                if (span / step <= MaxGridLines)
                {
                    return step;
                }
            }
        }
    }

    private static IEnumerable<double> Multiples(double step, double min, double max)
    {
        for (var k = Math.Ceiling(min / step); k * step <= max; k++)
        {
            yield return k * step;
        }
    }

    private static MapLineViewModel Line(double x1, double y1, double x2, double y2, string cssClass)
    {
        var (sx1, sy1) = Project(x1, y1);
        var (sx2, sy2) = Project(x2, y2);
        return new MapLineViewModel(Format.Svg(sx1), Format.Svg(sy1), Format.Svg(sx2), Format.Svg(sy2), cssClass);
    }

    private static string Label(double km) => Format.Coordinate(km);
}
