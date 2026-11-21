using System.Collections.ObjectModel;
using System.Text.Json;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.Services;

/// <summary>
/// <see cref="IScenarioStore"/> kept in memory, registered as a singleton.
/// <para>
/// Deliberately simple for the demo: one user, no persistence – every change is lost on restart, and the store starts
/// again from the initial data (data/scenario.json). In production this would be a database. All operations take one
/// lock; reads return the current immutable <see cref="ScenarioSnapshot"/> (lists are copied into read-only
/// collections), so readers never see a half-applied change and never need the lock afterwards.
/// </para>
/// </summary>
public sealed class InMemoryScenarioStore : IScenarioStore
{
    /// <summary>File name of the task scenario inside data/.</summary>
    public const string FileName = "scenario.json";

    /// <summary>Display name of the task scenario file.</summary>
    public const string DefaultSource = "data/" + FileName;

    private static readonly Scenario Empty = new(string.Empty, [], [], [], []);

    private readonly Func<(Scenario? Scenario, string? Error)> _load;
    private readonly string _source;
    private readonly Lock _gate = new();
    private ScenarioSnapshot _current;

    private InMemoryScenarioStore(Func<(Scenario? Scenario, string? Error)> load, string source)
    {
        _load = load;
        _source = source;

        var (scenario, error) = load();
        _current = new ScenarioSnapshot(Freeze(scenario ?? Empty), Version: 1, source, DateTime.Now, ChangeCount: 0, error);
    }

    /// <summary>
    /// Loads the scenario from a JSON file (<see cref="ScenarioLoader"/>); <see cref="Reset"/> reads the file again.
    /// A missing path or file, or malformed JSON, gives an empty scenario with <see cref="ScenarioSnapshot.LoadError"/>.
    /// </summary>
    public InMemoryScenarioStore(string? scenarioPath, string source = DefaultSource)
        : this(() => LoadFile(scenarioPath, source), source)
    {
    }

