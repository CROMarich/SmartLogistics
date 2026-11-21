using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.Services;

/// <summary>
/// The current scenario of the app (trucks, machines, locations, orders) that every page reads and the order pages edit.
/// <para>
/// Deliberately simple for the demo: one user, state kept in memory, every change is lost when the app restarts
/// (the store starts again from data/scenario.json). A production system would keep the scenario in a database
/// with users, concurrency control and history.
/// </para>
/// <para>
/// Every read returns an immutable snapshot: a change builds new lists and a new <see cref="Scenario"/> record, so a
/// reader never sees half of a change. The store does not validate the domain (unknown machines, duplicate IDs inside
/// the file, non-positive weights ...): the planner is robust to any data (spec R37) and reports such orders as K4.
/// Expected failures (unknown or duplicate IDs) are returned as <see cref="ScenarioStoreResult"/>, never thrown.
/// </para>
/// </summary>
public interface IScenarioStore
{
    /// <summary>The current scenario (immutable snapshot).</summary>
    Scenario Get();

    /// <summary>The current scenario together with its version and origin, read atomically (for caches and the UI).</summary>
    ScenarioSnapshot GetSnapshot();

    /// <summary>
    /// Appends an order. When <paramref name="newMachine"/> is given it is added to the machines in the same change
    /// (a machine typed into the order form). Fails when the order ID or the new machine ID already exists.
    /// </summary>
    ScenarioStoreResult AddOrder(TransportOrder order, Machine? newMachine = null);

    /// <summary>
    /// Replaces the order with the same <see cref="TransportOrder.Id"/> (the first one if the file contains duplicates),
    /// keeping its position. An optional <paramref name="newMachine"/> is added like in <see cref="AddOrder"/>.
    /// Fails when no order has that ID or the new machine ID already exists.
    /// </summary>
    ScenarioStoreResult UpdateOrder(TransportOrder order, Machine? newMachine = null);

    /// <summary>
    /// Removes the order with that ID (the first one if the file contains duplicates). Machines stay in the scenario.
    /// Fails when no order has that ID.
    /// </summary>
    ScenarioStoreResult DeleteOrder(string orderId);

    /// <summary>
    /// Discards all changes and loads the initial data again (data/scenario.json). When the file cannot be loaded the
    /// current state is kept and the result is a failure with the reason.
    /// </summary>
    ScenarioStoreResult Reset();
}

/// <summary>
/// One consistent state of the store.
/// </summary>
/// <param name="Scenario">The scenario; empty (no trucks, orders ...) when the initial data could not be loaded.</param>
/// <param name="Version">Increases with every successful change or reset; equal versions mean equal data.</param>
/// <param name="Source">Where the initial data came from, for the UI (e.g. "data/scenario.json").</param>
/// <param name="LoadedAt">When the initial data was loaded (app start or last reset), local time.</param>
/// <param name="ChangeCount">Successful changes since <paramref name="LoadedAt"/>; 0 = the data equals the file.</param>
/// <param name="LoadError">Why the initial data could not be loaded (Croatian, for the UI), or null.</param>
public sealed record ScenarioSnapshot(
    Scenario Scenario,
    long Version,
    string Source,
    DateTime LoadedAt,
    int ChangeCount,
    string? LoadError)
{
    public bool IsModified => ChangeCount > 0;
}

/// <summary>Outcome of a store operation.</summary>
public enum ScenarioStoreStatus
{
    Ok,

    /// <summary>AddOrder: an order with the same ID already exists.</summary>
    DuplicateOrderId,

    /// <summary>AddOrder / UpdateOrder: the new machine's ID already exists.</summary>
    DuplicateMachineId,

    /// <summary>UpdateOrder / DeleteOrder: no order has that ID.</summary>
    OrderNotFound,

    /// <summary>Null or empty IDs, which the store cannot address later.</summary>
    InvalidArgument,

    /// <summary>Reset: the initial data could not be loaded; the current state is kept.</summary>
    LoadFailed,
}

/// <summary>Result of a store operation; <see cref="Message"/> is a Croatian sentence for the UI when it failed.</summary>
public sealed record ScenarioStoreResult(ScenarioStoreStatus Status, string? Message = null)
{
    public bool Succeeded => Status == ScenarioStoreStatus.Ok;

    public static ScenarioStoreResult Ok { get; } = new(ScenarioStoreStatus.Ok);

    public static ScenarioStoreResult Fail(ScenarioStoreStatus status, string message) => new(status, message);
}
