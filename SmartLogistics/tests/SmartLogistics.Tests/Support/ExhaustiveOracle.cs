using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Tests.Support;

/// <summary>Best achievable objective (spec R32): most planned orders first, then fewest total km.</summary>
internal sealed record OracleResult(int PlannedOrders, double TotalKm);

/// <summary>
/// Test-only exhaustive search, written from docs/spec.md without looking at the solver, used to check
/// R32/R40 ("omit the smallest number of orders, among equals the plan with the fewest km").
///
/// For every truck a forward dynamic programme over the states (status of every order: not picked /
/// on board / delivered, current location) gives the minimum km of a route L0 -> ... -> L0 that serves
/// exactly a subset S of the orders, respecting weight and machine-count limits after every action
/// (R10–R12), pickup before delivery on the same truck (R5, R6) and only action locations as stops (R31).
/// The route is kept only if it fits the working day (R20/R21, inclusive per R35). Trucks are then
/// combined over disjoint subsets, and the best (count, km) pair is returned.
///
/// Only for valid scenarios with few orders (3^n states per truck; n = 10 gives 59049).
/// </summary>
internal static class ExhaustiveOracle
{
    private const double LimitEpsilon = 1e-6;

    public static OracleResult Solve(Scenario scenario, PlanningOptions options)
    {
        var n = scenario.Orders.Count;
        if (n > 12)
        {
            throw new ArgumentException("The exhaustive oracle is meant for at most 12 orders.", nameof(scenario));
        }

        var locationIndex = scenario.Locations
            .Select((location, i) => (location.Id, i))
            .ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        var locationCount = scenario.Locations.Count;
        var leg = new double[locationCount, locationCount];
        for (var a = 0; a < locationCount; a++)
        {
            for (var b = 0; b < locationCount; b++)
            {
                leg[a, b] = Distance.Leg(scenario.Locations[a], scenario.Locations[b]);
            }
        }

        var input = new OracleInput(
            n,
            locationCount,
            locationIndex[scenario.BaseLocationId],
            leg,
            scenario.Orders.Select(o => scenario.Machines.Single(m => m.Id == o.MachineId).WeightKg).ToArray(),
            scenario.Orders.Select(o => locationIndex[o.PickupLocationId]).ToArray(),
            scenario.Orders.Select(o => locationIndex[o.DeliveryLocationId]).ToArray());

        var subsets = 1 << n;
        var combined = Filled(subsets, double.PositiveInfinity);
        combined[0] = 0;

        foreach (var truck in scenario.Trucks)
        {
            var single = BestKmPerSubset(input, truck);
            for (var mask = 0; mask < subsets; mask++)
            {
                if (!FitsWorkday(single[mask], PopCount(mask), options))
                {
                    single[mask] = double.PositiveInfinity;
                }
            }

            var next = Filled(subsets, double.PositiveInfinity);
            for (var mask = 0; mask < subsets; mask++)
            {
                // Enumerate every subset 'sub' of 'mask' given to this truck; the rest went to earlier trucks.
                for (var sub = mask; ; sub = (sub - 1) & mask)
                {
                    var km = combined[mask ^ sub] + single[sub];
                    if (km < next[mask])
                    {
                        next[mask] = km;
                    }

                    if (sub == 0)
                    {
                        break;
                    }
                }
            }

            combined = next;
        }

        var bestCount = 0;
        var bestKm = 0.0;
        for (var mask = 0; mask < subsets; mask++)
        {
            if (double.IsPositiveInfinity(combined[mask]))
            {
                continue;
            }

            var count = PopCount(mask);
            if (count > bestCount || (count == bestCount && combined[mask] < bestKm))
            {
                bestCount = count;
                bestKm = combined[mask];
            }
        }

        return new OracleResult(bestCount, bestKm);
    }

    private sealed record OracleInput(
        int OrderCount,
        int LocationCount,
        int Base,
        double[,] Leg,
        int[] Weight,
        int[] Pickup,
        int[] Delivery);

    /// <summary>Minimum km of one route of <paramref name="truck"/> that serves exactly each subset (infinity if impossible).</summary>
    private static double[] BestKmPerSubset(OracleInput input, Truck truck)
    {
        var n = input.OrderCount;
        var locations = input.LocationCount;
        var pow3 = new int[n];
        var states = 1;
        for (var i = 0; i < n; i++)
        {
            pow3[i] = states;
            states *= 3;
        }

        // dp[code * locations + loc]: min km from the base to the state; code is base-3 (0 = not picked,
        // 1 = on board, 2 = delivered). Every action adds pow3[i] to the code, so increasing code order is
        // a valid processing order.
        var dp = Filled(states * locations, double.PositiveInfinity);
        dp[input.Base] = 0;
        var best = Filled(1 << n, double.PositiveInfinity);
        best[0] = 0;
        var digits = new int[n];

        for (var code = 0; code < states; code++)
        {
            int load = 0, onBoard = 0, delivered = 0;
            var rest = code;
            for (var i = 0; i < n; i++)
            {
                digits[i] = rest % 3;
                rest /= 3;
                if (digits[i] == 1)
                {
                    load += input.Weight[i];
                    onBoard++;
                }
                else if (digits[i] == 2)
                {
                    delivered |= 1 << i;
                }
            }

            for (var loc = 0; loc < locations; loc++)
            {
                var km = dp[code * locations + loc];
                if (double.IsPositiveInfinity(km))
                {
                    continue;
                }

                if (onBoard == 0 && code != 0)
                {
                    var complete = km + input.Leg[loc, input.Base];
                    if (complete < best[delivered])
                    {
                        best[delivered] = complete;
                    }
                }

                for (var i = 0; i < n; i++)
                {
                    if (digits[i] == 0)
                    {
                        if (load + input.Weight[i] > truck.CapacityKg || onBoard + 1 > truck.MaxMachines)
                        {
                            continue;
                        }

                        Relax(dp, (code + pow3[i]) * locations + input.Pickup[i], km + input.Leg[loc, input.Pickup[i]]);
                    }
                    else if (digits[i] == 1)
                    {
                        Relax(dp, (code + pow3[i]) * locations + input.Delivery[i], km + input.Leg[loc, input.Delivery[i]]);
                    }
                }
            }
        }

        return best;
    }

    private static bool FitsWorkday(double km, int orderCount, PlanningOptions options)
    {
        if (double.IsPositiveInfinity(km))
        {
            return false;
        }

        var minutes = options.DrivingMinutes(km);
        if (options.WorkdayMode == WorkdayMode.Total)
        {
            minutes += 2 * orderCount * options.MinutesPerAction;
        }

        return minutes <= options.MaxMinutes + LimitEpsilon;
    }

    private static void Relax(double[] dp, int index, double value)
    {
        if (value < dp[index])
        {
            dp[index] = value;
        }
    }

    private static double[] Filled(int length, double value)
    {
        var array = new double[length];
        Array.Fill(array, value);
        return array;
    }

    private static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);
}
