using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// <see cref="ScenarioLab"/> (page "Testni scenariji"): every case really produces the category / rule its test definition
/// expects, checked here independently of the lab's own checks (own planner and validator calls, own arithmetic); the
/// baseline is the planner's result on the task data; no case changes the task data or the app's scenario store.
/// Validator messages are not asserted (they are being translated), only rule IDs.
/// </summary>
[Trait("Area", "Web")]
public class ScenarioLabTests
{
    private static readonly Lazy<ScenarioLab> SharedLab = new(() => new ScenarioLab(LoadTask()));

    private static ScenarioLab Lab => SharedLab.Value;

    private static Scenario LoadTask() => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath);

    private static LabRun Run(string caseKey, string? defectKey = null)
    {
        var labCase = Lab.FindCase(caseKey);
        Assert.NotNull(labCase);
        var defect = defectKey is null ? null : Lab.FindDefect(defectKey);
        if (defectKey is not null)
        {
            Assert.NotNull(defect);
        }

        return Lab.Run(labCase, defect);
    }

    private static IReadOnlySet<string> TaskOrderIds { get; } = LoadTask().Orders.Select(o => o.Id).ToHashSet();

    private static IReadOnlyList<InfeasibleOrder> AddedOrders(LabRun run) =>
        run.Plan!.InfeasibleOrders.Where(i => !TaskOrderIds.Contains(i.Order.Id)).ToList();

    private static void AssertTaskOrdersPlannedAndValid(LabRun run)
    {
        Assert.NotNull(run.Plan);
        var planned = run.Plan.Routes.SelectMany(r => r.Stops).Select(s => s.OrderId).ToHashSet();
        Assert.True(TaskOrderIds.SetEquals(planned), "every task order is planned, and only those");
        Assert.Empty(PlanValidator.Validate(run.Plan, run.Scenario, run.Options));
    }

    // ---------- every case: expected = actual ----------

    [Theory]
    [InlineData(ScenarioLab.BaselineKey)]
    [InlineData(ScenarioLab.DrivingOnlyKey)]
    [InlineData(ScenarioLab.K1Key)]
    [InlineData(ScenarioLab.K2Key)]
    [InlineData(ScenarioLab.K3Key)]
    [InlineData(ScenarioLab.K4Key)]
    public void Every_planner_case_meets_its_test_definition(string caseKey)
    {
        var run = Run(caseKey);

        Assert.Null(run.Error);
        Assert.NotEmpty(run.Checks);
        Assert.All(run.Checks, c => Assert.True(c.IsMet, $"{caseKey}: {c.Code} – {c.Text}"));
        Assert.True(run.IsExpected);
        Assert.NotEmpty(run.Changes);
    }

    [Fact]
    [Trait("Rule", "R24")]
    public void Baseline_is_the_exact_planner_result_on_the_task_data()
    {
        var run = Run(ScenarioLab.BaselineKey);
        var expected = new ExactDpPlanner().Solve(LoadTask(), PlanningOptions.Default);

        Assert.Equal(PlanningOptions.Default, run.Options);
        AssertSamePlan(expected, run.Plan!);
        AssertTaskOrdersPlannedAndValid(run);
    }

    [Fact]
    [Trait("Rule", "R21")]
    public void DrivingOnly_case_is_the_exact_planner_result_and_not_longer_than_Total()
    {
        var run = Run(ScenarioLab.DrivingOnlyKey);
        var options = PlanningOptions.Default with { WorkdayMode = WorkdayMode.DrivingOnly };
        var expected = new ExactDpPlanner().Solve(LoadTask(), options);

        Assert.Equal(options, run.Options);
        AssertSamePlan(expected, run.Plan!);
        AssertTaskOrdersPlannedAndValid(run);
        Assert.True(run.Plan!.TotalKm <= Run(ScenarioLab.BaselineKey).Plan!.TotalKm + 0.05);
    }

    [Fact]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R13")]
    public void K1_case_adds_a_machine_heavier_than_every_truck_and_the_order_is_K1()
    {
        var run = Run(ScenarioLab.K1Key);

        var added = Assert.Single(AddedOrders(run));
        Assert.Equal(InfeasibilityReason.ExceedsAllCapacities, added.Reason);
        var machine = run.Scenario.Machines.Single(m => m.Id == added.Order.MachineId);
        Assert.All(run.Scenario.Trucks, t => Assert.True(machine.WeightKg > t.CapacityKg));
        AssertTaskOrdersPlannedAndValid(run);
    }

    [Fact]
    [Trait("Rule", "R33")]
    [Trait("Rule", "R20")]
    public void K2_case_adds_an_order_that_alone_exceeds_the_Total_day_but_fits_DrivingOnly()
    {
        var run = Run(ScenarioLab.K2Key);

        var added = Assert.Single(AddedOrders(run));
        Assert.Equal(InfeasibilityReason.ExceedsWorkday, added.Reason);

        // Own arithmetic of the stand-alone route base -> pickup -> delivery -> base (legs rounded first, R14).
        var locations = run.Scenario.Locations.ToDictionary(l => l.Id);
        var baseLocation = locations[run.Scenario.BaseLocationId];
        var pickup = locations[added.Order.PickupLocationId];
        var delivery = locations[added.Order.DeliveryLocationId];
        var km = Distance.Leg(baseLocation, pickup) + Distance.Leg(pickup, delivery) + Distance.Leg(delivery, baseLocation);
        var options = PlanningOptions.Default;
        Assert.True(km + 2 * options.MinutesPerAction > options.MaxMinutes, "Total: driving + 2 actions > 540");
        Assert.True(km <= options.MaxMinutes, "DrivingOnly: driving alone <= 540");

        // The description states this arithmetic with the computed numbers.
        Assert.Contains(run.Changes, c => c.Contains(Format.Km(km), StringComparison.Ordinal));
        Assert.Contains(run.Changes, c => c.Contains(Format.Coordinate(km + 2 * options.MinutesPerAction), StringComparison.Ordinal));

        // In DrivingOnly the same order is not K2 (it fits alone).
        var drivingOnly = new ExactDpPlanner().Solve(run.Scenario, options with { WorkdayMode = WorkdayMode.DrivingOnly });
        Assert.DoesNotContain(drivingOnly.InfeasibleOrders, i => i.Order.Id == added.Order.Id && i.Reason == InfeasibilityReason.ExceedsWorkday);

        AssertTaskOrdersPlannedAndValid(run);
    }

    [Fact]
    [Trait("Rule", "R32")]
    [Trait("Rule", "R33")]
    public void K3_case_with_a_short_day_leaves_out_orders_only_as_K3_and_the_plan_is_valid()
    {
        var run = Run(ScenarioLab.K3Key);

        Assert.Equal(ScenarioLab.ShortWorkdayMinutes, run.Options.MaxMinutes);
        Assert.Equal(LoadTask().Orders.Count, run.Scenario.Orders.Count);
        Assert.NotEmpty(run.Plan!.InfeasibleOrders);
        Assert.All(run.Plan.InfeasibleOrders, i => Assert.Equal(InfeasibilityReason.DoesNotFitWithOtherOrders, i.Reason));
        Assert.Equal(run.Scenario.Orders.Count, run.Plan.Routes.Sum(r => r.OrderCount) + run.Plan.InfeasibleOrders.Count);
        Assert.Empty(PlanValidator.Validate(run.Plan, run.Scenario, run.Options));
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void K4_case_adds_an_order_with_pickup_equal_to_delivery_and_one_with_an_unknown_location()
    {
        var run = Run(ScenarioLab.K4Key);

        var added = AddedOrders(run);
        Assert.Equal(2, added.Count);
        Assert.All(added, i => Assert.Equal(InfeasibilityReason.InvalidOrderData, i.Reason));
        var locationIds = run.Scenario.Locations.Select(l => l.Id).ToHashSet();
        Assert.Contains(added, i => i.Order.PickupLocationId == i.Order.DeliveryLocationId);
        Assert.Contains(added, i => !locationIds.Contains(i.Order.DeliveryLocationId));
        AssertTaskOrdersPlannedAndValid(run);
    }

    // ---------- broken plan: the validator reports the defect ----------

    [Theory]
    [Trait("Rule", "R2")]
    [Trait("Rule", "R5")]
    [Trait("Rule", "R6")]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R12")]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R23")]
    [Trait("Rule", "R32")]
    [InlineData("preopterecenje", "R11")]
    [InlineData("preopterecenje", "R13")]
    [InlineData("strojevi", "R12")]
    [InlineData("redoslijed", "R6")]
    [InlineData("kamioni", "R5")]
    [InlineData("povratak", "R2")]
    [InlineData("km", "R16")]
    [InlineData("izostavljen", "R32")]
    [InlineData("nestao", "R23")]
    [InlineData("radni-dan", "R20")]
    public void Every_defect_of_the_valid_plan_is_reported_with_its_rule(string defectKey, string ruleId)
    {
        var run = Run(ScenarioLab.BrokenPlanKey, defectKey);

        Assert.Null(run.Error);
        Assert.NotNull(run.Plan);
        Assert.Contains(ruleId, run.Defect!.ExpectedRuleIds);

        // Independent check: the plan is validated again here, against the unchanged task data and options.
        Assert.Equal(PlanningOptions.Default, run.Options);
        var violations = PlanValidator.Validate(run.Plan, LoadTask(), run.Options);
        Assert.Contains(violations, v => v.RuleId == ruleId);
        Assert.True(run.IsExpected);
        Assert.NotEmpty(run.Changes);
    }

    [Fact]
    public void The_broken_plan_starts_from_a_valid_baseline_and_every_defect_changes_it()
    {
        var baseline = Run(ScenarioLab.BaselineKey);
        Assert.Empty(baseline.Violations);

        foreach (var defect in Lab.Defects)
        {
            var run = Run(ScenarioLab.BrokenPlanKey, defect.Key);
            Assert.NotEmpty(run.Violations);
            Assert.NotEqual(Describe(baseline.Plan!), Describe(run.Plan!));
        }
    }

    [Fact]
    public void Defects_that_keep_the_routes_report_only_their_own_rule()
    {
        // Defects that change only reported numbers or one swap at the same location: nothing else may break.
        Assert.Equal(["R16"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "km")));
        Assert.Equal(["R12"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "strojevi")));
        Assert.Equal(["R32"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "izostavljen")));
        Assert.Equal(["R23"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "nestao")));
        Assert.Equal(["R20"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "radni-dan")));
    }

    [Fact]
    public void Defects_that_move_stops_report_exactly_their_rule_and_its_spec_consequences()
    {
        // The page's verdict is "expected ⊆ reported"; here the full set is pinned, so a change in the rebuilt route
        // arithmetic (which would add stray violations) cannot hide behind a green verdict.
        // Overload: N06 (3 900 kg) on A (3 500 kg) – load over capacity (R11), machine heavier than the truck (R13).
        Assert.Equal(["R11", "R13"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "preopterecenje")));
        // Delivery before pickup (R6) and its consequences: load replay (R10), pickup/delivery pairing (R4),
        // truck not empty after its last action (R28).
        Assert.Equal(["R10", "R28", "R4", "R6"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "redoslijed")));
        // Delivery on another truck (R5): the order is on two trucks (R7), plus R10, R4 and R28 as above.
        Assert.Equal(["R10", "R28", "R4", "R5", "R7"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "kamioni")));
        // No return to the depot (R2): the route's minutes no longer match its legs (R17).
        Assert.Equal(["R17", "R2"], RuleIds(Run(ScenarioLab.BrokenPlanKey, "povratak")));
    }

    // ---------- isolation and caching ----------

    [Fact]
    public void No_case_changes_the_scenario_store_or_the_task_data()
    {
        var store = new InMemoryScenarioStore(TestScenarios.ScenarioJsonPath);
        var before = store.GetSnapshot();
        var lab = new ScenarioLab(store.Get());

        var runs = lab.RunAll();
        foreach (var labCase in lab.Cases)
        {
            var defects = labCase.Kind == LabCaseKind.BrokenPlan ? lab.Defects.Select(d => (string?)d.Key).ToList() : [null];
            foreach (var defect in defects)
            {
                ScenarioLabViewModelFactory.Create(lab, labCase.Key, defect, store.GetSnapshot());
            }
        }

        var after = store.GetSnapshot();
        Assert.Same(before, after);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(0, after.ChangeCount);
        AssertSameData(LoadTask(), after.Scenario);
        AssertSameData(LoadTask(), lab.TaskScenario!);
        Assert.Equal(lab.Cases.Count - 1 + lab.Defects.Count, runs.Count);
    }

    [Fact]
    public void The_task_data_of_the_lab_cannot_be_changed_through_a_cast()
    {
        var orders = Lab.TaskScenario!.Orders;
        Assert.False(orders is List<TransportOrder>);
        Assert.False(orders is TransportOrder[]);
    }

    [Fact]
    public void A_case_is_computed_once_and_then_served_from_the_cache()
    {
        var first = Run(ScenarioLab.K1Key);
        var second = Run(ScenarioLab.K1Key);
        Assert.Same(first, second);
    }

    // ---------- page view model ----------

    [Fact]
    public void Page_shows_every_case_with_a_computed_verdict()
    {
        var page = ScenarioLabViewModelFactory.Create(Lab, "k2", null, InMemoryScenarioStore.FromScenario(LoadTask()).GetSnapshot());

        Assert.Null(page.Error);
        Assert.Null(page.SelectionWarning);
        Assert.Equal(Lab.Cases.Count, page.Cases.Count);
        Assert.All(page.Cases, c => Assert.Equal("Očekivano ✓", c.Verdict.Text));
        Assert.Single(page.Cases, c => c.IsSelected);
        Assert.True(page.Cases.Single(c => c.IsSelected).Route.TryGetValue(ScenarioLabViewModelFactory.CaseParameter, out var key) && key == "k2");
        Assert.NotNull(page.Selected?.Plan);
        Assert.Contains(page.Selected!.Outcomes, o => o.Text.StartsWith("K2", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_case_or_defect_falls_back_to_the_default_with_a_warning()
    {
        var snapshot = InMemoryScenarioStore.FromScenario(LoadTask()).GetSnapshot();

        var unknownCase = ScenarioLabViewModelFactory.Create(Lab, "nepostojeci", null, snapshot);
        Assert.NotNull(unknownCase.SelectionWarning);
        Assert.True(unknownCase.Cases[0].IsSelected);

        var unknownDefect = ScenarioLabViewModelFactory.Create(Lab, ScenarioLab.BrokenPlanKey, "nepostojeci", snapshot);
        Assert.NotNull(unknownDefect.SelectionWarning);
        Assert.Equal(Lab.Defects[0].Title, unknownDefect.Selected!.DefectTitle);
        Assert.Equal(Lab.Defects.Count, unknownDefect.Selected.Defects.Count);
    }

    // ---------- helpers ----------

    private static IReadOnlyList<string> RuleIds(LabRun run) =>
        run.Violations.Select(v => v.RuleId).Distinct().Order(StringComparer.Ordinal).ToList();

    private static string Describe(Plan plan) =>
        $"{plan.TotalKm:0.0}#" + string.Join("|", plan.Routes.Select(r =>
            $"{r.Truck.Id}:{r.TotalKm:0.0}:{r.DrivingMinutes:0.0}:" + string.Join(",", r.Stops.Select(s => $"{s.Action}-{s.OrderId}-{s.LocationId}"))))
        + "#" + string.Join(",", plan.InfeasibleOrders.Select(i => $"{i.Order.Id}-{i.Reason}"));

    private static void AssertSamePlan(Plan expected, Plan actual)
    {
        Assert.Equal(expected.TotalKm, actual.TotalKm, 6);
        Assert.Equal(Describe(expected), Describe(actual));
    }

    private static void AssertSameData(Scenario expected, Scenario actual)
    {
        Assert.Equal(expected.BaseLocationId, actual.BaseLocationId);
        Assert.Equal(expected.Trucks, actual.Trucks);
        Assert.Equal(expected.Machines, actual.Machines);
        Assert.Equal(expected.Locations, actual.Locations);
        Assert.Equal(expected.Orders, actual.Orders);
    }
}
