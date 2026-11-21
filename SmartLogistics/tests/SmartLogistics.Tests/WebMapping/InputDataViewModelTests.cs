using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Planning;
using SmartLogistics.Tests.Support;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// <see cref="InputDataViewModelFactory"/> (page "Ulazni podaci"): the four input tables of data/scenario.json (spec R27) and
/// K4 warnings taken from <see cref="Feasibility.Check"/>, the planner's own input check (docs/odluke.md T63 a).
/// </summary>
[Trait("Area", "Web")]
public class InputDataViewModelTests
{
    private static Scenario LoadTask() => ScenarioLoader.LoadFromFile(TestScenarios.ScenarioJsonPath);

    private static InputDataPageViewModel Page(Scenario scenario) =>
        InputDataViewModelFactory.Create(InMemoryScenarioStore.FromScenario(scenario).GetSnapshot());

    [Fact]
    [Trait("Rule", "R27")]
    public void Task_data_is_shown_in_four_tables_with_one_row_per_entry_in_file_order()
    {
        var task = LoadTask();
        var page = InputDataViewModelFactory.Create(new InMemoryScenarioStore(TestScenarios.ScenarioJsonPath).GetSnapshot());

        Assert.Null(page.Error);
        Assert.Equal(task.Trucks.Select(t => t.Id), page.Trucks.Select(r => r.Badge.Text));
        Assert.Equal(task.Machines.Select(m => m.Id), page.Machines.Select(r => r.Id));
        Assert.Equal(task.Locations.Select(l => l.Id), page.Locations.Select(r => r.Id));
        Assert.Equal(task.Orders.Select(o => o.Id), page.Orders.Rows.Select(r => r.Id));

        // docs/zadatak.md: 2 trucks, 10 machines, 10 locations (L0–L9), 10 orders.
        Assert.Equal(2, page.Trucks.Count);
        Assert.Equal(10, page.Machines.Count);
        Assert.Equal(10, page.Locations.Count);
        Assert.Equal(10, page.Orders.Rows.Count);
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void Task_data_has_no_order_warning_because_the_planner_rejects_none()
    {
        var task = LoadTask();
        Assert.Empty(Feasibility.Check(task, PlanningOptions.Default).Rejected);
        Assert.All(Page(task).Orders.Rows, r => Assert.Null(r.DataWarning));
    }

    public static TheoryData<string> InvalidScenarios => ["duplicate machine", "duplicate location", "duplicate order", "unknown and reused"];

    [Theory]
    [Trait("Rule", "R37")]
    [MemberData(nameof(InvalidScenarios))]
    public void Order_warnings_are_exactly_the_K4_orders_of_Feasibility_Check(string variant)
    {
        var scenario = Invalid(variant);
        var expected = Feasibility.Check(scenario, PlanningOptions.Default).Rejected
            .Where(r => r.Infeasible.Reason == InfeasibilityReason.InvalidOrderData)
            .ToDictionary(r => r.ScenarioIndex, r => r.Infeasible.Message);
        Assert.NotEmpty(expected);

        var rows = Page(scenario).Orders.Rows;

        Assert.Equal(scenario.Orders.Count, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            Assert.Equal(expected.GetValueOrDefault(i), rows[i].DataWarning);
            if (expected.ContainsKey(i))
            {
                Assert.Empty(rows[i].Trucks);
            }
        }
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void A_duplicated_machine_ID_makes_its_order_K4_on_the_page_as_in_the_planner()
    {
        var scenario = Invalid("duplicate machine");

        var row = Page(scenario).Orders.Rows.Single(r => r.Machine == "M03");
        Assert.NotNull(row.DataWarning);

        var plan = new ExactDpPlanner().Solve(scenario, PlanningOptions.Default);
        Assert.Contains(plan.InfeasibleOrders, i => i.Order.MachineId == "M03" && i.Reason == InfeasibilityReason.InvalidOrderData);
    }

    [Fact]
    [Trait("Rule", "R37")]
    public void An_unusable_truck_is_not_offered_for_orders()
    {
        var task = LoadTask();
        var scenario = task with { Trucks = [.. task.Trucks, new Truck("C", 0, 3)] };

        var page = Page(scenario);
        Assert.All(page.Orders.Rows, r => Assert.DoesNotContain(r.Trucks, b => b.Text == "C"));
        Assert.All(page.Machines, m => Assert.DoesNotContain(m.FitsOn, b => b.Text == "C"));
    }

    private static Scenario Invalid(string variant)
    {
        var task = LoadTask();
        return variant switch
        {
            // M03 defined twice: the old page logic took the first one and showed N03 as valid.
            "duplicate machine" => task with { Machines = [.. task.Machines, new Machine("M03", 500)] },
            "duplicate location" => task with { Locations = [.. task.Locations, new Location("L5", 1, 1)] },
            "duplicate order" => task with
            {
                Machines = [.. task.Machines, new Machine("M11", 500)],
                Orders = [.. task.Orders, new TransportOrder("N01", "M11", "L2", "L4")],
            },
            "unknown and reused" => task with
            {
                Orders =
                [
                    .. task.Orders,
                    new TransportOrder("N11", "M99", "L1", "L2"),
                    new TransportOrder("N12", "M01", "L2", "L4"),
                    new TransportOrder("N13", "M02", "L2", "L2"),
                ],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };
    }
}
