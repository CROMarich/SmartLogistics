using SmartLogistics.Core.Domain;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.RobustPlanning;

namespace SmartLogistics.Tests;

/// <summary>
/// Phase 5, amended R37 and R40: invalid input never stops planning, the planner always returns a plan.
/// Every scenario is the task data (TestScenarios.Task = data/scenario.json, checked by ScenarioDataTests)
/// plus one broken order or one broken input element. Unless stated otherwise the 10 task orders stay
/// valid, so the remaining plan is the task optimum: 200.1 km (Total) / 182.2 km (DrivingOnly), CLAUDE.md.
/// </summary>
public class InvalidInputPlanningTests
{
    private static readonly Machine M11Light = new("M11", 1000);

    /// <summary>Common checks for "one extra invalid order": K4, 10 task orders planned at the optimum, valid plan.</summary>
    private static void AssertOnlyExtraOrderIsK4(Scenario scenario, TransportOrder invalidOrder, WorkdayMode mode)
    {
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        SingleInfeasible(plan, invalidOrder, InfeasibilityReason.InvalidOrderData);
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(TestScenarios.Task.Orders.Select(o => o.Id).Order(StringComparer.Ordinal), PlannedOrderIds(plan));
        Assert.Equal(ReferenceKm(mode), plan.TotalKm, KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    // ---- Mandatory test 1: machine heavier than every truck (K1, R40 message) ----

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R40")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Machine_of_11000_kg_is_K1_and_the_other_ten_orders_are_planned_at_the_optimum(WorkdayMode mode)
    {
        // M11 = 11000 kg > 10000 kg (B) > 3500 kg (A): no truck can carry it (R13) -> K1 (R33).
        // Without N11 the scenario is the task -> 200.1 km Total / 182.2 km DrivingOnly (CLAUDE.md).
        var n11 = new TransportOrder("N11", "M11", "L1", "L2");
        var scenario = TaskWith([new Machine("M11", 11000)], n11);
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        var entry = SingleInfeasible(plan, n11, InfeasibilityReason.ExceedsAllCapacities);
        Assert.Contains("Stroj M11", entry.Message, StringComparison.Ordinal);
        Assert.Contains("teži je od nosivosti svih kamiona", entry.Message, StringComparison.Ordinal);
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(10, PlannedOrderIds(plan).Count);
        Assert.Equal(ReferenceKm(mode), plan.TotalKm, KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    [Fact]
    [Trait("Rule", "R40")]
    public void K1_message_contains_the_machine_weight_and_the_truck_capacities()
    {
        // R40: "Stroj Mxx (N kg) teži je od nosivosti svih kamiona" (with the capacities):
        // the numbers 11000 (M11), 10000 (B) and 3500 (A) must appear, in any thousands formatting.
        var n11 = new TransportOrder("N11", "M11", "L1", "L2");
        var scenario = TaskWith([new Machine("M11", 11000)], n11);

        var plan = SolveWithoutException(scenario, Options(WorkdayMode.Total));

        var entry = SingleInfeasible(plan, n11, InfeasibilityReason.ExceedsAllCapacities);
        var message = WithoutThousandsSeparators(entry.Message);
        Assert.Contains("11000", message, StringComparison.Ordinal);
        Assert.Contains("10000", message, StringComparison.Ordinal);
        Assert.Contains("3500", message, StringComparison.Ordinal);
    }

    // ---- Mandatory test 3: unknown location ----

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total, "L99", "L2")]
    [InlineData(WorkdayMode.DrivingOnly, "L99", "L2")]
    [InlineData(WorkdayMode.Total, "L1", "L99")]
    [InlineData(WorkdayMode.DrivingOnly, "L1", "L99")]
    public void Order_with_unknown_location_is_K4_and_the_other_ten_orders_are_planned(WorkdayMode mode, string pickup, string delivery)
    {
        var n11 = new TransportOrder("N11", "M11", pickup, delivery);
        AssertOnlyExtraOrderIsK4(TaskWith([M11Light], n11), n11, mode);
    }

    // ---- "The application must never fail": one invalid order each ----

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Order_with_pickup_equal_to_delivery_is_K4(WorkdayMode mode)
    {
        var n11 = new TransportOrder("N11", "M11", "L1", "L1");
        AssertOnlyExtraOrderIsK4(TaskWith([M11Light], n11), n11, mode);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total, 0)]
    [InlineData(WorkdayMode.DrivingOnly, 0)]
    [InlineData(WorkdayMode.Total, -500)]
    [InlineData(WorkdayMode.DrivingOnly, -500)]
    public void Order_whose_machine_weight_is_not_positive_is_K4(WorkdayMode mode, int weightKg)
    {
        var n11 = new TransportOrder("N11", "M11", "L1", "L2");
        AssertOnlyExtraOrderIsK4(TaskWith([new Machine("M11", weightKg)], n11), n11, mode);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Second_order_with_a_duplicate_id_is_K4_and_the_first_is_planned(WorkdayMode mode)
    {
        // Second "N05" (valid on its own: M11 1000 kg, L1 -> L2). R37: the second and every later order
        // with the same ID is K4; the first N05 (M05, L0 -> L8) stays a normal task order.
        var secondN05 = new TransportOrder("N05", "M11", "L1", "L2");
        AssertOnlyExtraOrderIsK4(TaskWith([M11Light], secondN05), secondN05, mode);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Order_with_unknown_machine_is_K4(WorkdayMode mode)
    {
        var n11 = new TransportOrder("N11", "M99", "L1", "L2");
        AssertOnlyExtraOrderIsK4(TaskWith([], n11), n11, mode);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Later_order_reusing_a_machine_is_K4_and_the_earlier_order_is_planned(WorkdayMode mode)
    {
        // M01 is already used by N01 (earlier in the input); R37 "machine already used in an earlier order".
        var n11 = new TransportOrder("N11", "M01", "L2", "L5");
        AssertOnlyExtraOrderIsK4(TaskWith([], n11), n11, mode);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R3")]
    [Trait("Rule", "R36")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Empty_order_list_gives_an_empty_valid_plan(WorkdayMode mode)
    {
        var scenario = TestScenarios.Task with { Orders = [] };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        Assert.Empty(plan.InfeasibleOrders);
        Assert.All(plan.Routes, r => Assert.Empty(r.Stops));
        Assert.All(plan.Routes, r => Assert.Equal(0, r.TotalKm, KmTolerance));
        Assert.Equal(0, plan.TotalKm, KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    // ---- Further R37 cases (spec R37 lists them; phase-5 task does not name them, see docs/odluke.md T32) ----

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Without_a_base_location_every_order_is_K4_and_no_truck_has_actions(WorkdayMode mode)
    {
        var scenario = TestScenarios.Task with { Locations = TestScenarios.Task.Locations.Where(l => l.Id != "L0").ToList() };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        Assert.All(plan.Routes, r => Assert.Empty(r.Stops));
        Assert.Equal(0, plan.TotalKm, KmTolerance);
        Assert.Equal(scenario.Orders.Count, plan.InfeasibleOrders.Count);
        Assert.All(plan.InfeasibleOrders, e =>
        {
            Assert.Equal(InfeasibilityReason.InvalidOrderData, e.Reason);
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
        });
        EachOrderExactlyOnce(plan, scenario);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Order_using_a_duplicated_machine_id_is_K4(WorkdayMode mode)
    {
        // A second machine "M01" makes the ID ambiguous; R37: orders using it (N01) are K4.
        var scenario = TestScenarios.Task with { Machines = [.. TestScenarios.Task.Machines, new Machine("M01", 900)] };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        SingleInfeasible(plan, TestScenarios.Task.Orders.Single(o => o.Id == "N01"), InfeasibilityReason.InvalidOrderData);
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(9, PlannedOrderIds(plan).Count);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Orders_using_a_duplicated_location_id_are_K4(WorkdayMode mode)
    {
        // A second "L1" makes the ID ambiguous; R37: N01 (L1 -> L3) and N09 (L6 -> L1) are K4, the other 8 are planned.
        var scenario = TestScenarios.Task with { Locations = [.. TestScenarios.Task.Locations, new Location("L1", 50, 50)] };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        AssertInfeasibleAreK4(plan, "N01", "N09");
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(8, PlannedOrderIds(plan).Count);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Orders_at_a_location_with_non_numeric_coordinates_are_K4(WorkdayMode mode)
    {
        // L9 gets X = NaN; R37: N03 (L9 -> L4) and N08 (L4 -> L9) are K4, the other 8 are planned.
        var scenario = TestScenarios.Task with
        {
            Locations = TestScenarios.Task.Locations.Select(l => l.Id == "L9" ? l with { X = double.NaN } : l).ToList(),
        };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        AssertInfeasibleAreK4(plan, "N03", "N08");
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(8, PlannedOrderIds(plan).Count);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total, 0, 3)]
    [InlineData(WorkdayMode.Total, -1, 3)]
    [InlineData(WorkdayMode.Total, 10000, 0)]
    [InlineData(WorkdayMode.DrivingOnly, 0, 3)]
    public void Unusable_truck_B_gets_no_actions_and_orders_only_B_could_carry_are_K1(WorkdayMode mode, int capacityKg, int maxMachines)
    {
        // R37: a truck with capacity or machine limit <= 0 is not used. Only A (3500 kg) remains, so
        // N06..N10 (M06 3900, M07 4800, M08 6100, M09 7500, M10 9400 kg, all > 3500 kg) are K1.
        // N01..N05 (800..3100 kg) fit A; each is either planned on A or K3 (A alone may not fit all into the day).
        var scenario = TestScenarios.Task with
        {
            Trucks = [new Truck("A", 3500, 2), new Truck("B", capacityKg, maxMachines)],
        };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        Assert.All(plan.Routes.Where(r => r.Truck.Id == "B"), r => Assert.Empty(r.Stops));
        var reasons = plan.InfeasibleOrders.ToDictionary(e => e.Order.Id, e => e.Reason, StringComparer.Ordinal);
        foreach (var id in new[] { "N06", "N07", "N08", "N09", "N10" })
        {
            Assert.True(reasons.TryGetValue(id, out var reason) && reason == InfeasibilityReason.ExceedsAllCapacities,
                $"{id} must be K1 when B is unusable.");
        }

        foreach (var id in new[] { "N01", "N02", "N03", "N04", "N05" })
        {
            Assert.True(!reasons.TryGetValue(id, out var reason) || reason == InfeasibilityReason.DoesNotFitWithOtherOrders,
                $"{id} fits truck A, so it must be planned or K3.");
        }

        EachOrderExactlyOnce(plan, scenario);
        PassesValidator(plan, scenario, options);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [Trait("Rule", "R23")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Truck_with_a_repeated_id_is_not_used(WorkdayMode mode)
    {
        // R37: a truck whose ID already appeared is not used. The duplicate "B" would be stronger
        // (20000 kg / 5 machines); since it is ignored, the plan is the task optimum.
        var duplicateB = new Truck("B", 20000, 5);
        var scenario = TestScenarios.Task with { Trucks = [.. TestScenarios.Task.Trucks, duplicateB] };
        var options = Options(mode);

        var plan = SolveWithoutException(scenario, options);

        Assert.All(plan.Routes.Where(r => r.Truck == duplicateB), r => Assert.Empty(r.Stops));
        Assert.Empty(plan.InfeasibleOrders);
        EachOrderExactlyOnce(plan, scenario);
        Assert.Equal(ReferenceKm(mode), plan.TotalKm, KmTolerance);
        PassesValidator(plan, scenario, options);
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void Invalid_planning_options_still_throw_ArgumentException()
    {
        // R37: invalid PlanningOptions are a programming error, not user input -> ArgumentException.
        var planner = PlannerLocator.Create();

        Assert.ThrowsAny<ArgumentException>(() => planner.Solve(TestScenarios.Task, PlanningOptions.Default with { SpeedKmh = 0 }));
    }

    private static void AssertInfeasibleAreK4(Plan plan, params string[] orderIds)
    {
        Assert.Equal(orderIds.Order(StringComparer.Ordinal), plan.InfeasibleOrders.Select(e => e.Order.Id).Order(StringComparer.Ordinal));
        Assert.All(plan.InfeasibleOrders, e =>
        {
            Assert.Equal(InfeasibilityReason.InvalidOrderData, e.Reason);
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
        });
    }
}
