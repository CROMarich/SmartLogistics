using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core.Planning;

/// <summary>One action of a reconstructed route: position of the order in the DP's order list and the action.</summary>
internal readonly record struct RouteStep(int OrderPosition, StopAction Action);

/// <summary>Shortest route of one truck for a fixed set of orders.</summary>
/// <param name="Km10">Length in whole tenths of a km, including the return leg to the base.</param>
/// <param name="Steps">Actions in route order (empty for an empty order set).</param>
/// <param name="States">Number of memoised DP states.</param>
internal sealed record RouteDpResult(int Km10, IReadOnlyList<RouteStep> Steps, int States);

/// <summary>
/// Exact dynamic programme over (current location, status of every order) for one truck and one order set.
/// The status of order i is a base-3 digit: 0 not picked up, 1 on the truck, 2 delivered. The value of a state
/// is the minimum remaining km (tenths) to finish every order and return to the base. The load (kg and number
/// of machines) is derived from the status digits, never stored separately. A pickup is allowed only if the load
/// after it stays within the truck's capacity and machine limit (spec R11, R12); a delivery is always allowed for
/// an order on the truck. Transitions are tried in the order of the given list (scenario order) and a strictly
/// smaller value wins, so ties are broken deterministically (spec R39).
/// States: at most 3^m x (2m + 1) for m orders; each state does O(m) work.
/// </summary>
internal sealed class RouteDp
{
    /// <summary>3^39 is the largest power of three below <see cref="long.MaxValue"/>.</summary>
    internal const int MaxOrders = 39;

    private const int Unreachable = int.MaxValue;

    private readonly int _count;
    private readonly int[] _pickup;
    private readonly int[] _delivery;
    private readonly int[] _weight;
    private readonly long[] _pow3;
    private readonly int[,] _legs;
    private readonly long _capacityKg;
    private readonly int _maxMachines;
    private readonly long _allDelivered;
    private readonly Dictionary<(long Status, int Location), int> _memo = new();

    private RouteDp(Truck truck, IReadOnlyList<PlannableOrder> orders, int[,] legs10, int baseIndex)
    {
        _count = orders.Count;
        _capacityKg = truck.CapacityKg;
        _maxMachines = truck.MaxMachines;
        _pickup = new int[_count];
        _delivery = new int[_count];
        _weight = new int[_count];
        _pow3 = new long[_count];

        // Local location indices: 0 is the base, then the order locations in first-use order.
        var local = new Dictionary<int, int> { [baseIndex] = 0 };
        var global = new List<int> { baseIndex };
        int Local(int index)
        {
            if (!local.TryGetValue(index, out var l))
            {
                l = global.Count;
                local[index] = l;
                global.Add(index);
            }

            return l;
        }

        long power = 1;
        for (var i = 0; i < _count; i++)
        {
            _pickup[i] = Local(orders[i].PickupIndex);
            _delivery[i] = Local(orders[i].DeliveryIndex);
            _weight[i] = orders[i].Machine.WeightKg;
            _pow3[i] = power;
            _allDelivered += 2 * power;
            power *= 3;
        }

        _legs = new int[global.Count, global.Count];
        for (var a = 0; a < global.Count; a++)
        {
            for (var b = 0; b < global.Count; b++)
            {
                _legs[a, b] = legs10[global[a], global[b]];
            }
        }
    }

    /// <summary>Shortest route of <paramref name="truck"/> serving all <paramref name="orders"/> (each must fit the truck alone).</summary>
    public static RouteDpResult Run(Truck truck, IReadOnlyList<PlannableOrder> orders, int[,] legs10, int baseIndex)
    {
        if (orders.Count == 0)
        {
            return new RouteDpResult(0, [], 0);
        }

        if (orders.Count > MaxOrders)
        {
            throw new NotSupportedException(
                $"The exact planner supports at most {MaxOrders} orders on one truck (base-3 status in a 64-bit integer); got {orders.Count}.");
        }

        var dp = new RouteDp(truck, orders, legs10, baseIndex);
        var km10 = dp.Best(0, 0);
        if (km10 == Unreachable)
        {
            // Cannot happen: every order fits the truck alone, so "deliver everything, then serve one by one" always works.
            throw new InvalidOperationException($"No route found for truck {truck.Id}; every order was expected to fit it alone.");
        }

        return new RouteDpResult(km10, dp.Reconstruct(), dp._memo.Count);
    }

    private int Best(int location, long status)
    {
        if (status == _allDelivered)
        {
            return _legs[location, 0];
        }

        if (_memo.TryGetValue((status, location), out var cached))
        {
            return cached;
        }

        Span<byte> digits = stackalloc byte[_count];
        var (loadKg, machines) = Decode(status, digits);
        var best = Unreachable;
        for (var i = 0; i < _count; i++)
        {
            var candidate = Candidate(i, digits[i], location, status, loadKg, machines);
            if (candidate < best)
            {
                best = candidate;
            }
        }

        _memo[(status, location)] = best;
        return best;
    }

    /// <summary>Value of the transition "act on order i" from the given state; <see cref="Unreachable"/> if not allowed.</summary>
    private int Candidate(int i, byte digit, int location, long status, long loadKg, int machines)
    {
        int next;
        if (digit == 0)
        {
            if (loadKg + _weight[i] > _capacityKg || machines + 1 > _maxMachines)
            {
                return Unreachable;
            }

            next = _pickup[i];
        }
        else if (digit == 1)
        {
            next = _delivery[i];
        }
        else
        {
            return Unreachable;
        }

        var rest = Best(next, status + _pow3[i]);
        return rest == Unreachable ? Unreachable : checked(_legs[location, next] + rest);
    }

    /// <summary>Splits the status into digits and returns the load on the truck (orders with status 1).</summary>
    private (long LoadKg, int Machines) Decode(long status, Span<byte> digits)
    {
        long loadKg = 0;
        var machines = 0;
        for (var i = 0; i < _count; i++)
        {
            digits[i] = (byte)(status % 3);
            status /= 3;
            if (digits[i] == 1)
            {
                loadKg += _weight[i];
                machines++;
            }
        }

        return (loadKg, machines);
    }

    /// <summary>Follows the memoised values from the start state; picks the first transition that attains the optimum.</summary>
    private IReadOnlyList<RouteStep> Reconstruct()
    {
        var steps = new List<RouteStep>(2 * _count);
        var location = 0;
        long status = 0;
        Span<byte> digits = stackalloc byte[_count];
        while (status != _allDelivered)
        {
            var best = Best(location, status);
            var (loadKg, machines) = Decode(status, digits);
            var chosen = -1;
            for (var i = 0; i < _count && chosen < 0; i++)
            {
                if (Candidate(i, digits[i], location, status, loadKg, machines) == best)
                {
                    chosen = i;
                }
            }

            var pickup = digits[chosen] == 0;
            steps.Add(new RouteStep(chosen, pickup ? StopAction.Pickup : StopAction.Delivery));
            location = pickup ? _pickup[chosen] : _delivery[chosen];
            status += _pow3[chosen];
        }

        return steps;
    }
}
