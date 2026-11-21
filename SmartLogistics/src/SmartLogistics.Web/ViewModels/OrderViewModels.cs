using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>
/// Posted fields of the order form (create and edit). The order ID is not posted: a new order gets the next free
/// "N.." (T43 d), an edited order keeps its ID from the route. Validation messages are Croatian.
/// A new machine is chosen with <see cref="NewMachineValue"/> and gets the next free "M.."; its weight must be
/// positive but may exceed every truck's capacity (the plan then shows K1, T43 e).
/// </summary>
public sealed class OrderInputModel : IValidatableObject
{
    /// <summary>Value of the "Novi stroj…" option in the machine select.</summary>
    public const string NewMachineValue = "__novi__";

    public const int MaxWeightKg = 1_000_000;

    [Display(Name = "Stroj")]
    [Required(ErrorMessage = "Odaberite stroj.")]
    public string? MachineId { get; set; }

    [Display(Name = "Masa novog stroja (kg)")]
    [Range(1, MaxWeightKg, ErrorMessage = "Masa novog stroja mora biti između 1 i 1.000.000 kg.")]
    public int? NewMachineWeightKg { get; set; }

    [Display(Name = "Preuzimanje")]
    [Required(ErrorMessage = "Odaberite lokaciju preuzimanja.")]
    public string? PickupLocationId { get; set; }

    [Display(Name = "Isporuka")]
    [Required(ErrorMessage = "Odaberite lokaciju isporuke.")]
    public string? DeliveryLocationId { get; set; }

    public bool IsNewMachine => MachineId == NewMachineValue;

    /// <summary>Rules across fields: pickup ≠ delivery, weight only (and always) for a new machine.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PickupLocationId is not null && PickupLocationId == DeliveryLocationId)
        {
            yield return new ValidationResult(
                "Lokacija isporuke mora biti različita od lokacije preuzimanja.", [nameof(DeliveryLocationId)]);
        }

        if (IsNewMachine && NewMachineWeightKg is null)
        {
            yield return new ValidationResult("Upišite masu novog stroja.", [nameof(NewMachineWeightKg)]);
        }

        if (!IsNewMachine && MachineId is not null && NewMachineWeightKg is not null)
        {
            yield return new ValidationResult(
                "Masa se upisuje samo za novi stroj: odaberite \"Novi stroj…\" ili obrišite masu.", [nameof(NewMachineWeightKg)]);
        }
    }
}

/// <summary>The create / edit form: select options and texts; the posted values are in <see cref="OrderInputModel"/>.</summary>
/// <param name="OrderId">The edited order's ID, or the ID the new order will get ("N11").</param>
/// <param name="NewMachineId">ID a new machine will get ("M11").</param>
/// <param name="MachineOptions">"M01 · 800 kg · u nalogu N01" … and "Novi stroj… (M11)".</param>
/// <param name="LocationOptions">"L1 (3, 8)" …, shared by pickup and delivery.</param>
public sealed record OrderFormViewModel(
    PageHeaderViewModel Header,
    bool IsEdit,
    string OrderId,
    string OrderIdHint,
    string NewMachineId,
    string NewMachineHint,
    IReadOnlyList<SelectListItem> MachineOptions,
    IReadOnlyList<SelectListItem> LocationOptions,
    string SubmitText);

/// <summary>The orders page (Pages/Orders/Index).</summary>
public sealed record OrdersPageViewModel(
    PageHeaderViewModel Header,
    string? Error,
    ScenarioStatusViewModel Status,
    IReadOnlyList<StatTileViewModel> Stats,
    OrdersTableViewModel Table);

/// <summary>Delete confirmation: the order as a one-row table, or null when it does not exist.</summary>
public sealed record OrderDeleteViewModel(PageHeaderViewModel Header, string OrderId, OrdersTableViewModel? Order);

