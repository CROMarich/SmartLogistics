using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.Ui;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages.Orders;

/// <summary>List of orders with edit / delete buttons and the reset button.</summary>
public class IndexModel(IScenarioStore store) : PageModel
{
    public OrdersPageViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = OrderViewModelFactory.CreateList(store.GetSnapshot());

    /// <summary>"Vrati izvorne podatke": reloads data/scenario.json, then Post-Redirect-Get.</summary>
    public IActionResult OnPostReset()
    {
        var result = store.Reset();
        if (result.Succeeded)
        {
            FlashMessage.Set(TempData, $"Izvorni podaci su vraćeni ({store.GetSnapshot().Source}); sve izmjene naloga su odbačene.");
            return RedirectToPage();
        }

        ModelState.AddModelError(string.Empty, result.Message ?? "Izvorne podatke nije moguće vratiti.");
        OnGet();
        return Page();
    }
}
