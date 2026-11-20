using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Planning;

/// <summary>Assigns orders to trucks and orders the stops of each route (spec R1–R39).</summary>
public interface IRoutePlanner
{
    Plan Solve(Scenario scenario, PlanningOptions options);
}
