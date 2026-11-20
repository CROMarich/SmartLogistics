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

    /// <summary>8.5 → "8,5" (for tables whose header already says km).</summary>
    public static string Number(double value) => value.ToString("0.0", Croatian);

    /// <summary>3500 → "3.500 kg".</summary>
    public static string Kg(int kg) => $"{Integer(kg)} kg";

    /// <summary>13500 → "13.500".</summary>
    public static string Integer(long value) => value.ToString("N0", Croatian);

    /// <summary>Coordinates without unnecessary decimals: -8 → "-8", 2.5 → "2,5".</summary>
    public static string Coordinate(double value) => value.ToString("0.##", Croatian);

    /// <summary>Minutes as a clock duration, rounded to whole minutes: 503.4 → "08:23".</summary>
    public static string Clock(double minutes)
    {
        var total = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        return $"{total / 60:00}:{total % 60:00}";
    }

    /// <summary>0.7 → "70 %".</summary>
    public static string Percent(double fraction) => $"{Math.Round(fraction * 100, MidpointRounding.AwayFromZero):0} %";

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
