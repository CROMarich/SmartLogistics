using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core;

/// <summary>Leg length between two locations (spec R14, R15).</summary>
public static class Distance
{
    /// <summary>
    /// Euclidean distance rounded to one decimal, midpoint away from zero.
    /// Every leg is rounded on its own; route lengths are sums of rounded legs.
    /// </summary>
    public static double Leg(Location from, Location to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return Math.Round(Math.Sqrt(dx * dx + dy * dy), 1, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Precomputed rounded legs between all pairs of locations, looked up by location ID.</summary>
public sealed class DistanceMatrix
{
    private readonly Dictionary<string, int> _indexById;
    private readonly double[,] _legs;

    public DistanceMatrix(IReadOnlyList<Location> locations)
    {
        Locations = locations;
        _indexById = locations
            .Select((location, index) => (location.Id, index))
            .ToDictionary(x => x.Id, x => x.index);

        _legs = new double[locations.Count, locations.Count];
        for (var i = 0; i < locations.Count; i++)
        {
            for (var j = 0; j < locations.Count; j++)
            {
                _legs[i, j] = Distance.Leg(locations[i], locations[j]);
            }
        }
    }

    /// <summary>Locations in the order of the matrix rows and columns.</summary>
    public IReadOnlyList<Location> Locations { get; }

    /// <summary>Rounded leg between two locations; throws <see cref="KeyNotFoundException"/> for an unknown ID.</summary>
    public double this[string fromId, string toId] => _legs[_indexById[fromId], _indexById[toId]];
}
