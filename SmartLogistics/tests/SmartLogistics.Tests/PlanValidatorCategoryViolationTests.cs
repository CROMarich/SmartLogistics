using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using static SmartLogistics.Tests.Support.PlanBuilder;

namespace SmartLogistics.Tests;

/// <summary>
/// Hand-built invalid plans for the amended R37 (invalid data must not be planned, unusable trucks must not
/// be used) and R33 (every infeasible order has the category its data give: K1, K2, K3 or K4).
/// Like <see cref="PlanValidatorViolationTests"/>, only the rule (and truck/step where it applies) is asserted,
/// never the validator message text.
/// </summary>
public class PlanValidatorCategoryViolationTests
{
    private static readonly PlanningOptions Options = PlanningOptions.Default;

    private static IReadOnlyList<PlanViolation> Validate(Plan plan, Scenario scenario, PlanningOptions? options = null) =>
        PlanValidator.Validate(plan, scenario, options ?? Options);

    /// <summary>Task data (trucks A, B and locations L0..L9) with only the given extra machines, locations and orders.</summary>
    private static Scenario TaskWithOnly(IEnumerable<Machine> machines, IEnumerable<Location> locations, params TransportOrder[] orders) =>
        TestScenarios.Task with
        {
            Machines = [.. TestScenarios.Task.Machines, .. machines],
            Locations = [.. TestScenarios.Task.Locations, .. locations],
            Orders = orders,
        };

    // ---------- R37: an order with invalid data (K4) must not be planned ----------

    [Fact]
    [Trait("Rule", "R37")]
    public void Order_with_pickup_equal_to_delivery_planned_on_A_is_R37()
    {
        // N11: M11 (1000 kg) from L1 to L1. R37: pickup == delivery is invalid -> must be K4, not planned.
        var n11 = new TransportOrder("N11", "M11", "L1", "L1");
        var scenario = TaskWithOnly([new Machine("M11", 1000)], [], n11);
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N11"), Delivery("N11"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "A", stepIndex: 0);
    }