/// <summary>
/// Builds the order pages and converts the form to domain objects. Pure functions over a <see cref="Scenario"/>.
/// </summary>
public static class OrderViewModelFactory
{
    public static OrdersPageViewModel CreateList(ScenarioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var header = new PageHeaderViewModel(
            "Planiranje",
            "Nalozi",
            "Dodavanje, izmjena i brisanje naloga trenutnog scenarija; plan prijevoza se odmah računa s izmjenama.");
        var status = InputDataViewModelFactory.CreateStatus(snapshot, "/Orders/Index");
        if (snapshot.LoadError is not null)
        {
            return new OrdersPageViewModel(header, snapshot.LoadError, status, [], new OrdersTableViewModel([], true));
        }

        var scenario = snapshot.Scenario;
        var table = InputDataViewModelFactory.CreateOrdersTable(scenario, showActions: true);
        var warnings = table.Rows.Count(r => r.DataWarning is not null);
        var changes = snapshot.ChangeCount;

        IReadOnlyList<StatTileViewModel> stats =
        [
            new("Nalozi", Format.Integer(table.Rows.Count),
                warnings == 0 ? "svi s ispravnim podacima" : $"{warnings} s neispravnim podacima (K4)", "list",
                warnings == 0 ? "sl-stat-tile-ok" : "sl-stat-tile-warning"),
            new("Strojevi", Format.Integer(scenario.Machines.Count), $"sljedeći novi: {ScenarioIds.NextMachineId(scenario)}", "layers", "sl-stat-tile-accent"),
            new("Izmjene", Format.Integer(changes),
                changes == 0 ? $"podaci kao u {snapshot.Source}" : "do ponovnog pokretanja aplikacije", "clock",
                changes == 0 ? "sl-stat-tile-neutral" : "sl-stat-tile-warning"),
        ];

        return new OrdersPageViewModel(header, null, status, stats, table);
    }

    /// <param name="orderId">The edited order, or null for a new one.</param>
    public static OrderFormViewModel CreateForm(Scenario scenario, string? orderId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var isEdit = orderId is not null;
        var newMachineId = ScenarioIds.NextMachineId(scenario);
        var id = orderId ?? ScenarioIds.NextOrderId(scenario);

        var usedIn = (scenario.Orders ?? [])
            .Where(o => o?.MachineId is not null && o.Id != orderId)
            .GroupBy(o => o.MachineId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(o => o.Id)));

        var machineOptions = (scenario.Machines ?? [])
            .Where(m => m?.Id is not null)
            .DistinctBy(m => m.Id)
            .Select(m => new SelectListItem(
                usedIn.TryGetValue(m.Id, out var orders)
                    ? $"{m.Id} · {Format.Kg(m.WeightKg)} · u nalogu {orders}"
                    : $"{m.Id} · {Format.Kg(m.WeightKg)}",
                m.Id))
            .Append(new SelectListItem($"Novi stroj… ({newMachineId}, masa u polju ispod)", OrderInputModel.NewMachineValue))
            .ToList();

        var locationOptions = (scenario.Locations ?? [])
            .Where(l => l?.Id is not null)
            .DistinctBy(l => l.Id)
            .Select(l => new SelectListItem(
                l.Id == scenario.BaseLocationId
                    ? $"{l.Id} – baza ({Format.Coordinate(l.X)}; {Format.Coordinate(l.Y)})"
                    : $"{l.Id} ({Format.Coordinate(l.X)}; {Format.Coordinate(l.Y)})",
                l.Id))
            .ToList();

        var header = isEdit
            ? new PageHeaderViewModel("Nalozi", $"Izmjena naloga {id}", "Promijeni stroj ili lokacije naloga; oznaka naloga ostaje ista.")
            : new PageHeaderViewModel("Nalozi", "Novi nalog", "Nalog dobiva sljedeću slobodnu oznaku; stroj se bira s popisa ili upisuje kao novi.");

