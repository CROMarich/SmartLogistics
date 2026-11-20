namespace SmartLogistics.Core.Domain;

/// <summary>Which time counts against the working-day limit (spec D1, R20–R22).</summary>
public enum WorkdayMode
{
    /// <summary>Driving + actions must fit into <see cref="PlanningOptions.MaxMinutes"/> (default).</summary>
    Total,

    /// <summary>Only driving must fit into <see cref="PlanningOptions.MaxMinutes"/>.</summary>
    DrivingOnly,
}

/// <summary>Parameters of a planning run; defaults match the task (9 h, 20 min per action, 60 km/h).</summary>
public sealed record PlanningOptions(
    WorkdayMode WorkdayMode = WorkdayMode.Total,
    double MaxMinutes = 540,
    double MinutesPerAction = 20,
    double SpeedKmh = 60)
{
    public static PlanningOptions Default { get; } = new();

    /// <summary>
    /// Driving time for a distance. Written as km * (60 / speed) so that at 60 km/h the factor is
    /// exactly 1.0 and minutes equal kilometres without floating-point drift (spec R17).
    /// </summary>
    public double DrivingMinutes(double km) => km * (60.0 / SpeedKmh);
}
