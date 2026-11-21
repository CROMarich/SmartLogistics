using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// Display formats of the Web layer (docs/odluke.md T42 b, T46 e/f): km with one decimal and a decimal comma,
/// kg with a dot as thousands separator, hh:mm with whole minutes (midpoint away from zero) and two-digit hours,
/// shares as whole percentages (midpoint away from zero).
/// </summary>
[Trait("Area", "Web")]
public class FormatTests
{
    [Theory]
    [Trait("Rule", "R14")]
    [InlineData(162.9, "162,9 km")] // CLAUDE.md: truck B in the reference split
    [InlineData(37.2, "37,2 km")]   // CLAUDE.md: truck A in the reference split
    [InlineData(200.1, "200,1 km")] // CLAUDE.md: Total optimum
    [InlineData(182.2, "182,2 km")] // CLAUDE.md: DrivingOnly optimum
    [InlineData(0.0, "0,0 km")]     // unused truck / leg L0 -> L0
    [InlineData(8.5, "8,5 km")]
    [InlineData(17.0, "17,0 km")]   // one decimal is always shown
    public void Km_has_one_decimal_with_a_decimal_comma(double km, string expected)
    {
        Assert.Equal(expected, Format.Km(km));
    }

    [Theory]
    [InlineData(10000, "10.000 kg")] // capacity of B
    [InlineData(3500, "3.500 kg")]   // capacity of A
    [InlineData(9900, "9.900 kg")]   // M04 + M09
    [InlineData(800, "800 kg")]
    [InlineData(0, "0 kg")]
    [InlineData(1000000, "1.000.000 kg")]
    public void Kg_uses_a_dot_as_thousands_separator(int kg, string expected)
    {
        Assert.Equal(expected, Format.Kg(kg));
    }

    [Theory]
    [Trait("Rule", "R17")]
    [Trait("Rule", "R18")]
    [InlineData(522.9, "08:43")] // B in Total: 162.9 + 18 × 20 = 522.9 → 523 min = 8 h 43 min
    [InlineData(77.2, "01:17")]  // A in Total: 37.2 + 2 × 20 = 77.2 → 77 min = 1 h 17 min
    [InlineData(0.0, "00:00")]   // departure
    [InlineData(540.0, "09:00")] // working-day limit
    [InlineData(582.2, "09:42")] // B in DrivingOnly: 182.2 + 20 × 20 = 582.2 → 582 min
    [InlineData(600.0, "10:00")]
    [InlineData(59.4, "00:59")]
    [InlineData(1500.0, "25:00")] // hours are not wrapped at 24
    public void Clock_rounds_to_whole_minutes_with_two_digit_hours(double minutes, string expected)
    {
        Assert.Equal(expected, Format.Clock(minutes));
    }

    [Theory]
    [InlineData(37.5, "00:38")]   // exactly half a minute → away from zero
    [InlineData(38.5, "00:39")]   // banker's rounding would give 38 → "00:38"
    [InlineData(0.5, "00:01")]    // banker's rounding would give 0 → "00:00"
    [InlineData(59.5, "01:00")]   // rounding carries into the hour
    [InlineData(67.5, "01:08")]   // A with N05 alone: 20 + 27.5 + 20 (see PlanMapperTests)
    [InlineData(0.4999, "00:00")] // just below the midpoint
    public void Clock_rounds_exactly_half_a_minute_away_from_zero(double minutes, string expected)
    {
        Assert.Equal(expected, Format.Clock(minutes));
    }

    [Fact]
    public void Clock_treats_a_sum_of_legs_that_should_be_a_half_minute_as_the_midpoint()
    {
        // T46 f: a sum of one-decimal legs can carry floating-point noise below the midpoint.
        // 13.6 + 11.2 + 0.7 is mathematically 25.5, but in double it is 25.499999999999996.
        double[] legs = [13.6, 11.2, 0.7];
        var minutes = 0.0;
        foreach (var leg in legs)
        {
            minutes += leg;
        }

        Assert.True(minutes < 25.5, "precondition: the double sum lies just below the midpoint");
        Assert.Equal(25.5, minutes, 6);
        Assert.Equal("00:26", Format.Clock(minutes));
    }

    [Theory]
    [Trait("Rule", "R11")]
    [InlineData(9900, 10000, "99 %")]   // B after pickup N09 in the reference route
    [InlineData(3100, 3500, "89 %")]    // M05 on A: 3100 / 3500 = 88.57 % → 89 %
    [InlineData(10000, 10000, "100 %")] // M06 + M08 on B: full capacity is allowed (spec D9)
    [InlineData(0, 3500, "0 %")]
    [InlineData(0, 10000, "0 %")]
    [InlineData(9400, 10000, "94 %")]   // M10 alone on B
    [InlineData(1750, 3500, "50 %")]
    [InlineData(145, 1000, "15 %")]     // exactly 14.5 % → away from zero
    [InlineData(125, 1000, "13 %")]     // exactly 12.5 % → away from zero (banker's would give 12)
    public void Capacity_percent_is_a_whole_percentage_rounded_away_from_zero(long loadKg, long capacityKg, string expected)
    {
        Assert.Equal(expected, Format.Percent(loadKg, capacityKg));
    }

    [Theory]
    [Trait("Rule", "R20")]
    [Trait("Rule", "R21")]
    [InlineData(522.9, 540.0, "97 %")] // B in Total: 522.9 / 540 = 96.83 % → 97 %
    [InlineData(77.2, 540.0, "14 %")]  // A in Total: 77.2 / 540 = 14.30 % → 14 %
    [InlineData(182.2, 540.0, "34 %")] // B in DrivingOnly: 182.2 / 540 = 33.74 % → 34 %
    [InlineData(540.0, 540.0, "100 %")]
    [InlineData(0.0, 540.0, "0 %")]
    [InlineData(78.3, 540.0, "15 %")]  // 78.3 / 540 = exactly 14.5 % → away from zero
    [InlineData(2.7, 540.0, "1 %")]    // 2.7 / 540 = exactly 0.5 % → away from zero
    public void Workday_percent_is_a_whole_percentage_rounded_away_from_zero(double minutes, double limit, string expected)
    {
        Assert.Equal(expected, Format.Percent(minutes, limit));
    }
}
