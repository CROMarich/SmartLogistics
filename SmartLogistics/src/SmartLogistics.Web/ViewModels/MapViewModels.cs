using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.ViewModels;

/// <summary>A location on the SVG map; coordinates are already in SVG space (y flipped) and formatted.</summary>
public sealed record MapPointViewModel(string Id, string Cx, string Cy, string LabelX, string LabelY, bool IsBase);

/// <summary>A directed line on the map (an order or a route leg), shortened so the arrowhead stays visible.</summary>
public sealed record MapArrowViewModel(string Id, string X1, string Y1, string X2, string Y2, string CssClass, string Title);

/// <summary>One legend entry: a coloured swatch and its meaning.</summary>
public sealed record MapLegendItemViewModel(string Text, string SwatchCssClass);

public sealed record MapViewModel(
    string ViewBox,
    string PointRadius,
    IReadOnlyList<MapPointViewModel> Points,
    IReadOnlyList<MapArrowViewModel> Arrows,
    IReadOnlyList<MapLegendItemViewModel> Legend);

/// <summary>
/// Map geometry shared by all maps. Coordinates are in km; SVG y grows downwards, so y is flipped.
/// Arrows are shortened so that the arrowhead stops at the edge of the location marker.
/// </summary>
public static class MapBuilder
{
    private const double Padding = 3.0;
    private const double PointRadius = 0.8;
    private const double ArrowGap = PointRadius + 0.5;

    public static MapViewModel? Create(
        Scenario scenario,
        IReadOnlyList<MapArrowViewModel> arrows,
        IReadOnlyList<MapLegendItemViewModel> legend)
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

        return new MapViewModel(viewBox, Format.Svg(PointRadius), points, arrows, legend);
    }

    /// <summary>Arrow from one location to another, or null when they are too close to draw (e.g. the same location).</summary>
    public static MapArrowViewModel? Arrow(string id, Location from, Location to, string cssClass, string title)
    {
        var length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        if (length <= 2 * ArrowGap)
        {
            return null;
        }

        var ux = (to.X - from.X) / length;
        var uy = (to.Y - from.Y) / length;

        return new MapArrowViewModel(
            id,
            Format.Svg(from.X + ux * ArrowGap), Format.Svg(-(from.Y + uy * ArrowGap)),
            Format.Svg(to.X - ux * ArrowGap), Format.Svg(-(to.Y - uy * ArrowGap)),
            cssClass,
            title);
    }
}
