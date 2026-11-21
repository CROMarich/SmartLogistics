using SmartLogistics.Core;
using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;
using SmartLogistics.Tests.Support;
using SmartLogistics.Web.ViewModels.Transport;
using static SmartLogistics.Tests.Support.PlanBuilder;
using static SmartLogistics.Tests.WebMapping.WebTestData;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// <see cref="PlanMapper"/> on real plans: the solver plan for data/scenario.json (both modes) and hand-built routes
/// whose numbers follow from the coordinates. Expected strings are checked literally where the value is fixed by
/// spec.md/CLAUDE.md, otherwise by parsing the string back and comparing with the domain value.
/// <para>
/// The per-truck split of the solver plan (A 37.2 / B 162.9) is not asserted (docs/odluke.md T16: spec.md does not show
/// that the optimum is unique); "97 %" for B in Total is asserted on the hand-built reference split instead.
/// </para>
/// </summary>
[Trait("Area", "Web")]
public class PlanMapperTests
{
    private const double KmTolerance = 0.05;

    // ---------- solver plan, both modes ----------

    [Theory]
    [Trait("Rule", "R24")]
    [InlineData(WorkdayMode.Total, "200,1 km")]       // CLAUDE.md: Total optimum 200.1 km
    [InlineData(WorkdayMode.DrivingOnly, "182,2 km")] // CLAUDE.md: DrivingOnly optimum 182.2 km
    public void Plan_total_km_of_the_solver_plan_is_formatted_with_one_decimal(WorkdayMode mode, string expected)
    {
        var model = PlanMapper.ToPlan(SolverPlan(mode), TaskScenario, Options(mode), violations: []);

        Assert.Equal(expected, model.TotalKm);
        Assert.Equal(expected, model.Stats[0].Value);
        Assert.Equal("10 / 10", model.OrdersText);
        Assert.True(model.AllOrdersPlanned);
        Assert.True(model.IsValid);
        Assert.Empty(model.InfeasibleOrders);
    }

