using System.Globalization;
using System.Text.RegularExpressions;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.PlanBuilder;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// Shared input of the Web mapper tests: the scenario from data/scenario.json, solver plans for both working-day
/// modes (computed once per test run, the solver is located by <see cref="PlannerLocator"/>) and a hand-built plan
/// with the reference split of CLAUDE.md (A 37.2 km, B 162.9 km), whose legs are computed from the coordinates.
/// </summary>
internal static class WebTestData
{
    private static readonly Lazy<Scenario> LazyScenario = new(() => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath));
    private static readonly Lazy<Plan> LazyTotalPlan = new(() => Solve(WorkdayMode.Total));
    private static readonly Lazy<Plan> LazyDrivingOnlyPlan = new(() => Solve(WorkdayMode.DrivingOnly));

    public static Scenario TaskScenario => LazyScenario.Value;

    public static PlanningOptions Options(WorkdayMode mode) => PlanningOptions.Default with { WorkdayMode = mode };

    /// <summary>The solver plan for data/scenario.json in the given mode.</summary>
    public static Plan SolverPlan(WorkdayMode mode) => mode == WorkdayMode.Total ? LazyTotalPlan.Value : LazyDrivingOnlyPlan.Value;

    private static Plan Solve(WorkdayMode mode) => PlannerLocator.Create().Solve(TaskScenario, Options(mode));

    /// <summary>
    /// Truck A of the reference split (Total): N03 only, L0 → L9 → L4 → L0.
    /// Legs: L0–L9 = sqrt(64 + 25) = 9.43 → 9.4; L9–L4 = sqrt(289 + 1) = 17.03 → 17.0; L4–L0 = sqrt(81 + 36) = 10.82 → 10.8.
    /// Sum 37.2 km; minutes 37.2 + 2 × 20 = 77.2.
    /// </summary>
    public static readonly PlanStep[] ReferenceRouteA = [Pickup("N03"), Delivery("N03")];

    /// <summary>
    /// Truck B of the reference split (Total): the other nine orders.
    /// Legs (km, from the coordinates, one decimal): L0→L0 0.0, L0→L1 8.5, L1→L2 7.6, L2→L8 13.6, L8→L8 0.0,
    /// L8→L2 13.6, L2→L3 7.3, L3→L5 10.8, L5→L5 0.0, L5→L7 12.5, L7→L7 0.0, L7→L3 17.3, L3→L3 0.0, L3→L4 17.0,
    /// L4→L9 17.0, L9→L6 18.0, L6→L1 11.2, L1→L0 8.5, return L0→L0 0.0.
    /// Sum 162.9 km; 18 actions → minutes 162.9 + 18 × 20 = 522.9. Largest load: after step 16 (pickup N09):
    /// M04 2400 + M09 7500 = 9900 kg.
    /// </summary>
    public static readonly PlanStep[] ReferenceRouteB =
    [
        Pickup("N05"), Pickup("N01"), Pickup("N02"), Delivery("N05"), Pickup("N07"), Delivery("N07"),
        Delivery("N01"), Delivery("N02"), Pickup("N06"), Delivery("N06"), Pickup("N10"), Delivery("N10"),
        Pickup("N04"), Pickup("N08"), Delivery("N08"), Pickup("N09"), Delivery("N09"), Delivery("N04"),
    ];

    /// <summary>The reference split for data/scenario.json in Total mode (200.1 km).</summary>
    public static Plan ReferencePlan() =>
        new PlanBuilder(TaskScenario, Options(WorkdayMode.Total))
            .Route("A", ReferenceRouteA)
            .Route("B", ReferenceRouteB)
            .Build();

    public static TruckRoute Route(Plan plan, string truckId) => plan.Routes.Single(r => r.Truck.Id == truckId);

    // ---------- parsing of formatted strings (independent of Format) ----------

    private static readonly CultureInfo Croatian = CultureInfo.GetCultureInfo("hr-HR");

    /// <summary>"162,9 km" → 162.9; asserts the format "digits, comma, one digit, space, km".</summary>
    public static double ParseKm(string text)
    {
        Assert.Matches(new Regex(@"^\d+,\d km$"), text);
        return double.Parse(text[..^3], NumberStyles.Number, Croatian);
    }

    /// <summary>"10.000 kg" → 10000; asserts dot as thousands separator and no decimals.</summary>
    public static int ParseKg(string text)
    {
        Assert.Matches(new Regex(@"^\d{1,3}(\.\d{3})* kg$"), text);
        return int.Parse(text[..^3].Replace(".", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
    }

    /// <summary>"08:43" → 523 minutes; asserts at least two digits for hours and exactly two for minutes.</summary>
    public static int ParseClock(string text)
    {
        Assert.Matches(new Regex(@"^\d{2,}:[0-5]\d$"), text);
        var parts = text.Split(':');
        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    /// <summary>"97 %" → 97.</summary>
    public static int ParsePercent(string text)
    {
        Assert.Matches(new Regex(@"^\d+ %$"), text);
        return int.Parse(text[..^2], CultureInfo.InvariantCulture);
    }

    /// <summary>SVG attribute (invariant culture, decimal point).</summary>
    public static double ParseSvg(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Whole minutes as the UI must show them: midpoint away from zero (T42 b).</summary>
    public static int WholeMinutes(double minutes) => (int)Math.Round(minutes, MidpointRounding.AwayFromZero);

    /// <summary>Whole percentage of part / whole, midpoint away from zero, computed exactly in decimal.</summary>
    public static int WholePercent(double part, double whole) =>
        (int)Math.Round((decimal)part * 100m / (decimal)whole, MidpointRounding.AwayFromZero);
}
