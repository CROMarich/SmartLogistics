using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.PlanBuilder;

namespace SmartLogistics.Tests;

/// <summary>Hand-built plans that respect every rule must pass the validator with no violations.</summary>
public class PlanValidatorValidPlanTests
{
    private static IReadOnlyList<PlanViolation> Validate(Plan plan, Scenario scenario, PlanningOptions options) =>
        PlanValidator.Validate(plan, scenario, options);

    [Theory]
    [Trait("Rule", "R2")]
    [Trait("Rule", "R4")]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Small_plan_A_N01_B_N06_has_no_violations(WorkdayMode mode)
    {
        // Scenario reduced to N01 and N06 (all other input unchanged).
        // A: L0 -> L1 (P N01) -> L3 (D N01) -> L0 = 8.5 + 14.0 + 19.2 = 41.7 km, 81.7 min with actions
        // B: L0 -> L5 (P N06) -> L7 (D N06) -> L0 = 26.2 + 12.5 + 21.5 = 60.2 km, 100.2 min with actions
        // plan = 101.9 km (checked by PlanBuilderTests)
        var options = PlanningOptions.Default with { WorkdayMode = mode };
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        ViolationAssert.Empty(Validate(plan, scenario, options));
    }

    [Theory]
    [Trait("Rule", "R23")]
    [Trait("Rule", "R33")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Order_heavier_than_every_truck_listed_as_K1_with_message_is_valid(WorkdayMode mode)
    {
        // RS6 (hypothetical): N11 moves M11 = 12000 kg (> 10000 kg B, > 3500 kg A), so it is infeasible (K1).
        // N01 is planned on A (41.7 km); N11 is listed with reason and numbers; B is unused.
        var options = PlanningOptions.Default with { WorkdayMode = mode };
        var task = TestScenarios.TaskWithOrders("N01");
        var scenario = task with
        {
            Machines = [.. task.Machines, new Machine("M11", 12000)],
            Orders = [.. task.Orders, new TransportOrder("N11", "M11", "L1", "L2")],
        };
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Infeasible("N11", "M11 weighs 12000 kg > 10000 kg (B) and > 3500 kg (A).", InfeasibilityReason.ExceedsAllCapacities)
            .Build();

        ViolationAssert.Empty(Validate(plan, scenario, options));
    }

    [Fact]
    [Trait("Rule", "R3")]
    [Trait("Rule", "R36")]
    public void Unused_truck_with_no_stops_and_zero_km_is_not_a_violation()
    {
        // A: N01 = 41.7 km; B has no stops, 0 km, 0 orders. Plan = 41.7 km.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build();

        Assert.Empty(plan.Routes.Single(r => r.Truck.Id == "B").Stops);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R3")]
    [Trait("Rule", "R16")]
    public void Plan_with_no_orders_and_both_trucks_unused_has_no_violations()
    {
        // RS19: empty order list -> both trucks unused, 0 km.
        var scenario = TestScenarios.TaskWithOrders();
        var plan = new PlanBuilder(scenario).Build();

        Assert.Equal(0.0, plan.TotalKm, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R15")]
    [Trait("Rule", "R19")]
    public void Delivery_in_base_as_last_action_gives_zero_km_return_and_is_valid()
    {
        // RS2: A: L0 -> L3 (P N04) -> L0 (D N04) -> L0 = 19.2 + 19.2 + 0.0 = 38.4 km.
        // The delivery in L0 is an action (20 min); the 0 km return is not: 38.4 + 2 x 20 = 78.4 min.
        var scenario = TestScenarios.TaskWithOrders("N04");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N04"), Delivery("N04"))
            .Build();

        var a = plan.Routes.Single(r => r.Truck.Id == "A");
        Assert.Equal(38.4, a.TotalKm, 1e-9);
        Assert.Equal(78.4, a.TotalMinutes, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R15")]
    [Trait("Rule", "R18")]
    [Trait("Rule", "R31")]
    public void Actions_in_base_mid_route_are_valid()
    {
        // RS1 + RS3: A: L0 -> L3 (P N04) -> L0 (D N04) -> L0 (P N05) -> L8 (D N05) -> L0
        //   = 19.2 + 19.2 + 0.0 + 27.5 + 27.5 = 93.4 km; 4 actions = 80 min (both base actions count).
        //   loads: 2400, 0, 3100, 0 (M04 and M05 are never on A together: 5500 > 3500 would break R11).
        var scenario = TestScenarios.TaskWithOrders("N04", "N05");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N04"), Delivery("N04"), Pickup("N05"), Delivery("N05"))
            .Build();

        var a = plan.Routes.Single(r => r.Truck.Id == "A");
        Assert.Equal(93.4, a.TotalKm, 1e-9);
        Assert.Equal(80.0, a.ActionMinutes, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R15")]
    public void Pickup_in_base_as_first_action_is_valid()
    {
        // RS1: A: L0 (P N05) -> L8 (D N05) -> L0 = 0.0 + 27.5 + 27.5 = 55.0 km (spec 3.7 row N05).
        var scenario = TestScenarios.TaskWithOrders("N05");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N05"), Delivery("N05"))
            .Build();

        Assert.Equal(55.0, plan.TotalKm, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R11")]
    public void Load_exactly_at_capacity_is_valid()
    {
        // RS9: B carries M08 + M06 = 6100 + 3900 = 10000 kg = capacity (allowed, <=).
        // B: L0 -> L4 (P N08) -> L5 (P N06) -> L7 (D N06) -> L9 (D N08) -> L0
        //   = 10.8 + 19.2 + 12.5 + 28.2 + 9.4 = 80.1 km; loads 6100, 10000, 6100, 0.
        var scenario = TestScenarios.TaskWithOrders("N06", "N08");
        var plan = new PlanBuilder(scenario)
            .Route("B", Pickup("N08"), Pickup("N06"), Delivery("N06"), Delivery("N08"))
            .Build();

        var b = plan.Routes.Single(r => r.Truck.Id == "B");
        Assert.Equal(10000, b.Stops[1].LoadKgAfter);
        Assert.Equal(80.1, b.TotalKm, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Fact]
    [Trait("Rule", "R12")]
    public void Machine_count_exactly_at_limit_is_valid()
    {
        // A carries M01 + M02 = 2 machines (limit 2), 2100 kg (spec 3.3).
        // A: L0 -> L1 (P N01) -> L2 (P N02) -> L3 (D N01) -> L5 (D N02) -> L0
        //   = 8.5 + 7.6 + 7.3 + 10.8 + 26.2 = 60.4 km
        var scenario = TestScenarios.TaskWithOrders("N01", "N02");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Pickup("N02"), Delivery("N01"), Delivery("N02"))
            .Build();

        Assert.Equal(60.4, plan.TotalKm, 1e-9);
        ViolationAssert.Empty(Validate(plan, scenario, PlanningOptions.Default));
    }

    [Theory]
    [Trait("Rule", "R35")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total, 100.2)]      // B: 60.2 km driving + 2 x 20 min = 100.2 min
    [InlineData(WorkdayMode.DrivingOnly, 60.2)] // B: 26.2 + 12.5 + 21.5 = 60.2 min driving
    public void Workday_exactly_at_the_limit_is_valid(WorkdayMode mode, double maxMinutes)
    {
        // The limit is inclusive and must tolerate floating-point sums of one-decimal legs (R35).
        var options = new PlanningOptions(mode, maxMinutes);
        var scenario = TestScenarios.TaskWithOrders("N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        ViolationAssert.Empty(Validate(plan, scenario, options));
    }

    [Fact]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R22")]
    public void Plan_over_the_limit_only_because_of_actions_is_valid_in_DrivingOnly()
    {
        // RS15 (positive half): B with N06 drives 60.2 min and needs 100.2 min with actions.
        // With a 90 min limit it passes DrivingOnly (60.2 <= 90); the Total half is a negative test.
        var options = new PlanningOptions(WorkdayMode.DrivingOnly, MaxMinutes: 90);
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        ViolationAssert.Empty(Validate(plan, scenario, options));
    }

    [Fact]
    [Trait("Rule", "R1")]
    public void Validator_reports_which_rules_it_checks()
    {
        // Rule IDs of the agreed check table (docs/odluke.md T12, T13, phase 3 task).
        string[] expected = ["R1", "R2", "R4", "R5", "R6", "R7", "R8", "R10", "R11", "R12", "R16", "R17", "R18",
            "R20", "R21", "R23", "R26", "R28", "R33", "R36"];

        var missing = expected.Except(PlanValidator.CheckedRuleIds).ToList();

        Assert.True(missing.Count == 0, "CheckedRuleIds is missing: " + string.Join(", ", missing));
    }
}
