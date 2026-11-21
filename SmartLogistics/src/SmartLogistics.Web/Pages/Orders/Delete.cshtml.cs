using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.Ui;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages.Orders;

/// <summary>Delete confirmation (/Orders/Delete/N01): GET shows the order, POST deletes it (PRG to the list).</summary>
public class DeleteModel(IScenarioStore store) : PageModel
{
    public OrderDeleteViewModel ViewModel { get; private set; } = null!;

    public IActionResult OnGet(string id)
    {
        ViewModel = OrderViewModelFactory.CreateDelete(store.Get(), id);
        if (ViewModel.Order is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
        }

        return Page();
    }

    public IActionResult OnPost(string id)
    {
        var result = store.DeleteOrder(id);
        if (result.Succeeded)
        {
            FlashMessage.Set(TempData, $"Nalog {id} je obrisan.");
            return RedirectToPage("/Orders/Index");
        }

        ModelState.AddModelError(string.Empty, result.Message ?? $"Nalog {id} nije obrisan.");
        ViewModel = OrderViewModelFactory.CreateDelete(store.Get(), id);
        return Page();
    }
}
