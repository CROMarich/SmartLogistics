using System.Diagnostics;
using System.Globalization;
using System.Text;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using Xunit.Abstractions;

namespace SmartLogistics.Tests;

/// <summary>
/// Compares <see cref="ExactDpPlanner"/> with the independent <see cref="ReferenceOracle"/> (Dijkstra over
/// (location, order status) states) on the task data and on 20 seeded random scenarios, in both workday modes.
/// </summary>
public class ReferenceOracleTests(ITestOutputHelper output)
{
    /// <summary>Plan km is a sum of one-decimal legs; 0.05 separates neighbouring one-decimal values.</summary>
    private const double KmTolerance = 0.05;

    private const int ScenarioCount = 20;
    private const int BaseSeed = 20261121;

    public static TheoryData<int, WorkdayMode> RandomCases()
    {
        var data = new TheoryData<int, WorkdayMode>();
        for (var i = 0; i < ScenarioCount; i++)
        {
            data.Add(i, WorkdayMode.Total);
            data.Add(i, WorkdayMode.DrivingOnly);
        }

        return data;
    }

    [Theory]
    [Trait("Rule", "R24")]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total, 200.1)]       // CLAUDE.md: Total optimum 200.1 km
    [InlineData(WorkdayMode.DrivingOnly, 182.2)] // CLAUDE.md: DrivingOnly optimum 182.2 km
    public void Oracle_matches_the_reference_optimum_on_the_task_data(WorkdayMode mode, double expectedKm)
    {
        var options = PlanningOptions.Default with { WorkdayMode = mode };
        var watch = Stopwatch.StartNew();

        var result = ReferenceOracle.Solve(TestScenarios.Task, options);

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{mode}: {result} ({watch.ElapsedMilliseconds} ms)"));
        Assert.Equal(10, result.PlannedOrders); // spec 3.7: every order is feasible alone; all 10 fit together.
        Assert.Equal(expectedKm, result.TotalKm, KmTolerance);
    }

    [Theory]
    [Trait("Rule", "R24")]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R38")]
    [MemberData(nameof(RandomCases))]
    public void Exact_planner_matches_the_oracle_on_random_scenarios(int scenarioIndex, WorkdayMode mode)
    {
        var seed = BaseSeed + scenarioIndex;
        var scenario = RandomScenarioGenerator.Create(seed);
        var options = PlanningOptions.Default with { WorkdayMode = mode };

        var watch = Stopwatch.StartNew();
        var plan = new ExactDpPlanner().Solve(scenario, options);
        var plannerMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var oracle = ReferenceOracle.Solve(scenario, options);
        var oracleMs = watch.ElapsedMilliseconds;

        var heavierThanAllTrucks = scenario.Orders.Count(o =>
            !scenario.Trucks.Any(t => t.CanCarry(scenario.Machines.Single(m => m.Id == o.MachineId))));
        var plannedByPlanner = plan.Routes.SelectMany(r => r.Stops).Select(s => s.OrderId).Distinct(StringComparer.Ordinal).Count();

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"| {scenarioIndex + 1} | {mode} | {scenario.Orders.Count} | {heavierThanAllTrucks} | {plan.TotalKm:F1} | {oracle.TotalKm:F1} | {plannedByPlanner} / {oracle.PlannedOrders} | {plannerMs} / {oracleMs} ms |"));

        var context = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Scenario #{scenarioIndex + 1} (seed {seed}), {mode}, max {options.MaxMinutes} min")
            .AppendLine(RandomScenarioGenerator.Describe(scenario))
            .AppendLine(CultureInfo.InvariantCulture, $"Planner: {plannedByPlanner} orders, {plan.TotalKm:F1} km; {DescribeRoutes(plan)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Oracle:  {oracle}")
            .ToString();

        Assert.True(plannedByPlanner == oracle.PlannedOrders,
            $"Planned order count differs (R32).{Environment.NewLine}{context}");
        Assert.True(Math.Abs(plan.TotalKm - oracle.TotalKm) <= KmTolerance,
            $"Total km differs (R24).{Environment.NewLine}{context}");
    }

    [Theory]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R12")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R23")]
    [MemberData(nameof(RandomCases))]
    public void Exact_planner_plan_for_random_scenarios_passes_the_validator(int scenarioIndex, WorkdayMode mode)
    {
        var seed = BaseSeed + scenarioIndex;
        var scenario = RandomScenarioGenerator.Create(seed);
        var options = PlanningOptions.Default with { WorkdayMode = mode };

        var plan = new ExactDpPlanner().Solve(scenario, options);
        var violations = PlanValidator.Validate(plan, scenario, options);

        Assert.True(violations.Count == 0,
            $"Scenario #{scenarioIndex + 1} (seed {seed}), {mode}{Environment.NewLine}"
            + RandomScenarioGenerator.Describe(scenario) + Environment.NewLine
            + ViolationAssert.Describe(violations));
    }

    [Fact]
    [Trait("Rule", "R13")]
    [Trait("Rule", "R32")]
    public void Random_scenarios_cover_heavy_machines_and_a_limiting_workday()
    {
        // Guards against a vacuous comparison: with the fixed seeds the generator must produce at least one
        // order no truck can carry (K1), at least one B-only machine, and at least one scenario where the
        // working day leaves an order unplanned in Total mode (fewer orders than all carriable orders).
        var scenarios = Enumerable.Range(0, ScenarioCount).Select(i => RandomScenarioGenerator.Create(BaseSeed + i)).ToList();
        int Weight(Scenario s, TransportOrder o) => s.Machines.Single(m => m.Id == o.MachineId).WeightKg;

        Assert.Contains(scenarios, s => s.Orders.Any(o => Weight(s, o) > 10000));
        Assert.Contains(scenarios, s => s.Orders.Any(o => Weight(s, o) is > 3500 and <= 10000));
        Assert.Contains(scenarios, s =>
            ReferenceOracle.Solve(s, PlanningOptions.Default).PlannedOrders < s.Orders.Count(o => Weight(s, o) <= 10000));
    }

    private static string DescribeRoutes(Plan plan) =>
        string.Join("; ", plan.Routes.Select(r => string.Create(CultureInfo.InvariantCulture,
            $"{r.Truck.Id}: [{string.Join(" ", r.Stops.Select(s => $"{(s.Action == StopAction.Pickup ? "+" : "-")}{s.OrderId}@{s.LocationId}"))}] {r.TotalKm:F1} km {r.TotalMinutes:F1} min")))
        + "; infeasible: [" + string.Join(", ", plan.InfeasibleOrders.Select(i => $"{i.Order.Id} {i.Reason}")) + "]";
}

