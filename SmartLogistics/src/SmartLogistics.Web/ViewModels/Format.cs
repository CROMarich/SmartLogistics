namespace SmartLogistics.Web.ViewModels;

/// <summary>
/// Display formatting shared by all view models (Croatian UI). Pages never format values themselves;
/// later phases add km, kg and hh:mm formatting here.
/// </summary>
public static class Format
{
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
