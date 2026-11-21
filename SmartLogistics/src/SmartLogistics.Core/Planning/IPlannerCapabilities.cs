namespace SmartLogistics.Core.Planning;

/// <summary>
/// Optional description of a planner for the UI (spec R38: a heuristic result must say that the optimum is not
/// guaranteed). <see cref="Domain.Plan"/> has no field for it, so the UI asks the planner:
/// <c>planner is IPlannerCapabilities { GuaranteesOptimum: false }</c> means "show the notice".
/// A planner that does not implement the interface should be treated as not guaranteeing the optimum.
/// </summary>
public interface IPlannerCapabilities
{
    /// <summary>Short name for the UI, e.g. "ExactDpPlanner (egzaktno)".</summary>
    string DisplayName { get; }

    /// <summary>True when every plan is a proven optimum per spec R32 (most planned orders, then fewest km).</summary>
    bool GuaranteesOptimum { get; }
}