/// <summary>
/// Seeded generator of valid scenarios (no K4 data): trucks A 3500 kg / 2 and B 10000 kg / 3, base L0 at (0, 0),
/// 4–7 orders, unique machines, pickup != delivery. Sometimes heavier machines (B only, or heavier than every
/// truck = K1) and far locations (so that the working day limits the plan).
/// </summary>
internal static class RandomScenarioGenerator
{
    public static Scenario Create(int seed)
    {
        var random = new Random(seed);

        var locationCount = random.Next(4, 9); // L1..Ln in addition to L0
        var locations = new List<Location> { new("L0", 0, 0) };
        for (var i = 1; i <= locationCount; i++)
        {
            double x;
            double y;
            if (random.NextDouble() < 0.2)
            {
                // Far location: 80–260 km from the base, integer coordinates.
                var radius = random.Next(80, 261);
                var angle = random.NextDouble() * 2 * Math.PI;
                x = Math.Round(radius * Math.Cos(angle));
                y = Math.Round(radius * Math.Sin(angle));
            }
            else
            {
                x = random.Next(-25, 26);
                y = random.Next(-25, 26);
            }

            locations.Add(new Location($"L{i}", x, y));
        }

        var orderCount = random.Next(4, 8);
        var machines = new List<Machine>();
        var orders = new List<TransportOrder>();
        for (var i = 1; i <= orderCount; i++)
        {
            var kind = random.NextDouble();
            var weight = kind < 0.08
                ? random.Next(101, 131) * 100   // 10100–13000 kg: heavier than every truck (K1)
                : kind < 0.35
                    ? random.Next(36, 100) * 100 // 3600–9900 kg: only B
                    : random.Next(5, 36) * 100;  // 500–3500 kg: A or B
            var machineId = $"M{i:00}";
            machines.Add(new Machine(machineId, weight));

            var pickup = random.Next(locations.Count);
            var delivery = random.Next(locations.Count - 1);
            if (delivery >= pickup)
            {
                delivery++; // pickup != delivery
            }

            orders.Add(new TransportOrder($"N{i:00}", machineId, locations[pickup].Id, locations[delivery].Id));
        }

        return new Scenario("L0", [new Truck("A", 3500, 2), new Truck("B", 10000, 3)], machines, locations, orders);
    }

    /// <summary>The scenario as text, enough to rebuild it by hand.</summary>
    public static string Describe(Scenario scenario)
    {
        var text = new StringBuilder();
        text.AppendLine("Locations: " + string.Join(", ", scenario.Locations.Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.Id}({l.X},{l.Y})"))));
        text.AppendLine("Machines: " + string.Join(", ", scenario.Machines.Select(m => $"{m.Id}={m.WeightKg}kg")));
        text.Append("Orders: " + string.Join(", ", scenario.Orders.Select(o => $"{o.Id}:{o.MachineId} {o.PickupLocationId}->{o.DeliveryLocationId}")));
        return text.ToString();
    }
}
