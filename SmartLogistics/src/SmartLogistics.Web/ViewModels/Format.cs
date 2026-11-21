using System.Globalization;

namespace SmartLogistics.Web.ViewModels;

/// <summary>
/// Display formatting shared by all view models. The UI is Croatian (decimal comma, dot as thousands
/// separator) regardless of the server culture; SVG attributes always use the invariant culture.
/// Pages never format values themselves.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Croatian = CultureInfo.GetCultureInfo("hr-HR");

    /// <summary>8.5 → "8,5 km" (one decimal, as legs are rounded to one decimal).</summary>
    public static string Km(double km) => $"{Number(km)} km";

    /// <summary>
    /// Signed km difference with one decimal (rounded AwayFromZero first): 3.44 → "+3,4 km", -1.2 → "−1,2 km",
    /// 0.04 → "0,0 km" (no sign when the rounded difference is zero).
    /// </summary>
    public static string SignedKm(double km)
    {
        var rounded = Math.Round(km, 1, MidpointRounding.AwayFromZero);
        var sign = rounded > 0 ? "+" : rounded < 0 ? "−" : string.Empty;
        return $"{sign}{Number(Math.Abs(rounded))} km";
    }

    /// <summary>8.5 → "8,5" (for tables whose header already says km).</summary>
    public static string Number(double value) => value.ToString("0.0", Croatian);

    /// <summary>3500 → "3.500 kg".</summary>
    public static string Kg(int kg) => $"{Integer(kg)} kg";

    /// <summary>13500 → "13.500".</summary>
    public static string Integer(long value) => value.ToString("N0", Croatian);

    /// <summary>Coordinates without unnecessary decimals: -8 → "-8", 2.5 → "2,5".</summary>
    public static string Coordinate(double value) => value.ToString("0.##", Croatian);

    /// <summary>
    /// Minutes as a clock duration, rounded to whole minutes (midpoint away from zero), at least two digits for hours:
    /// 522.9 → "08:43", 37.5 → "00:38", 600 → "10:00". Sums of legs can carry floating-point noise
    /// (37.4999999999 instead of 37.5), so the value is first rounded to 6 decimals.
    /// </summary>
    public static string Clock(double minutes)
    {
        var total = (long)Math.Round(Math.Round(minutes, 6), MidpointRounding.AwayFromZero);
        var sign = total < 0 ? "-" : string.Empty;
        total = Math.Abs(total);
        return $"{sign}{total / 60:00}:{total % 60:00}";
    }

    /// <summary>0.7 → "70 %".</summary>
    public static string Percent(double fraction) => $"{Math.Round(fraction * 100, MidpointRounding.AwayFromZero):0} %";

    /// <summary>
    /// Share as a whole percentage, computed exactly in integers (midpoint away from zero):
    /// Percent(9900, 10000) → "99 %", Percent(1750, 3500) → "50 %", Percent(145, 1000) → "15 %".
    /// "–" when <paramref name="whole"/> is not positive (nothing to compare with).
    /// </summary>
    public static string Percent(long part, long whole) =>
        whole <= 0 ? "–" : $"{Math.Round(part * 100m / whole, MidpointRounding.AwayFromZero):0} %";

    /// <summary>
    /// Share of two measured values (e.g. minutes of the working day) as a whole percentage, midpoint away from zero,
    /// computed in decimal so that 522.9 / 540 does not suffer from binary rounding: → "97 %".
    /// "–" when <paramref name="whole"/> is not positive or a value is not finite.
    /// </summary>
    public static string Percent(double part, double whole)
    {
        if (!(whole > 0) || !double.IsFinite(part) || !double.IsFinite(whole) || Math.Abs(part) > 1e15 || whole > 1e15)
        {
            return "–";
        }

        return $"{Math.Round((decimal)part * 100m / (decimal)whole, MidpointRounding.AwayFromZero):0} %";
    }

    /// <summary>Elapsed computation time: "12 ms" or "1,4 s".</summary>
    public static string Elapsed(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds < 1000
            ? $"{Math.Max(1, Math.Round(elapsed.TotalMilliseconds)):0} ms"
            : $"{elapsed.TotalSeconds.ToString("0.0", Croatian)} s";

    /// <summary>Number for SVG attributes (always a decimal point).</summary>
    public static string Svg(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Croatian plural form: 1, 21, 31… → <paramref name="one"/>; 2–4, 22–24… → <paramref name="few"/>;
    /// everything else (0, 5–20, 25…) → <paramref name="many"/>.
    /// Example: Plural(3, "odluka", "odluke", "odluka") → "odluke".
    /// </summary>
    public static string Plural(int count, string one, string few, string many)
    {
        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;

        if (lastDigit == 1 && lastTwoDigits != 11)
        {
            return one;
        }

        var isFew = (lastDigit is >= 2 and <= 4) && (lastTwoDigits is < 12 or > 14);
        return isFew ? few : many;
    }
}