        return new OrderFormViewModel(
            header,
            isEdit,
            id,
            isEdit ? "Oznaka naloga se ne mijenja." : "Dodjeljuje se automatski (sljedeća slobodna oznaka).",
            newMachineId,
            $"Samo uz \"Novi stroj…\": stroj {newMachineId} dodaje se uz nalog. Masa smije biti veća od nosivosti kamiona – plan ga tada prikazuje kao neizvediv (K1).",
            machineOptions,
            locationOptions,
            isEdit ? "Spremi izmjene" : "Dodaj nalog");
    }

    /// <summary>The posted values of an existing order, for the edit form; null when no order has that ID.</summary>
    public static OrderInputModel? ToInput(Scenario scenario, string orderId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var order = (scenario.Orders ?? []).FirstOrDefault(o => o?.Id == orderId);
        return order is null
            ? null
            : new OrderInputModel
            {
                MachineId = order.MachineId,
                PickupLocationId = order.PickupLocationId,
                DeliveryLocationId = order.DeliveryLocationId,
            };
    }

    /// <summary>
    /// Checks a (field-valid) form against the current scenario: the chosen machine and locations must exist.
    /// Returns (field name of <see cref="OrderInputModel"/>, Croatian message) pairs; empty = valid.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> CheckAgainstScenario(OrderInputModel input, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(scenario);

        var errors = new List<(string, string)>();
        if (!input.IsNewMachine && input.MachineId is not null && !(scenario.Machines ?? []).Any(m => m?.Id == input.MachineId))
        {
            errors.Add((nameof(OrderInputModel.MachineId), $"Stroj {input.MachineId} ne postoji u scenariju."));
        }

        var locations = (scenario.Locations ?? []).Where(l => l?.Id is not null).Select(l => l.Id).ToHashSet();
        if (input.PickupLocationId is not null && !locations.Contains(input.PickupLocationId))
        {
            errors.Add((nameof(OrderInputModel.PickupLocationId), $"Lokacija {input.PickupLocationId} ne postoji u scenariju."));
        }

        if (input.DeliveryLocationId is not null && !locations.Contains(input.DeliveryLocationId))
        {
            errors.Add((nameof(OrderInputModel.DeliveryLocationId), $"Lokacija {input.DeliveryLocationId} ne postoji u scenariju."));
        }

        return errors;
    }

    /// <summary>
    /// The order (and the new machine, if chosen) for the store. A new machine gets <see cref="ScenarioIds.NextMachineId"/>.
    /// Call only for a valid form.
    /// </summary>
    public static (TransportOrder Order, Machine? NewMachine) ToDomain(OrderInputModel input, string orderId, Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(scenario);

        var newMachine = input.IsNewMachine
            ? new Machine(ScenarioIds.NextMachineId(scenario), input.NewMachineWeightKg ?? 0)
            : null;
        var order = new TransportOrder(
            orderId,
            newMachine?.Id ?? input.MachineId ?? string.Empty,
            input.PickupLocationId ?? string.Empty,
            input.DeliveryLocationId ?? string.Empty);
        return (order, newMachine);
    }

    /// <summary>"Nalog N11 je dodan (novi stroj M11, 11.000 kg)." and similar success texts for TempData.</summary>
    public static string SuccessMessage(string verb, TransportOrder order, Machine? newMachine) =>
        newMachine is null
            ? $"Nalog {order.Id} je {verb}."
            : $"Nalog {order.Id} je {verb} (novi stroj {newMachine.Id}, {Format.Kg(newMachine.WeightKg)}).";

    public static OrderDeleteViewModel CreateDelete(Scenario scenario, string orderId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(orderId);
        var header = new PageHeaderViewModel("Nalozi", $"Brisanje naloga {orderId}",
            "Nalog se uklanja iz trenutnog scenarija; stroj ostaje na popisu strojeva. Izvorni podaci vraćaju se gumbom \"Vrati izvorne podatke\".");

        var row = InputDataViewModelFactory.CreateOrdersTable(scenario, showActions: false).Rows.FirstOrDefault(r => r.Id == orderId);
        return new OrderDeleteViewModel(header, orderId, row is null ? null : new OrdersTableViewModel([row], ShowActions: false));
    }
}
