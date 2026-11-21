using SmartLogistics.Core.Domain;
using SmartLogistics.Core.Validation;

namespace SmartLogistics.Core.Planning;

/// <summary>One action of a route as chosen by a planner: which plannable order and pickup or delivery.</summary>
internal readonly record struct RouteAction(PlannableOrder Order, StopAction Action);

/// <summary>
/// Building blocks shared by the planners: turning an ordered list of actions into a <see cref="TruckRoute"/> with
/// every <see cref="Stop"/> field filled in (load, km and minutes, legs in whole tenths of a km, T24), and the final
/// <see cref="PlanValidator"/> check (a violation is a planner bug).
/// </summary>
internal static class PlanAssembly
{
    /// <summary>
    /// Builds the route of <paramref name="truck"/> visiting <paramref name="actions"/> in order, starting and ending
    /// at the base. Returns the route and its km in tenths (including the return leg). No actions = unused truck
    /// (R3, R36). The actions are not checked here; the validator does that for the whole plan.
    /// </summary>
    public static (TruckRoute Route, int Km10) BuildRoute(
        PreparedScenario input, Truck truck, IReadOnlyList<RouteAction> actions, int orderCount)
    {
        var options = input.Options;
        if (actions.Count == 0)
        {
            return (new TruckRoute(truck, [], 0, 0, 0, 0), 0); // unused truck (R3, R36)
        }

        var stops = new List<Stop>(actions.Count);
        var location = input.BaseIndex;
        var cumulativeKm10 = 0;
        var loadKg = 0;
        var machines = 0;
        foreach (var (order, action) in actions)
        {
            var pickup = action == StopAction.Pickup;
            var next = pickup ? order.PickupIndex : order.DeliveryIndex;
            var leg10 = input.Legs10[location, next];
            cumulativeKm10 += leg10;
            loadKg += pickup ? order.Machine.WeightKg : -order.Machine.WeightKg;
            machines += pickup ? 1 : -1;
            var cumulativeKm = cumulativeKm10 / 10.0;
            var actionsDone = stops.Count + 1;
            stops.Add(new Stop(
                input.Locations[next].Id,
                action,
                order.Order.Id,
                loadKg,
                machines,
                leg10 / 10.0,
                cumulativeKm,
                options.DrivingMinutes(cumulativeKm) + actionsDone * options.MinutesPerAction));
            location = next;
        }

        var routeKm10 = cumulativeKm10 + input.Legs10[location, input.BaseIndex];
        var routeKm = routeKm10 / 10.0;
        var route = new TruckRoute(
            truck,
            stops,
            routeKm,
            options.DrivingMinutes(routeKm),
            stops.Count * options.MinutesPerAction,
            orderCount);
        return (route, routeKm10);
    }

    /// <summary>
    /// Runs <see cref="PlanValidator.Validate"/> on the finished plan; any violation is a bug in the planner named
    /// <paramref name="plannerName"/> and throws <see cref="InvalidOperationException"/> listing every violation.
    /// </summary>
    public static void EnsureValid(Plan plan, Scenario scenario, PlanningOptions options, string plannerName)
    {
        var violations = PlanValidator.Validate(plan, scenario, options);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bug in {plannerName}: the plan breaks {violations.Count} rule(s):" + Environment.NewLine
                + string.Join(Environment.NewLine, violations.Select(v =>
                    $"- {v.RuleId}{(v.TruckId is null ? "" : $" truck {v.TruckId}")}{(v.StepIndex is null ? "" : $" step {v.StepIndex + 1}")}: {v.Message}")));
        }
    }
}
