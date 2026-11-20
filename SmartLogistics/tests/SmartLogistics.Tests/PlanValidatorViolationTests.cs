using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.PlanBuilder;

namespace SmartLogistics.Tests;

/// <summary>
/// Hand-built invalid plans: each one breaks one rule (on top of an otherwise correct plan built by
/// <see cref="PlanBuilder"/>) and the validator must report that rule. One error may cause several
/// violations, so the tests only require the rule (and, where it applies, truck and step) to be among them.
/// </summary>
public class PlanValidatorViolationTests
{
    private static readonly PlanningOptions Options = PlanningOptions.Default;

    private static IReadOnlyList<PlanViolation> Validate(Plan plan, Scenario scenario, PlanningOptions? options = null) =>
        PlanValidator.Validate(plan, scenario, options ?? Options);

    // ---------- R1: trucks of the plan must be the scenario trucks ----------

    [Fact]
    [Trait("Rule", "R1")]
    public void Route_for_a_truck_that_is_not_in_the_scenario_is_R1()
    {
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var plan = builder
            .Route(builder.BuildRoute(new Truck("C", 3500, 2), [Pickup("N01"), Delivery("N01")]))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R1", truckId: "C");
    }

    [Fact]
    [Trait("Rule", "R1")]
    public void Truck_with_limits_different_from_the_scenario_is_R1()
    {
        // The plan claims A can carry 5000 kg; the scenario says 3500 kg (docs/odluke.md T15).
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var plan = builder
            .Route(builder.BuildRoute(new Truck("A", 5000, 2), [Pickup("N01"), Delivery("N01")]))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R1", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R1")]
    public void Two_routes_for_the_same_truck_are_R1()
    {
        var scenario = TestScenarios.TaskWithOrders("N01", "N02");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("A", Pickup("N02"), Delivery("N02"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R1", truckId: "A");
    }

    // ---------- R2: route ends in L0 ----------

    [Fact]
    [Trait("Rule", "R2")]
    public void Route_that_does_not_return_to_base_is_R2()
    {
        // A: L0 -> L1 -> L3 reported as 8.5 + 14.0 = 22.5 km, i.e. without the return leg L3-L0 = 19.2
        // (docs/odluke.md T13: km equal to the sum without the return leg means "no return").
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var plan = builder
            .Route(builder.BuildRoute(builder.Truck("A"), [Pickup("N01"), Delivery("N01")], returnToBase: false))
            .Build();

        Assert.Equal(22.5, plan.Routes[0].TotalKm, 1e-9);
        ViolationAssert.Contains(Validate(plan, scenario), "R2", truckId: "A");
    }

    // ---------- R4: exactly one pickup and one delivery per order ----------

    [Fact]
    [Trait("Rule", "R4")]
    public void Order_picked_up_twice_is_R4()
    {
        // A: P N01, P N01, D N01, D N01 (loads 800, 1600, 800, 0 - capacity is fine, the order is not).
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Pickup("N01"), Delivery("N01"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R4");
    }

    [Fact]
    [Trait("Rule", "R4")]
    public void Order_picked_up_and_never_delivered_is_R4()
    {
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R4");
    }

    // ---------- R5, R6, R7: same truck, pickup before delivery, one truck per order ----------

    [Fact]
    [Trait("Rule", "R5")]
    public void Pickup_on_A_and_delivery_on_B_is_R5()
    {
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"))
            .Route("B", Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R5");
    }

    [Fact]
    [Trait("Rule", "R6")]
    [Trait("Rule", "R9")]
    public void Delivery_before_pickup_on_the_same_truck_is_R6()
    {
        // RS11: A: L0 -> L3 (D N01) -> L1 (P N01) -> L0. Load after step 0 would be -800 kg.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Delivery("N01"), Pickup("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R6", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R7")]
    public void Same_order_on_both_trucks_is_R7()
    {
        // Each route on its own is complete and correct; together they carry N01 twice.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N01"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R7");
    }

    // ---------- R8: action at the order's location ----------

    [Fact]
    [Trait("Rule", "R8")]
    public void Pickup_at_the_wrong_location_is_R8()
    {
        // N01 is picked up at L1; the plan picks it up at L2 (km computed for L2, so only the location is wrong).
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01").At("L2"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R8", truckId: "A", stepIndex: 0);
    }

    [Fact]
    [Trait("Rule", "R8")]
    public void Delivery_at_the_wrong_location_is_R8()
    {
        // N01 is delivered at L3; the plan delivers it at L5.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01").At("L5"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R8", truckId: "A", stepIndex: 1);
    }

    // ---------- R9, R10: reported load after each action ----------

    [Fact]
    [Trait("Rule", "R10")]
    public void Wrong_reported_load_kg_after_an_action_is_R10()
    {
        // After picking up M01 the load is 800 kg; the plan reports 1300 kg.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"))
            .WithStop(0, s => s with { LoadKgAfter = 1300 });
        var plan = builder.Route(route).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R10", truckId: "A", stepIndex: 0);
    }

    [Fact]
    [Trait("Rule", "R10")]
    public void Wrong_reported_machine_count_after_an_action_is_R10()
    {
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"))
            .WithStop(0, s => s with { MachinesAfter = 2 });
        var plan = builder.Route(route).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R10", truckId: "A", stepIndex: 0);
    }

    [Fact]
    [Trait("Rule", "R9")]
    [Trait("Rule", "R34")]
    public void Load_reported_as_if_the_truck_started_loaded_is_R10()
    {
        // Trucks start empty (R9, R34). Reporting 500 kg more after every action (1300, 500)
        // means the plan assumes a starting load; the first wrong value is at step 0.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"))
            .WithAllStops(s => s with { LoadKgAfter = s.LoadKgAfter + 500 });
        var plan = builder.Route(route).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R10", truckId: "A", stepIndex: 0);
    }

    // ---------- R11, R13: weight <= capacity after each action ----------

    [Fact]
    [Trait("Rule", "R11")]
    public void A_carrying_M02_and_M04_together_is_R11_at_the_second_pickup()
    {
        // A: L0 -> L2 (P N02) -> L3 (P N04) -> L5 (D N02) -> L0 (D N04) -> L0
        //   loads: 1300, 1300 + 2400 = 3700 (> 3500, step 1), 2400, 0  (spec 3.3: M02 + M04 = 3700)
        //   km: 14.9 + 7.3 + 10.8 + 26.2 + 0.0 = 59.2
        var scenario = TestScenarios.TaskWithOrders("N02", "N04");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N02"), Pickup("N04"), Delivery("N02"), Delivery("N04"))
            .Build();

        var violations = Validate(plan, scenario);

        ViolationAssert.Contains(violations, "R11", truckId: "A", stepIndex: 1);
        Assert.False(
            violations.Any(v => v.RuleId == "R11" && v.StepIndex != 1),
            "R11 only at step 1 (3700 kg); steps 0, 2, 3 carry 1300, 2400, 0 kg:" + Environment.NewLine + ViolationAssert.Describe(violations));
    }

    [Fact]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R13")]
    public void Machine_heavier_than_A_assigned_to_A_is_R11()
    {
        // RS7: N06 (M06 = 3900 kg) on A (3500 kg) is over capacity right after the pickup (step 0).
        var scenario = TestScenarios.TaskWithOrders("N06");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N06"), Delivery("N06"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R11", truckId: "A", stepIndex: 0);
    }

    [Fact]
    [Trait("Rule", "R11")]
    public void M10_together_with_another_machine_on_B_is_R11()
    {
        // RS8: B: P N01 (800) at L1, then P N10 (9400) at L7 -> 10200 kg > 10000 at step 1.
        var scenario = TestScenarios.TaskWithOrders("N01", "N10");
        var plan = new PlanBuilder(scenario)
            .Route("B", Pickup("N01"), Pickup("N10"), Delivery("N10"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R11", truckId: "B", stepIndex: 1);
    }

    // ---------- R12: machine count <= limit after each action ----------

    [Fact]
    [Trait("Rule", "R12")]
    public void Three_light_machines_on_A_is_R12_without_R11()
    {
        // Own scenario: three 100 kg machines (300 kg <= 3500), so only the count (3 > 2) is broken.
        // A: L0 -> P1 (P Q1, P Q2, P Q3) -> P2 (D Q1, D Q2, D Q3) -> L0; the third machine is loaded at step 2.
        var scenario = TestScenarios.LightMachines;
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("Q1"), Pickup("Q2"), Pickup("Q3"), Delivery("Q1"), Delivery("Q2"), Delivery("Q3"))
            .Build();

        var violations = Validate(plan, scenario);

        ViolationAssert.Contains(violations, "R12", truckId: "A", stepIndex: 2);
        ViolationAssert.DoesNotContain(violations, "R11");
    }

    [Fact]
    [Trait("Rule", "R12")]
    public void Four_machines_on_B_is_R12_without_R11()
    {
        // RS10: B picks up M01, M02, M04, M03 -> 800, 2100, 4500, 6300 kg (<= 10000) but 4 > 3 machines at step 3.
        var scenario = TestScenarios.TaskWithOrders("N01", "N02", "N03", "N04");
        var plan = new PlanBuilder(scenario)
            .Route("B",
                Pickup("N01"), Pickup("N02"), Pickup("N04"), Pickup("N03"),
                Delivery("N03"), Delivery("N04"), Delivery("N01"), Delivery("N02"))
            .Build();

        var violations = Validate(plan, scenario);

        ViolationAssert.Contains(violations, "R12", truckId: "B", stepIndex: 3);
        ViolationAssert.DoesNotContain(violations, "R11");
    }

    // ---------- R16, R17, R18, R19: reported km and minutes ----------

    [Fact]
    [Trait("Rule", "R16")]
    public void Wrong_reported_route_km_is_R16()
    {
        // A: N01 is 41.7 km; the plan reports 42.7 km (plan total kept consistent with the routes).
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"));
        var plan = builder.Route(route with { TotalKm = route.TotalKm + 1 }).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R16", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R16")]
    public void Wrong_reported_plan_total_km_is_R16()
    {
        // Routes are correct (41.7 + 0), the plan total says 50.0.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build() with { TotalKm = 50.0 };

        ViolationAssert.Contains(Validate(plan, scenario), "R16");
    }

    [Fact]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R14")]
    public void Wrong_reported_leg_km_at_a_stop_is_R16()
    {
        // Leg L1-L3 is 14.0 km (sqrt(197) = 14.0357 -> 14.0); the plan reports 14.1 at step 1.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"))
            .WithStop(1, s => s with { LegKm = 14.1, CumulativeKm = 22.6 });
        var plan = builder.Route(route).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R16", truckId: "A", stepIndex: 1);
    }

    [Fact]
    [Trait("Rule", "R17")]
    public void Wrong_reported_driving_minutes_is_R17()
    {
        // 41.7 km at 60 km/h = 41.7 min; the plan reports 46.7 min.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"));
        var plan = builder.Route(route with { DrivingMinutes = route.DrivingMinutes + 5 }).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R17", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R18")]
    public void Wrong_reported_action_minutes_is_R18()
    {
        // 2 actions x 20 min = 40 min; the plan reports 20 min.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"));
        var plan = builder.Route(route with { ActionMinutes = 20 }).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R18", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R19")]
    [Trait("Rule", "R18")]
    public void Counting_the_return_to_base_as_an_action_is_R18()
    {
        // RS2: the return is not an action. 2 actions = 40 min; counting the return gives 3 x 20 = 60 min.
        var scenario = TestScenarios.TaskWithOrders("N04");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N04"), Delivery("N04"));
        var plan = builder.Route(route with { ActionMinutes = 60 }).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R18", truckId: "A");
    }

    // ---------- R20, R21, R29: working day ----------

    [Fact]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R22")]
    public void Exceeded_workday_in_Total_mode_is_R20()
    {
        // RS15 (negative half), limit 90 min:
        //   A (N01): 41.7 + 40 = 81.7 min  <= 90 -> fine
        //   B (N06): 60.2 + 40 = 100.2 min > 90 -> R20 (driving alone, 60.2, would fit: see the DrivingOnly test)
        var options = new PlanningOptions(WorkdayMode.Total, MaxMinutes: 90);
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        var violations = Validate(plan, scenario, options);

        ViolationAssert.Contains(violations, "R20", truckId: "B");
        ViolationAssert.DoesNotContain(violations, "R20", truckId: "A");
        ViolationAssert.DoesNotContain(violations, "R21");
    }

    [Fact]
    [Trait("Rule", "R21")]
    public void Exceeded_driving_time_in_DrivingOnly_mode_is_R21()
    {
        // Limit 50 min of driving: A drives 41.7 (fine), B drives 60.2 (> 50 -> R21).
        var options = new PlanningOptions(WorkdayMode.DrivingOnly, MaxMinutes: 50);
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route("B", Pickup("N06"), Delivery("N06"))
            .Build();

        var violations = Validate(plan, scenario, options);

        ViolationAssert.Contains(violations, "R21", truckId: "B");
        ViolationAssert.DoesNotContain(violations, "R21", truckId: "A");
        ViolationAssert.DoesNotContain(violations, "R20");
    }

    [Fact]
    [Trait("Rule", "R29")]
    [Trait("Rule", "R20")]
    public void Workday_includes_the_return_leg_in_Total_mode()
    {
        // A (N01), limit 70 min: last action finishes at 8.5 + 20 + 14.0 + 20 = 62.5 min (<= 70),
        // but the day ends back in L0: 62.5 + 19.2 = 81.7 min > 70 -> R20.
        var options = new PlanningOptions(WorkdayMode.Total, MaxMinutes: 70);
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build();

        Assert.Equal(62.5, plan.Routes[0].Stops[^1].CumulativeMinutes, 1e-9);
        ViolationAssert.Contains(Validate(plan, scenario, options), "R20", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R29")]
    [Trait("Rule", "R21")]
    public void Workday_includes_the_return_leg_in_DrivingOnly_mode()
    {
        // A (N01), limit 30 min: driving to the last action 8.5 + 14.0 = 22.5 (<= 30),
        // with the return 22.5 + 19.2 = 41.7 > 30 -> R21.
        var options = new PlanningOptions(WorkdayMode.DrivingOnly, MaxMinutes: 30);
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario, options), "R21", truckId: "A");
    }

    // ---------- R23, R33: every order planned or infeasible with a message ----------

    [Fact]
    [Trait("Rule", "R23")]
    public void Order_neither_planned_nor_listed_as_infeasible_is_R23()
    {
        // Scenario has N01 and N06; the plan only carries N01 and says nothing about N06.
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R23");
    }

    [Fact]
    [Trait("Rule", "R23")]
    public void Order_both_planned_and_listed_as_infeasible_is_R23()
    {
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Infeasible("N01")
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R23");
    }

    [Theory]
    [Trait("Rule", "R33")]
    [InlineData("")]
    [InlineData("   ")]
    public void Infeasible_order_without_a_message_is_R33(string message)
    {
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Infeasible("N06", message)
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R33");
    }

    [Theory]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Feasible_order_listed_as_infeasible_is_R32(WorkdayMode mode)
    {
        // R32: a feasible order must not be left out to save km. N06 alone on B needs
        // 60.2 km driving (+ 40 min actions = 100.2 min in Total), far below 540 min, and B is empty,
        // so listing N06 as K3 "does not fit with the other orders" is not allowed.
        var options = PlanningOptions.Default with { WorkdayMode = mode };
        var scenario = TestScenarios.TaskWithOrders("N01", "N06");
        var plan = new PlanBuilder(scenario, options)
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Infeasible("N06", "Test: N06 does not fit into the plan with the other orders.")
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario, options), "R32");
    }

    // ---------- R26, R28, R36 ----------

    [Fact]
    [Trait("Rule", "R26")]
    public void Wrong_reported_order_count_is_R26()
    {
        // A carries one order (1 pickup, 1 delivery); the plan reports 2.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var route = builder.BuildRoute("A", Pickup("N01"), Delivery("N01"));
        var plan = builder.Route(route with { OrderCount = 2 }).Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R26", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R28")]
    public void Machine_left_on_the_truck_at_the_end_is_R28()
    {
        // A picks up M01 (800 kg) and returns to L0 with it: load after the last action is 800 kg / 1 machine.
        var scenario = TestScenarios.TaskWithOrders("N01");
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R28", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R36")]
    [Trait("Rule", "R3")]
    public void Unused_truck_with_non_zero_km_is_R36()
    {
        // B has no stops but reports 5 km (e.g. a phantom L0 -> L0 tour).
        var scenario = TestScenarios.TaskWithOrders("N01");
        var builder = new PlanBuilder(scenario);
        var plan = builder
            .Route("A", Pickup("N01"), Delivery("N01"))
            .Route(UnusedRoute(builder.Truck("B")) with { TotalKm = 5, DrivingMinutes = 5 })
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R36", truckId: "B");
    }
}