    /// <summary>Starts from the given scenario (e.g. in tests); <see cref="Reset"/> returns to it.</summary>
    public static InMemoryScenarioStore FromScenario(Scenario scenario, string source = "zadani scenarij")
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new InMemoryScenarioStore(() => (scenario, null), source);
    }

    /// <summary>The store for the app: data/scenario.json found via <see cref="ProjectPaths"/>.</summary>
    public static InMemoryScenarioStore FromEnvironment(IWebHostEnvironment environment) =>
        new(TaskDataPath(environment));

    /// <summary>Full path of the task scenario (data/scenario.json via <see cref="ProjectPaths"/>), or null when data/ is not found.</summary>
    public static string? TaskDataPath(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var dataDirectory = ProjectPaths.FindDirectory(environment, "data");
        return dataDirectory is null ? null : Path.Combine(dataDirectory, FileName);
    }

    public Scenario Get() => GetSnapshot().Scenario;

    public ScenarioSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _current;
        }
    }

    public ScenarioStoreResult AddOrder(TransportOrder order, Machine? newMachine = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        lock (_gate)
        {
            var scenario = _current.Scenario;
            if (string.IsNullOrWhiteSpace(order.Id))
            {
                return ScenarioStoreResult.Fail(ScenarioStoreStatus.InvalidArgument, "Nalog mora imati oznaku.");
            }

            if (scenario.Orders.Any(o => o?.Id == order.Id))
            {
                return ScenarioStoreResult.Fail(ScenarioStoreStatus.DuplicateOrderId, $"Nalog {order.Id} već postoji.");
            }

            if (CheckNewMachine(scenario, newMachine) is { } machineError)
            {
                return machineError;
            }

            Commit(scenario with
            {
                Orders = [.. scenario.Orders, order],
                Machines = newMachine is null ? scenario.Machines : [.. scenario.Machines, newMachine],
            });
            return ScenarioStoreResult.Ok;
        }
    }

    public ScenarioStoreResult UpdateOrder(TransportOrder order, Machine? newMachine = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        lock (_gate)
        {
            var scenario = _current.Scenario;
            var index = IndexOf(scenario, order.Id);
            if (index < 0)
            {
                return NotFound(order.Id);
            }

            if (CheckNewMachine(scenario, newMachine) is { } machineError)
            {
                return machineError;
            }

            var orders = scenario.Orders.ToArray();
            orders[index] = order;
            Commit(scenario with
            {
                Orders = orders,
                Machines = newMachine is null ? scenario.Machines : [.. scenario.Machines, newMachine],
            });
            return ScenarioStoreResult.Ok;
        }
    }

    public ScenarioStoreResult DeleteOrder(string orderId)
    {
        lock (_gate)
        {
            var scenario = _current.Scenario;
            var index = IndexOf(scenario, orderId);
            if (index < 0)
            {
                return NotFound(orderId);
            }

            Commit(scenario with { Orders = scenario.Orders.Where((_, i) => i != index).ToArray() });
            return ScenarioStoreResult.Ok;
        }
    }

    public ScenarioStoreResult Reset()
    {
        lock (_gate)
        {
            var (scenario, error) = _load();
            if (scenario is null)
            {
                return ScenarioStoreResult.Fail(ScenarioStoreStatus.LoadFailed, error ?? $"Podatke iz {_source} nije moguće učitati.");
            }

            _current = new ScenarioSnapshot(Freeze(scenario), _current.Version + 1, _source, DateTime.Now, ChangeCount: 0, LoadError: null);
            return ScenarioStoreResult.Ok;
        }
    }

    // Callers hold the lock.
    private void Commit(Scenario changed) =>
        _current = _current with
        {
            Scenario = Freeze(changed),
            Version = _current.Version + 1,
            ChangeCount = _current.ChangeCount + 1,
        };

    private static int IndexOf(Scenario scenario, string? orderId)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return -1;
        }

        for (var i = 0; i < scenario.Orders.Count; i++)
        {
            if (scenario.Orders[i]?.Id == orderId)
            {
                return i;
            }
        }

        return -1;
    }

    private static ScenarioStoreResult NotFound(string? orderId) =>
        ScenarioStoreResult.Fail(ScenarioStoreStatus.OrderNotFound, $"Nalog {orderId} ne postoji.");

    private static ScenarioStoreResult? CheckNewMachine(Scenario scenario, Machine? newMachine)
    {
        if (newMachine is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(newMachine.Id))
        {
            return ScenarioStoreResult.Fail(ScenarioStoreStatus.InvalidArgument, "Novi stroj mora imati oznaku.");
        }

        return scenario.Machines.Any(m => m?.Id == newMachine.Id)
            ? ScenarioStoreResult.Fail(ScenarioStoreStatus.DuplicateMachineId, $"Stroj {newMachine.Id} već postoji.")
            : null;
    }

    // Copies every list into a read-only collection, so a snapshot cannot be changed through a cast to List<T> or T[].
    private static Scenario Freeze(Scenario scenario) => scenario with
    {
        Trucks = ReadOnly(scenario.Trucks),
        Machines = ReadOnly(scenario.Machines),
        Locations = ReadOnly(scenario.Locations),
        Orders = ReadOnly(scenario.Orders),
    };

    private static ReadOnlyCollection<T> ReadOnly<T>(IReadOnlyList<T>? items) => (items ?? []).ToList().AsReadOnly();

    /// <summary>Loads a scenario file; a missing path or file, or malformed JSON, gives null and a Croatian error.</summary>
    internal static (Scenario? Scenario, string? Error) LoadFile(string? path, string source)
    {
        if (path is null)
        {
            return (null, "Mapa data/ nije pronađena.");
        }

        if (!File.Exists(path))
        {
            return (null, $"Datoteka {source} ne postoji.");
        }

        try
        {
            return (ScenarioLoader.LoadFromFile(path), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return (null, $"Datoteku {source} nije moguće učitati: {ex.Message}");
        }
    }
}
