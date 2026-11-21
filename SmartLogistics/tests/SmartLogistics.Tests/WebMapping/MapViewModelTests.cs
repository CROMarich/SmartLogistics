using SmartLogistics.Core.Domain;
using SmartLogistics.Web.ViewModels;
using SmartLogistics.Web.ViewModels.Transport;
using static SmartLogistics.Tests.WebMapping.WebTestData;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// The SVG route map (phase 6c): task coordinates have Y up, SVG has y down, so the projection is x = X, y = -Y
/// (docs/odluke.md T49 f). All SVG values are strings in the invariant culture and are parsed as such.
/// </summary>
[Trait("Area", "Web")]
public class MapViewModelTests
{
    private const double Tolerance = 1e-9;

    private static MapViewModel Map(Plan plan)
    {
        var map = PlanMapper.ToMap(plan, TaskScenario);
        Assert.NotNull(map);
        return map;
    }

    private static MapViewModel SolverMap() => Map(SolverPlan(WorkdayMode.Total));

    private static (double X, double Y) Point(MapViewModel map, string locationId)
    {
        var location = map.Locations.Single(l => l.Id == locationId);
        return (ParseSvg(location.X), ParseSvg(location.Y));
    }

    [Fact]
    public void L4_south_of_the_base_is_drawn_below_the_base()
    {
        // L4 = (9, -6): SVG y = 6, base L0 = (0, 0): SVG y = 0. SVG y grows downwards, so L4 must have the larger y.
        var map = SolverMap();
        var l4 = Point(map, "L4");
        var l0 = Point(map, "L0");

        Assert.True(l4.Y > l0.Y, $"L4 y = {l4.Y} must be greater than L0 y = {l0.Y}");
        Assert.Equal(6.0, l4.Y, Tolerance);
        Assert.Equal(0.0, l0.Y, Tolerance);
    }

    [Fact]
    public void L8_north_of_the_base_is_drawn_above_the_base()
    {
        // L8 = (23, 15): SVG y = -15 < 0.
        var map = SolverMap();
        var l8 = Point(map, "L8");

        Assert.True(l8.Y < Point(map, "L0").Y, $"L8 y = {l8.Y} must be smaller than the base y");
        Assert.Equal(-15.0, l8.Y, Tolerance);
    }

    [Fact]
    public void Every_location_is_projected_as_x_equals_X_and_y_equals_minus_Y()
    {
        // Guards against a swapped or mirrored transformation (e.g. y = Y, or x and y exchanged).
        var map = SolverMap();

        Assert.Equal(TaskScenario.Locations.Count, map.Locations.Count);
        foreach (var location in TaskScenario.Locations)
        {
            var (x, y) = Point(map, location.Id);
            Assert.Equal(location.X, x, Tolerance);
            Assert.Equal(-location.Y, y, Tolerance);
        }

        var (x4, y4) = MapGeometry.Project(9, -6);
        Assert.Equal(9.0, x4, Tolerance);
        Assert.Equal(6.0, y4, Tolerance);
        var (x9, y9) = MapGeometry.Project(new Location("L9", -8, -5));
        Assert.Equal(-8.0, x9, Tolerance);
        Assert.Equal(5.0, y9, Tolerance);
    }

