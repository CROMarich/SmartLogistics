using System.Globalization;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.Services;

/// <summary>
/// Generates IDs for new orders and machines (T43 d): the next free "N.." / "M.." after the highest number in use.
/// Pure functions, robust to any data (null entries, IDs in other formats are ignored).
/// </summary>
public static class ScenarioIds
{
    public const string OrderPrefix = "N";
    public const string MachinePrefix = "M";

    /// <summary>N01..N10 → "N11"; no orders → "N01".</summary>
    public static string NextOrderId(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return NextId(OrderPrefix, (scenario.Orders ?? []).Select(o => o?.Id));
    }

    /// <summary>M01..M10 → "M11"; no machines → "M01".</summary>
    public static string NextMachineId(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return NextId(MachinePrefix, (scenario.Machines ?? []).Select(m => m?.Id));
    }

    /// <summary>
    /// Prefix + (highest number among IDs of the form prefix + digits) + 1, at least two digits ("N07", "N100").
    /// Skips numbers whose ID already exists in another spelling (e.g. "N011" next to "N11").
    /// </summary>
    public static string NextId(string prefix, IEnumerable<string?> existingIds)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(existingIds);

        var ids = existingIds.Where(id => id is not null).Select(id => id!).ToHashSet(StringComparer.Ordinal);
        var highest = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.Ordinal))
            .Select(id => id[prefix.Length..])
            .Where(digits => digits.All(char.IsAsciiDigit) && digits.Length <= 9)
            .Select(digits => int.Parse(digits, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

        var next = highest + 1;
        while (ids.Contains(Format(prefix, next)))
        {
            next++;
        }

        return Format(prefix, next);
    }

    private static string Format(string prefix, int number) => prefix + number.ToString("00", CultureInfo.InvariantCulture);
}
