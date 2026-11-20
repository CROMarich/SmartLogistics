using SmartLogistics.Core.Domain;

namespace SmartLogistics.Tests.Support;

/// <summary>
/// Input data for hand-built plans. The task data is typed in independently of data/scenario.json
/// (docs/zadatak.md, docs/spec.md 3.2 and 3.5); <c>ScenarioDataTests</c> checks that both agree.
/// </summary>
internal static class TestScenarios
{
    public const string Base = "L0";

    public static Scenario Task { get; } = new(
        Base,
        [
            new Truck("A", 3500, 2),
            new Truck("B", 10000, 3),
        ],
        [
            new Machine("M01", 800),
            new Machine("M02", 1300),
            new Machine("M03", 1800),
            new Machine("M04", 2400),
            new Machine("M05", 3100),
            new Machine("M06", 3900),
            new Machine("M07", 4800),
            new Machine("M08", 6100),
            new Machine("M09", 7500),
            new Machine("M10", 9400),
        ],
        [
            new Location("L0", 0, 0),
            new Location("L1", 3, 8),
            new Location("L2", 10, 11),
            new Location("L3", 17, 9),
            new Location("L4", 9, -6),
            new Location("L5", 26, 3),
            new Location("L6", -7, 13),
            new Location("L7", 20, -8),
            new Location("L8", 23, 15),
            new Location("L9", -8, -5),
        ],
        [
            new TransportOrder("N01", "M01", "L1", "L3"),
            new TransportOrder("N02", "M02", "L2", "L5"),
            new TransportOrder("N03", "M03", "L9", "L4"),
            new TransportOrder("N04", "M04", "L3", "L0"),
            new TransportOrder("N05", "M05", "L0", "L8"),
            new TransportOrder("N06", "M06", "L5", "L7"),
            new TransportOrder("N07", "M07", "L8", "L2"),
            new TransportOrder("N08", "M08", "L4", "L9"),
            new TransportOrder("N09", "M09", "L6", "L1"),
            new TransportOrder("N10", "M10", "L7", "L3"),
        ]);

    /// <summary>The task data reduced to the given orders (trucks, machines and locations unchanged).</summary>
    public static Scenario TaskWithOrders(params string[] orderIds) =>
        Task with { Orders = Task.Orders.Where(o => orderIds.Contains(o.Id)).ToList() };

    /// <summary>
    /// Hypothetical scenario that isolates R12: three 100 kg machines (300 kg total, far below 3500 kg),
    /// so three machines on A break only the machine-count limit, not the weight limit.
    /// P1 (3,4) is 5.0 km from L0, P2 (6,8) is 5.0 km from P1 and 10.0 km from L0.
    /// </summary>
    public static Scenario LightMachines { get; } = new(
        Base,
        [
            new Truck("A", 3500, 2),
            new Truck("B", 10000, 3),
        ],
        [
            new Machine("X1", 100),
            new Machine("X2", 100),
            new Machine("X3", 100),
        ],
        [
            new Location("L0", 0, 0),
            new Location("P1", 3, 4),
            new Location("P2", 6, 8),
        ],
        [
            new TransportOrder("Q1", "X1", "P1", "P2"),
            new TransportOrder("Q2", "X2", "P1", "P2"),
            new TransportOrder("Q3", "X3", "P1", "P2"),
        ]);

    /// <summary>Path of data/scenario.json copied to the test output directory.</summary>
    public static string ScenarioJsonPath => Path.Combine(AppContext.BaseDirectory, "data", "scenario.json");
}
