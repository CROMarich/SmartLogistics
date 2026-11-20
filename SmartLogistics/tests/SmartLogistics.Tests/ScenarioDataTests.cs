using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Tests.Support;

namespace SmartLogistics.Tests;

/// <summary>data/scenario.json must contain exactly the task data (docs/zadatak.md), and the defaults must match the task.</summary>
public class ScenarioDataTests
{
    private const double Tolerance = 1e-9;

    private static Scenario Loaded => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath);

    [Fact]
    [Trait("Rule", "R1")]
    public void Scenario_file_has_exactly_trucks_A_3500_2_and_B_10000_3()
    {
        Assert.Equal([new Truck("A", 3500, 2), new Truck("B", 10000, 3)], Loaded.Trucks);
    }

    [Fact]
    [Trait("Rule", "R2")]
    public void Scenario_file_base_is_L0()
    {
        Assert.Equal("L0", Loaded.BaseLocationId);
    }

    [Fact]
    [Trait("Rule", "R8")]
    public void Scenario_file_machines_locations_and_orders_match_the_task()
    {
        var loaded = Loaded;
        var expected = TestScenarios.Task;

        Assert.Equal(expected.Machines, loaded.Machines);
        Assert.Equal(expected.Orders, loaded.Orders);
        Assert.Equal(expected.Locations.Select(l => l.Id), loaded.Locations.Select(l => l.Id));
        foreach (var (e, a) in expected.Locations.Zip(loaded.Locations))
        {
            Assert.Equal(e.X, a.X, Tolerance);
            Assert.Equal(e.Y, a.Y, Tolerance);
        }
    }

    [Fact]
    [Trait("Rule", "R22")]
    [Trait("Rule", "R17")]
    [Trait("Rule", "R18")]
    public void Default_options_are_Total_540_min_20_min_per_action_60_kmh()
    {
        var options = PlanningOptions.Default;

        Assert.Equal(WorkdayMode.Total, options.WorkdayMode);
        Assert.Equal(WorkdayMode.Total, new PlanningOptions().WorkdayMode);
        Assert.Equal(540.0, options.MaxMinutes, Tolerance);
        Assert.Equal(20.0, options.MinutesPerAction, Tolerance);
        Assert.Equal(60.0, options.SpeedKmh, Tolerance);
    }

    [Theory]
    [Trait("Rule", "R17")]
    [InlineData(12.5)]
    [InlineData(28.7)]
    [InlineData(200.1)]
    public void At_60_kmh_driving_minutes_equal_km(double km)
    {
        Assert.Equal(km, PlanningOptions.Default.DrivingMinutes(km), Tolerance);
    }
}
