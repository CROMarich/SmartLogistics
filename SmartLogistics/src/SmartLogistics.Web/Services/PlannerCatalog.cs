using SmartLogistics.Core.Planning;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Services;

/// <summary>Metadata of one planning algorithm the UI can offer.</summary>
/// <param name="Key">Keyed-service key and query value of the plan form ("exact", "ortools").</param>
/// <param name="Name">Croatian name for the UI, e.g. "Egzaktni (DP)".</param>
/// <param name="Description">One Croatian sentence for the form hint and the comparison table.</param>
/// <param name="IsExact">True when the planner proves the optimum (spec R38); false for a heuristic.</param>
public sealed record PlannerDescriptor(string Key, string Name, string Description, bool IsExact);

/// <summary>
/// The algorithms known to the UI, in display order. A descriptor only describes an algorithm; whether it can run is
/// decided by the DI container: the planner must be registered as a keyed <see cref="IRoutePlanner"/> under
/// <see cref="PlannerDescriptor.Key"/> (see <c>Program.cs</c>). An algorithm without a registration is shown as
/// "not available" instead of throwing.
/// </summary>
public static class PlannerCatalog
{
    public const string ExactKey = "exact";
    public const string OrToolsKey = "ortools";

    /// <summary>The default algorithm of the plan form (R38: exact optimum for the given data size).</summary>
    public const string DefaultKey = ExactKey;

    public static readonly PlannerDescriptor Exact = new(
        ExactKey,
        "Egzaktni (DP)",
        "Sve raspodjele naloga i dinamičko programiranje po kamionu – dokazani najmanji broj km.",
        IsExact: true);

    public static readonly PlannerDescriptor OrTools = new(
        OrToolsKey,
        "OR-Tools (heuristika)",
        // Search limits come from the planner itself (decision T55), so the text cannot drift from the code.
        "Google OR-Tools, usmjeravanje vozila s preuzimanjem i isporukom; vođena lokalna pretraga (GLS) u "
            + $"{OrToolsPlanner.GuidedLocalSearchLambdas.Count} deterministička pokretanja (lambda "
            + $"{string.Join(" i ", OrToolsPlanner.GuidedLocalSearchLambdas.Select(Format.Number))}), svako do "
            + $"{Format.Integer(OrToolsPlanner.SolutionLimit)} rješenja, zajedno najviše {OrToolsPlanner.TimeLimitSeconds} s.",
        IsExact: false);

    public static IReadOnlyList<PlannerDescriptor> All { get; } = [Exact, OrTools];

    /// <summary>Case-insensitive lookup by key; null for an unknown key.</summary>
    public static PlannerDescriptor? Find(string? key) =>
        key is null ? null : All.FirstOrDefault(d => string.Equals(d.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
}