    [Theory]
    [Trait("Rule", "R37")]
    [InlineData(0)]
    [InlineData(-500)]
    public void Order_whose_machine_weight_is_not_positive_planned_on_A_is_R37(int weightKg)
    {
        // R37: machine mass <= 0 is invalid -> the order must be K4, not planned.
        var n11 = new TransportOrder("N11", "M11", "L1", "L2");
        var scenario = TaskWithOnly([new Machine("M11", weightKg)], [], n11);
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N11"), Delivery("N11"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "A", stepIndex: 0);
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void Order_with_unknown_delivery_location_planned_on_A_is_R37()
    {
        // N11: M11 (1000 kg) from L1 to L99, which does not exist. R37: the order must be K4.
        // L99 has no coordinates, so the hand-built plan delivers at L2 instead (R8 may be reported too).
        var n11 = new TransportOrder("N11", "M11", "L1", "L99");
        var scenario = TaskWithOnly([new Machine("M11", 1000)], [], n11);
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N11"), Delivery("N11").At("L2"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "A");
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void Order_whose_machine_is_already_used_by_an_earlier_order_planned_on_A_is_R37()
    {
        // N11 uses M01, which N01 (earlier in the input) already uses. R37: the later order is K4.
        // A carries N01 (steps 0, 1) and then N11 (steps 2, 3); one 800 kg machine at a time fits A.
        var n01 = TestScenarios.Task.Orders.Single(o => o.Id == "N01");
        var n11 = new TransportOrder("N11", "M01", "L2", "L5");
        var scenario = TaskWithOnly([], [], n01, n11);
        var plan = new PlanBuilder(scenario)
            .Route("A", Pickup("N01"), Delivery("N01"), Pickup("N11"), Delivery("N11"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "A", stepIndex: 2);
    }

    // ---------- R37: an unusable truck must not have actions ----------

    [Theory]
    [Trait("Rule", "R37")]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(3500, 0)]
    [InlineData(3500, -1)]
    public void Truck_with_a_non_positive_limit_that_has_actions_is_R37(int capacityKg, int maxMachines)
    {
        // R37: a truck with capacity or max machines <= 0 is not used (no actions). C carries N01 anyway.
        var scenario = TestScenarios.TaskWithOrders("N01") with
        {
            Trucks = [.. TestScenarios.Task.Trucks, new Truck("C", capacityKg, maxMachines)],
        };
        var plan = new PlanBuilder(scenario)
            .Route("C", Pickup("N01"), Delivery("N01"))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "C");
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void Second_truck_with_an_already_used_id_that_has_actions_is_R37()
    {
        // R37: a truck whose ID already appeared is not used. The scenario has A, B and a second "B"
        // (20000 kg / 5); the first B is unused, the second B carries N01.
        var secondB = new Truck("B", 20000, 5);
        var scenario = TestScenarios.TaskWithOrders("N01") with
        {
            Trucks = [.. TestScenarios.Task.Trucks, secondB],
        };
        var builder = new PlanBuilder(scenario).WithoutAutomaticUnusedTrucks();
        var plan = builder
            .Route(UnusedRoute(TestScenarios.Task.Trucks[0]))
            .Route(UnusedRoute(TestScenarios.Task.Trucks[1]))
            .Route(builder.BuildRoute(secondB, [Pickup("N01"), Delivery("N01")]))
            .Build();

        ViolationAssert.Contains(Validate(plan, scenario), "R37", truckId: "B");
    }

    // ---------- R33: the category of an infeasible order must match its data ----------

    /// <summary>
    /// Single-order scenarios on the task trucks (A 3500 kg, B 10000 kg) and locations:
    /// K1 – M11 11000 kg > 10000 kg (B) > 3500 kg (A), L1 -> L2;
    /// K2 – M11 1000 kg, L0 -> LF (300, 0): 0.0 + 300.0 + 300.0 = 600 km; Total 600 + 2 x 20 = 640 min > 540;
    /// K3 – N06 (task order, alone 60.2 km + 40 min = 100.2 min on B, spec 3.7): feasible alone;
    /// K4 – M11 1000 kg, L1 -> L1 (pickup == delivery, R37).
    /// </summary>
    private static Scenario CategoryScenario(string dataCategory) => dataCategory switch
    {
        "K1" => TaskWithOnly([new Machine("M11", 11000)], [], new TransportOrder("N11", "M11", "L1", "L2")),
        "K2" => TaskWithOnly([new Machine("M11", 1000)], [new Location("LF", 300, 0)], new TransportOrder("N11", "M11", "L0", "LF")),
        "K3" => TestScenarios.TaskWithOrders("N06"),
        "K4" => TaskWithOnly([new Machine("M11", 1000)], [], new TransportOrder("N11", "M11", "L1", "L1")),
        _ => throw new ArgumentOutOfRangeException(nameof(dataCategory), dataCategory, "Unknown category."),
    };

    private static Plan OnlyInfeasible(Scenario scenario, InfeasibilityReason reason) =>
        new PlanBuilder(scenario)
            .Infeasible(scenario.Orders.Single().Id, "Test: nalog nije u planu (kategorija zadana testom).", reason)
            .Build();

    [Theory]
    [Trait("Rule", "R33")]
    [InlineData("K1", InfeasibilityReason.DoesNotFitWithOtherOrders)]
    [InlineData("K1", InfeasibilityReason.ExceedsWorkday)]
    [InlineData("K1", InfeasibilityReason.InvalidOrderData)]
    [InlineData("K2", InfeasibilityReason.DoesNotFitWithOtherOrders)]
    [InlineData("K2", InfeasibilityReason.ExceedsAllCapacities)]
    [InlineData("K2", InfeasibilityReason.InvalidOrderData)]
    [InlineData("K3", InfeasibilityReason.ExceedsWorkday)]
    [InlineData("K3", InfeasibilityReason.ExceedsAllCapacities)]
    [InlineData("K4", InfeasibilityReason.DoesNotFitWithOtherOrders)]
    [InlineData("K4", InfeasibilityReason.ExceedsAllCapacities)]
    [InlineData("K4", InfeasibilityReason.ExceedsWorkday)]
    public void Infeasible_order_with_the_wrong_category_is_R33(string dataCategory, InfeasibilityReason reported)
    {
        var scenario = CategoryScenario(dataCategory);
        var plan = OnlyInfeasible(scenario, reported);

        ViolationAssert.Contains(Validate(plan, scenario), "R33");
    }

    [Theory]
    [Trait("Rule", "R33")]
    [InlineData("K1", InfeasibilityReason.ExceedsAllCapacities)]
    [InlineData("K2", InfeasibilityReason.ExceedsWorkday)]
    [InlineData("K4", InfeasibilityReason.InvalidOrderData)]
    public void Infeasible_order_with_the_category_its_data_give_is_valid(string dataCategory, InfeasibilityReason reported)
    {
        // Control for the test above: the same plans with the right category have no violation at all.
        var scenario = CategoryScenario(dataCategory);
        var plan = OnlyInfeasible(scenario, reported);

        ViolationAssert.Empty(Validate(plan, scenario));
    }

    [Fact]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R21")]
    public void Order_K2_only_in_Total_listed_as_K2_in_DrivingOnly_is_R33()
    {
        // The category depends on the workday mode (RS17). M11 1000 kg, L0 -> LG (255, 0):
        // 0.0 + 255.0 + 255.0 = 510 km. Total: 510 + 40 = 550 min > 540 -> K2; DrivingOnly: 510 min <= 540 -> not K2.
        var scenario = TaskWithOnly([new Machine("M11", 1000)], [new Location("LG", 255, 0)], new TransportOrder("N11", "M11", "L0", "LG"));
        var total = PlanningOptions.Default with { WorkdayMode = WorkdayMode.Total };
        var drivingOnly = PlanningOptions.Default with { WorkdayMode = WorkdayMode.DrivingOnly };
        var plan = OnlyInfeasible(scenario, InfeasibilityReason.ExceedsWorkday);

        ViolationAssert.Empty(Validate(plan, scenario, total));
        ViolationAssert.Contains(Validate(plan, scenario, drivingOnly), "R33");
    }
}
