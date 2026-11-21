using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.Ui;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages.Orders;

/// <summary>
/// Shared part of the create and edit pages: the posted <see cref="Input"/>, the select options and the save flow
/// (DataAnnotations → checks against the scenario → store → Post-Redirect-Get with a TempData message).
/// A failed store operation is shown as a form error in ModelState, never as an exception.
/// </summary>
public abstract class OrderFormPageModel(IScenarioStore store) : PageModel
{
    [BindProperty]
    public OrderInputModel Input { get; set; } = new();

    public OrderFormViewModel Form { get; private set; } = null!;

    /// <summary>Set when the edited order does not exist (the page shows it instead of the form, status 404).</summary>
    public string? NotFoundMessage { get; private set; }

    protected IScenarioStore Store => store;

    /// <param name="orderId">The edited order, or null for a new one.</param>
    protected void BuildForm(Scenario scenario, string? orderId) => Form = OrderViewModelFactory.CreateForm(scenario, orderId);

    protected IActionResult OrderNotFound(string orderId)
    {
        NotFoundMessage = $"Nalog {orderId} ne postoji (možda je obrisan ili su vraćeni izvorni podaci).";
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }

    /// <param name="orderId">ID of the edited order; null = new order with the next free ID.</param>
    /// <param name="save">The store call (AddOrder or UpdateOrder).</param>
    /// <param name="verb">"dodan" / "izmijenjen", for the success message.</param>
    protected IActionResult Save(string? orderId, Func<TransportOrder, Machine?, ScenarioStoreResult> save, string verb)
    {
        var scenario = store.Get();
        if (ModelState.IsValid)
        {
            foreach (var (field, message) in OrderViewModelFactory.CheckAgainstScenario(Input, scenario))
            {
                ModelState.AddModelError($"{nameof(Input)}.{field}", message);
            }
        }

        if (ModelState.IsValid)
        {
            var (order, newMachine) = OrderViewModelFactory.ToDomain(Input, orderId ?? ScenarioIds.NextOrderId(scenario), scenario);
            var result = save(order, newMachine);
            if (result.Succeeded)
            {
                FlashMessage.Set(TempData, OrderViewModelFactory.SuccessMessage(verb, order, newMachine));
                return RedirectToPage("/Orders/Index");
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Nalog nije spremljen.");
        }

        BuildForm(store.Get(), orderId);
        return Page();
    }
}
