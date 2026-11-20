using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Planning;

/// <summary>Search effort of one <see cref="ExactDpPlanner"/> run (for diagnostics and the UI).</summary>
/// <param name="AssignmentsTried">Assignments of orders to trucks that were enumerated (all phases).</param>
/// <param name="AssignmentsEvaluated">Enumerated assignments whose truck routes were actually looked up / computed.</param>
/// <param name="ExtendedSearchUsed">True when no assignment of all plannable orders was feasible and orders could stay unassigned (T23).</param>
/// <param name="DpRuns">Distinct (truck, order set) dynamic programmes that were solved (results are cached).</param>
/// <param name="DpStates">Memoised DP states summed over all DP runs.</param>
public sealed record ExactDpStatistics(
    long AssignmentsTried,
    long AssignmentsEvaluated,
    bool ExtendedSearchUsed,
    int DpRuns,
    long DpStates);

/// <summary>A plan together with the search effort that produced it.</summary>
public sealed record ExactDpResult(Plan Plan, ExactDpStatistics Statistics);