    [Fact]
    public void Distinct_locations_get_distinct_markers()
    {
        var points = SolverMap().Locations.Select(l => (ParseSvg(l.X), ParseSvg(l.Y))).ToList();

        Assert.Equal(points.Count, points.Distinct().Count());
        Assert.Equal(TaskScenario.Locations.Select(l => l.Id).Order(StringComparer.Ordinal),
            SolverMap().Locations.Select(l => l.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Only_the_base_is_marked_as_base()
    {
        var map = SolverMap();

        var baseMarker = Assert.Single(map.Locations, l => l.IsBase);
        Assert.Equal("L0", baseMarker.Id);
        Assert.Contains("sl-map-location-base", baseMarker.CssClass.Split(' '));
        Assert.All(map.Locations.Where(l => !l.IsBase), l => Assert.DoesNotContain("sl-map-location-base", l.CssClass.Split(' ')));
    }

    [Fact]
    public void View_box_contains_every_location_with_the_margin()
    {
        var map = SolverMap();
        var parts = map.ViewBox.Split(' ');
        Assert.Equal(4, parts.Length);
        var (minX, minY, width, height) = (ParseSvg(parts[0]), ParseSvg(parts[1]), ParseSvg(parts[2]), ParseSvg(parts[3]));

        Assert.True(width > 0 && height > 0);
        foreach (var marker in map.Locations)
        {
            var (x, y) = (ParseSvg(marker.X), ParseSvg(marker.Y));
            Assert.InRange(x, minX + MapGeometry.Margin - Tolerance, minX + width - MapGeometry.Margin + Tolerance);
            Assert.InRange(y, minY + MapGeometry.Margin - Tolerance, minY + height - MapGeometry.Margin + Tolerance);
        }
    }

    [Fact]
    public void View_box_of_the_task_is_the_bounding_box_plus_3_km()
    {
        // X from -8 (L9) to 26 (L5), Y from -8 (L7) to 15 (L8); margin 3 km (T46 g):
        // minX = -8 - 3 = -11, width = 26 + 3 - (-11) = 40; SVG top = -(15 + 3) = -18, height = 18 - (-8 - 3) = 29.
        var map = SolverMap();

        Assert.Equal("-11 -18 40 29", map.ViewBox);
        Assert.Equal(map.Frame.ViewBox, map.ViewBox);
        Assert.Equal(-11.0, map.Frame.MinX, Tolerance);
        Assert.Equal(29.0, map.Frame.MaxX, Tolerance);
        Assert.Equal(-11.0, map.Frame.MinY, Tolerance);
        Assert.Equal(18.0, map.Frame.MaxY, Tolerance);
    }

    [Fact]
    public void Svg_numbers_use_the_invariant_culture()
    {
        var map = Map(ReferencePlan());
        var values = map.Locations.SelectMany(l => new[] { l.X, l.Y, l.LabelX, l.LabelY })
            .Concat(map.Segments.SelectMany(s => new[] { s.X1, s.Y1, s.X2, s.Y2 }))
            .Concat(map.Steps.SelectMany(s => new[] { s.X, s.Y }))
            .Concat(map.ViewBox.Split(' '))
            .Append(map.PointRadius)
            .ToList();

        Assert.All(values, v => Assert.DoesNotContain(",", v));
        Assert.All(values, v => Assert.True(double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _), $"'{v}' is not an invariant number"));
    }

    [Fact]
    public void Route_segments_follow_the_route_and_leave_out_legs_of_zero_km()
    {
        // Reference split: A drives L0 -> L9 -> L4 -> L0 (3 legs, none 0 km).
        // B has 19 legs (18 actions + return); 6 of them are 0 km (steps 1, 5, 9, 11, 13 and the return), so 13 are drawn.
        var plan = ReferencePlan();
        var map = Map(plan);

        Assert.Equal(3, map.Segments.Count(s => s.TruckId == "A"));
        Assert.Equal(13, map.Segments.Count(s => s.TruckId == "B"));

        // Every segment lies next to the projected end points of its leg (shortened by at most ArrowGap,
        // moved sideways by at most the parallel offset of two routes, 0.2 km).
        var maxDistance = MapGeometry.ArrowGap + 0.5;
        foreach (var route in plan.Routes)
        {
            var path = new List<string> { "L0" };
            path.AddRange(route.Stops.Select(s => s.LocationId));
            path.Add("L0");

            foreach (var segment in map.Segments.Where(s => s.TruckId == route.Truck.Id))
            {
                var from = MapGeometry.Project(Location(path[segment.Leg - 1]));
                var to = MapGeometry.Project(Location(path[segment.Leg]));

                Assert.True(Gap(from, (ParseSvg(segment.X1), ParseSvg(segment.Y1))) <= maxDistance,
                    $"Truck {route.Truck.Id} leg {segment.Leg} does not start at {path[segment.Leg - 1]}");
                Assert.True(Gap(to, (ParseSvg(segment.X2), ParseSvg(segment.Y2))) <= maxDistance,
                    $"Truck {route.Truck.Id} leg {segment.Leg} does not end at {path[segment.Leg]}");
                Assert.Contains($"sl-map-route-{PlanMapper.TruckColor(route.Truck, TaskScenario)}", segment.CssClass.Split(' '));
            }
        }
    }

    [Fact]
    public void A_leg_towards_the_south_west_goes_left_and_down_in_svg()
    {
        // A, leg 1: L0 (0, 0) -> L9 (-8, -5). In SVG x decreases and y increases (downwards).
        var segment = Map(ReferencePlan()).Segments.Single(s => s.TruckId == "A" && s.Leg == 1);

        Assert.True(ParseSvg(segment.X2) < ParseSvg(segment.X1));
        Assert.True(ParseSvg(segment.Y2) > ParseSvg(segment.Y1));
    }

    [Fact]
    public void A_leg_towards_the_north_east_goes_right_and_up_in_svg()
    {
        // B, leg 2: L0 (0, 0) -> L1 (3, 8). In SVG x increases and y decreases (upwards).
        var segment = Map(ReferencePlan()).Segments.Single(s => s.TruckId == "B" && s.Leg == 2);

        Assert.True(ParseSvg(segment.X2) > ParseSvg(segment.X1));
        Assert.True(ParseSvg(segment.Y2) < ParseSvg(segment.Y1));
    }

    [Fact]
    public void Legend_lists_the_base_and_every_used_truck_with_its_km()
    {
        var legend = Map(ReferencePlan()).Legend.Select(l => l.Text).ToList();

        Assert.Equal(new[] { "baza", "Kamion A – 37,2 km", "Kamion B – 162,9 km" }, legend);
    }

    [Fact]
    public void Map_without_routes_still_shows_all_locations()
    {
        var empty = new Plan(TaskScenario.Trucks.Select(t => new TruckRoute(t, [], 0, 0, 0, 0)).ToList(), [], 0);
        var map = Map(empty);

        Assert.Equal(TaskScenario.Locations.Count, map.Locations.Count);
        Assert.Empty(map.Segments);
        Assert.Empty(map.Steps);
        Assert.Equal("-11 -18 40 29", map.ViewBox);
    }

    private static Location Location(string id) => TaskScenario.Locations.Single(l => l.Id == id);

    private static double Gap((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
