using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Tests;

public class DistanceTests
{
    private const double Tolerance = 1e-9;

    // Coordinates from docs/zadatak.md, typed in independently of data/scenario.json.
    private static readonly IReadOnlyList<Location> Locations =
    [
        new("L0", 0, 0),
        new("L1", 3, 8),
        new("L2", 10, 11),
        new("L3", 17, 9),
        new("L4", 9, -6),
        new("L5", 26, 3),
        new("L6", -7, 13),
        new("L7", 20, -8),
        new("L8", 23, 15),
        new("L9", -8, -5),
    ];

    private static Location At(string id) => Locations.Single(l => l.Id == id);

    public static TheoryData<string> AllLocationIds() => new(Locations.Select(l => l.Id));

    [Theory]
    [Trait("Rule", "R14")]
    [InlineData("L0", "L1", 8.5)]   // sqrt(3² + 8²)  = sqrt(73)   = 8.544… -> 8.5
    [InlineData("L3", "L8", 8.5)]   // sqrt(6² + 6²)  = sqrt(72)   = 8.485… -> 8.5
    [InlineData("L5", "L9", 34.9)]  // sqrt(34² + 8²) = sqrt(1220) = 34.928… -> 34.9
    public void Leg_is_euclidean_distance_rounded_to_one_decimal(string from, string to, double expectedKm)
    {
        Assert.Equal(expectedKm, Distance.Leg(At(from), At(to)), Tolerance);
    }

    [Fact]
    [Trait("Rule", "R14")]
    public void Leg_rounds_midpoint_away_from_zero()
    {
        // 0.25 is exactly representable: AwayFromZero gives 0.3, the default banker's rounding would give 0.2.
        var leg = Distance.Leg(new Location("P", 0, 0), new Location("Q", 0.25, 0));

        Assert.Equal(0.3, leg, Tolerance);
    }

    [Fact]
    [Trait("Rule", "R14")]
    public void Leg_is_symmetric_for_every_pair_of_locations()
    {
        foreach (var a in Locations)
        {
            foreach (var b in Locations)
            {
                Assert.Equal(Distance.Leg(a, b), Distance.Leg(b, a), Tolerance);
            }
        }
    }

    [Theory]
    [Trait("Rule", "R15")]
    [MemberData(nameof(AllLocationIds))]
    public void Leg_from_a_location_to_itself_is_zero(string id)
    {
        Assert.Equal(0.0, Distance.Leg(At(id), At(id)), Tolerance);
    }

    [Fact]
    [Trait("Rule", "R14")]
    public void Matrix_returns_the_same_leg_as_Distance_Leg_for_every_pair()
    {
        var matrix = new DistanceMatrix(Locations);

        foreach (var a in Locations)
        {
            foreach (var b in Locations)
            {
                Assert.Equal(Distance.Leg(a, b), matrix[a.Id, b.Id], Tolerance);
            }
        }
    }
}