    [Theory]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(WorkdayMode.Total, "vožnja + radnje ≤ 09:00")]
    [InlineData(WorkdayMode.DrivingOnly, "samo vožnja ≤ 09:00")]
    public void Workday_mode_text_names_what_counts_and_the_limit(WorkdayMode mode, string expected)
    {
        Assert.Equal(expected, PlanMapper.WorkdayModeText(Options(mode)));
        Assert.Equal(expected, PlanMapper.ToPlan(SolverPlan(mode), TaskScenario, Options(mode)).WorkdayModeText);
    }

    [Theory]
    [Trait("Rule", "R2")]
    [Trait("Rule", "R19")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Every_used_route_starts_with_a_departure_row_at_the_base(WorkdayMode mode)
    {
        var used = SolverPlan(mode).Routes.Where(r => r.Stops.Count > 0).ToList();
        Assert.NotEmpty(used);

        foreach (var route in used)
        {
            var first = PlanMapper.ToStopRows(route, TaskScenario)[0];

            Assert.Equal(StopRowKind.Departure, first.Kind);
            Assert.Equal("L0", first.LocationId);
            Assert.Equal("Polazak", first.ActionText);
            Assert.Equal("–", first.Step);
            Assert.Equal("–", first.OrderId);
            Assert.Equal("–", first.LegKm);
            Assert.Equal("0,0 km", first.CumulativeKm);
            Assert.Equal("00:00", first.Time);
            Assert.Equal("0 kg", first.LoadKg);
            Assert.Equal("0 %", first.CapacityPercent);
            Assert.True(first.IsBaseRow);
        }
    }

    [Theory]
    [Trait("Rule", "R2")]
    [Trait("Rule", "R16")]
    [Trait("Rule", "R19")]
    [Trait("Rule", "R28")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Every_used_route_ends_with_a_return_row_with_the_route_totals(WorkdayMode mode)
    {
        foreach (var route in SolverPlan(mode).Routes.Where(r => r.Stops.Count > 0))
        {
            var rows = PlanMapper.ToStopRows(route, TaskScenario);
            var last = rows[^1];
            var lastStop = route.Stops[^1];
            var lastLocation = TaskScenario.Locations.Single(l => l.Id == lastStop.LocationId);
            var baseLocation = TaskScenario.Locations.Single(l => l.Id == "L0");

            Assert.Equal(route.Stops.Count + 2, rows.Count);
            Assert.Equal(StopRowKind.Return, last.Kind);
            Assert.Equal("L0", last.LocationId);
            Assert.Equal("Povratak", last.ActionText);
            Assert.Equal("–", last.Step);
            Assert.True(last.IsBaseRow);

            // Return leg = rounded distance from the last action back to the base (R14), km including it = route km.
            Assert.Equal(Distance.Leg(lastLocation, baseLocation), ParseKm(last.LegKm), KmTolerance);
            Assert.Equal(route.TotalKm, ParseKm(last.CumulativeKm), KmTolerance);
            Assert.Equal(WholeMinutes(route.TotalMinutes), ParseClock(last.Time));

            // R28: empty after the last action.
            Assert.Equal("0 kg", last.LoadKg);
            Assert.Equal("0 %", last.CapacityPercent);
            Assert.Equal($"0 / {route.Truck.MaxMachines}", last.Machines);
        }
    }

    [Theory]
    [Trait("Rule", "R10")]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R12")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Action_rows_show_load_and_capacity_percent_after_every_action(WorkdayMode mode)
    {
        foreach (var route in SolverPlan(mode).Routes.Where(r => r.Stops.Count > 0))
        {
            var rows = PlanMapper.ToStopRows(route, TaskScenario);

            for (var i = 0; i < route.Stops.Count; i++)
            {
                var stop = route.Stops[i];
                var row = rows[i + 1];
                var order = TaskScenario.Orders.Single(o => o.Id == stop.OrderId);

                Assert.Equal((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), row.Step);
                Assert.Equal(stop.Action == StopAction.Pickup ? StopRowKind.Pickup : StopRowKind.Delivery, row.Kind);
                Assert.Equal(stop.Action == StopAction.Pickup ? "Preuzimanje" : "Isporuka", row.ActionText);
                Assert.Equal(stop.LocationId, row.LocationId);
                Assert.Equal(stop.OrderId, row.OrderId);
                Assert.Equal(order.MachineId, row.MachineId);
                Assert.Equal(stop.LegKm, ParseKm(row.LegKm), KmTolerance);
                Assert.Equal(stop.CumulativeKm, ParseKm(row.CumulativeKm), KmTolerance);
                Assert.Equal(WholeMinutes(stop.CumulativeMinutes), ParseClock(row.Time));
                Assert.Equal(stop.LoadKgAfter, ParseKg(row.LoadKg));
                Assert.Equal($"{stop.MachinesAfter} / {route.Truck.MaxMachines}", row.Machines);

                // % of capacity = load after the action / capacity, whole percent, never above 100 in a valid plan (R11).
                var percent = ParsePercent(row.CapacityPercent);
                Assert.Equal(WholePercent(stop.LoadKgAfter, route.Truck.CapacityKg), percent);
                Assert.InRange(percent, 0, 100);
                Assert.Null(row.RowCssClass);
                Assert.False(row.IsBaseRow);
            }
        }
    }

    [Theory]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [Trait("Rule", "R29")]
    [InlineData(WorkdayMode.Total)]
    [InlineData(WorkdayMode.DrivingOnly)]
    public void Truck_workday_percent_counts_the_time_of_the_mode(WorkdayMode mode)
    {
        var options = Options(mode);

        foreach (var route in SolverPlan(mode).Routes)
        {
            var model = PlanMapper.ToTruckRoute(route, TaskScenario, options);
            var counted = mode == WorkdayMode.Total ? route.TotalMinutes : route.DrivingMinutes;

            Assert.Equal(mode == WorkdayMode.Total ? "vožnja + radnje" : "vožnja", model.WorkdayLabel);
            Assert.Equal(WholeMinutes(counted), ParseClock(model.WorkdayTime));
            Assert.Equal("09:00", model.WorkdayMax);
            Assert.Equal(WholePercent(counted, 540), ParsePercent(model.WorkdayPercent));
            Assert.InRange(ParsePercent(model.WorkdayPercent), 0, 100);
            Assert.False(model.ExceedsWorkday);
            Assert.Equal(route.TotalKm, ParseKm(model.Km), KmTolerance);
            Assert.Equal(WholeMinutes(route.DrivingMinutes), ParseClock(model.DrivingTime));
            Assert.Equal(WholeMinutes(route.ActionMinutes), ParseClock(model.ActionTime));
            Assert.Equal(WholeMinutes(route.TotalMinutes), ParseClock(model.TotalTime));
        }
    }

    // ---------- reference split (hand-built, Total) ----------

    [Fact]
    [Trait("Rule", "R24")]
    public void Reference_split_is_a_valid_plan_of_200_1_km()
    {
        // Precondition for the tests below: the hand-built plan is valid and has the reference value.
        var plan = ReferencePlan();

        ViolationAssert.Empty(PlanValidator.Validate(plan, TaskScenario, Options(WorkdayMode.Total)));
        Assert.Equal(200.1, plan.TotalKm, KmTolerance); // 37.2 + 162.9
        Assert.Equal("200,1 km", PlanMapper.ToPlan(plan, TaskScenario, Options(WorkdayMode.Total)).TotalKm);
    }

    [Fact]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R29")]
    public void Truck_B_of_the_reference_split_uses_97_percent_of_the_working_day_in_Total()
    {
        var model = PlanMapper.ToTruckRoute(Route(ReferencePlan(), "B"), TaskScenario, Options(WorkdayMode.Total));

        // 162.9 km driving + 18 actions × 20 min = 522.9 min → "08:43"; 522.9 / 540 = 96.83 % → "97 %".
        Assert.Equal("Kamion B", model.Title);
        Assert.Equal("162,9 km", model.Km);
        Assert.Equal("02:43", model.DrivingTime); // 162.9 → 163 min
        Assert.Equal("06:00", model.ActionTime);  // 360 min
        Assert.Equal("08:43", model.TotalTime);
        Assert.Equal("08:43", model.WorkdayTime);
        Assert.Equal("97 %", model.WorkdayPercent);
        Assert.Equal("vožnja + radnje 08:43 od 09:00 (97 %)", model.WorkdayText);
        Assert.Equal("9 naloga", model.OrdersText);
        Assert.Equal("10.000 kg · 3 stroja", model.Capacity);
        Assert.False(model.IsUnused);
        Assert.False(model.ExceedsWorkday);
    }

    [Fact]
    [Trait("Rule", "R20")]
    public void Truck_A_of_the_reference_split_uses_14_percent_of_the_working_day_in_Total()
    {
        var model = PlanMapper.ToTruckRoute(Route(ReferencePlan(), "A"), TaskScenario, Options(WorkdayMode.Total));

        // 37.2 km + 2 × 20 min = 77.2 min → "01:17"; 77.2 / 540 = 14.30 % → "14 %".
        Assert.Equal("37,2 km", model.Km);
        Assert.Equal("00:37", model.DrivingTime);
        Assert.Equal("00:40", model.ActionTime);
        Assert.Equal("01:17", model.TotalTime);
        Assert.Equal("14 %", model.WorkdayPercent);
        Assert.Equal("1 nalog", model.OrdersText);
        Assert.Equal("3.500 kg · 2 stroja", model.Capacity);
    }

    [Fact]
    [Trait("Rule", "R21")]
    public void The_same_route_counts_only_driving_in_DrivingOnly()
    {
        // B of the reference split, shown in DrivingOnly: 162.9 / 540 = 30.17 % → "30 %".
        var model = PlanMapper.ToTruckRoute(Route(ReferencePlan(), "B"), TaskScenario, Options(WorkdayMode.DrivingOnly));

        Assert.Equal("vožnja", model.WorkdayLabel);
        Assert.Equal("02:43", model.WorkdayTime);
        Assert.Equal("30 %", model.WorkdayPercent);
        Assert.Equal("08:43", model.TotalTime);
    }

    [Fact]
    [Trait("Rule", "R10")]
    [Trait("Rule", "R11")]
    public void Reference_route_B_shows_the_capacity_column_row_by_row()
    {
        var rows = PlanMapper.ToStopRows(Route(ReferencePlan(), "B"), TaskScenario);

        // 18 actions + departure + return.
        Assert.Equal(20, rows.Count);

        // Step 1: pickup N05 (M05 3100 kg) in the base, leg L0 -> L0 = 0.0 km, done after 20 min. 3100 / 10000 = 31 %.
        Assert.Equal(("1", "L0", "Preuzimanje", "N05", "M05"), (rows[1].Step, rows[1].LocationId, rows[1].ActionText, rows[1].OrderId, rows[1].MachineId));
        Assert.Equal(("0,0 km", "0,0 km", "00:20", "3.100 kg", "1 / 3", "31 %"),
            (rows[1].LegKm, rows[1].CumulativeKm, rows[1].Time, rows[1].LoadKg, rows[1].Machines, rows[1].CapacityPercent));

        // Step 3: pickup N02 at L2: 3100 + 800 + 1300 = 5200 kg, 3 machines; km 8.5 + 7.6 = 16.1; 16.1 + 3 × 20 = 76.1 min.
        Assert.Equal(("7,6 km", "16,1 km", "01:16", "5.200 kg", "3 / 3", "52 %"),
            (rows[3].LegKm, rows[3].CumulativeKm, rows[3].Time, rows[3].LoadKg, rows[3].Machines, rows[3].CapacityPercent));

        // Step 11: pickup N10 (M10 9400 kg) alone: 9400 / 10000 = 94 %.
        Assert.Equal(("N10", "9.400 kg", "1 / 3", "94 %"), (rows[11].OrderId, rows[11].LoadKg, rows[11].Machines, rows[11].CapacityPercent));

        // Step 16: pickup N09 at L6 with N04 on board: 2400 + 7500 = 9900 kg → 99 %; km 125.2 + 18.0 = 143.2;
        // minutes 143.2 + 16 × 20 = 463.2 → "07:43".
        Assert.Equal(("16", "L6", "N09", "M09"), (rows[16].Step, rows[16].LocationId, rows[16].OrderId, rows[16].MachineId));
        Assert.Equal(("18,0 km", "143,2 km", "07:43", "9.900 kg", "2 / 3", "99 %"),
            (rows[16].LegKm, rows[16].CumulativeKm, rows[16].Time, rows[16].LoadKg, rows[16].Machines, rows[16].CapacityPercent));

        // Step 18: delivery N04 in the base: km 154.4 + 8.5 = 162.9, minutes 162.9 + 360 = 522.9 → "08:43".
        Assert.Equal(("Isporuka", "L0", "8,5 km", "162,9 km", "08:43", "0 kg", "0 %"),
            (rows[18].ActionText, rows[18].LocationId, rows[18].LegKm, rows[18].CumulativeKm, rows[18].Time, rows[18].LoadKg, rows[18].CapacityPercent));

        // Return: L0 -> L0 = 0.0 km, not an action (R19): time stays 08:43.
        Assert.Equal((StopRowKind.Return, "0,0 km", "162,9 km", "08:43", "0 %"),
            (rows[19].Kind, rows[19].LegKm, rows[19].CumulativeKm, rows[19].Time, rows[19].CapacityPercent));

        var maxPercent = rows.Max(r => ParsePercent(r.CapacityPercent));
        Assert.Equal(99, maxPercent);
    }

    [Fact]
    [Trait("Rule", "R19")]
    public void Reference_route_A_has_the_return_leg_from_L4()
    {
        var rows = PlanMapper.ToStopRows(Route(ReferencePlan(), "A"), TaskScenario);

        // L0 -> L9 9.4 km (+20 → 29.4 min → "00:29"), L9 -> L4 17.0 km (26.4 km, 66.4 min → "01:06"),
        // return L4 -> L0 10.8 km (37.2 km, 77.2 min → "01:17"). M03 1800 / 3500 = 51.43 % → "51 %".
        Assert.Equal(4, rows.Count);
        Assert.Equal(("9,4 km", "9,4 km", "00:29", "1.800 kg", "51 %"), (rows[1].LegKm, rows[1].CumulativeKm, rows[1].Time, rows[1].LoadKg, rows[1].CapacityPercent));
        Assert.Equal(("17,0 km", "26,4 km", "01:06", "0 kg", "0 %"), (rows[2].LegKm, rows[2].CumulativeKm, rows[2].Time, rows[2].LoadKg, rows[2].CapacityPercent));
        Assert.Equal(("10,8 km", "37,2 km", "01:17", "0 kg", "0 %"), (rows[3].LegKm, rows[3].CumulativeKm, rows[3].Time, rows[3].LoadKg, rows[3].CapacityPercent));
    }

    // ---------- other hand-built routes ----------

    [Fact]
    [Trait("Rule", "R11")]
    [Trait("Rule", "R18")]
    public void Machine_M05_alone_on_A_fills_89_percent_and_ends_on_a_half_minute()
    {
        var scenario = TestScenarios.TaskWithOrders("N05");
        var route = new PlanBuilder(scenario).BuildRoute("A", Pickup("N05"), Delivery("N05"));
        var rows = PlanMapper.ToStopRows(route, scenario);

        // Pickup in the base: leg 0.0 km, 20 min. 3100 / 3500 = 88.57 % → "89 %".
        Assert.Equal(("0,0 km", "00:20", "3.100 kg", "1 / 2", "89 %"), (rows[1].LegKm, rows[1].Time, rows[1].LoadKg, rows[1].Machines, rows[1].CapacityPercent));

        // L0 -> L8 = sqrt(23² + 15²) = sqrt(754) = 27.46 → 27.5 km; 20 + 27.5 + 20 = 67.5 min → "01:08" (midpoint away from zero).
        Assert.Equal(("27,5 km", "27,5 km", "01:08", "0 kg", "0 %"), (rows[2].LegKm, rows[2].CumulativeKm, rows[2].Time, rows[2].LoadKg, rows[2].CapacityPercent));

        // Return 27.5 km → 55.0 km; 55.0 + 40 = 95 min → "01:35"; 95 / 540 = 17.59 % → "18 %".
        Assert.Equal(("27,5 km", "55,0 km", "01:35"), (rows[3].LegKm, rows[3].CumulativeKm, rows[3].Time));
        Assert.Equal("18 %", PlanMapper.ToTruckRoute(route, scenario, PlanningOptions.Default).WorkdayPercent);
    }

    [Fact]
    [Trait("Rule", "R11")]
    public void Full_capacity_of_B_is_shown_as_100_percent()
    {
        // spec D9: M06 3900 + M08 6100 = 10000 kg on B is allowed (inclusive limit).
        var scenario = TestScenarios.TaskWithOrders("N06", "N08");
        var options = PlanningOptions.Default;
        var builder = new PlanBuilder(scenario, options);
        var plan = builder.Route("B", Pickup("N06"), Pickup("N08"), Delivery("N06"), Delivery("N08")).Build();
        ViolationAssert.Empty(PlanValidator.Validate(plan, scenario, options));

        var rows = PlanMapper.ToStopRows(Route(plan, "B"), scenario);

        Assert.Equal(("3.900 kg", "39 %"), (rows[1].LoadKg, rows[1].CapacityPercent));
        Assert.Equal(("10.000 kg", "2 / 3", "100 %"), (rows[2].LoadKg, rows[2].Machines, rows[2].CapacityPercent));
        Assert.Equal(("6.100 kg", "61 %"), (rows[3].LoadKg, rows[3].CapacityPercent));
        Assert.Equal(("0 kg", "0 %"), (rows[4].LoadKg, rows[4].CapacityPercent));
    }

    [Fact]
    [Trait("Rule", "R3")]
    [Trait("Rule", "R36")]
    public void Unused_truck_has_no_rows_and_zero_totals()
    {
        var route = UnusedRoute(TaskScenario.Trucks.Single(t => t.Id == "A"));
        var model = PlanMapper.ToTruckRoute(route, TaskScenario, PlanningOptions.Default);

        Assert.Empty(PlanMapper.ToStopRows(route, TaskScenario));
        Assert.True(model.IsUnused);
        Assert.Empty(model.Stops);
        Assert.Equal("0,0 km", model.Km);
        Assert.Equal("00:00", model.TotalTime);
        Assert.Equal("0 %", model.WorkdayPercent);
        Assert.Equal("0 naloga", model.OrdersText);
    }

    [Fact]
    [Trait("Rule", "R20")]
    public void Route_over_the_working_day_is_marked_and_the_percent_is_not_capped()
    {
        // The reference route B with a limit of 500 min: 522.9 / 500 = 104.58 % → "105 %".
        var options = PlanningOptions.Default with { MaxMinutes = 500 };
        var model = PlanMapper.ToTruckRoute(Route(ReferencePlan(), "B"), TaskScenario, options);

        Assert.True(model.ExceedsWorkday);
        Assert.Equal("08:20", model.WorkdayMax);
        Assert.Equal("105 %", model.WorkdayPercent);
    }
}
