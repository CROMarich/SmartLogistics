using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class KartaModel(PlanService plans) : PageModel
{
    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Karta ruta",
        "Rute kamiona od baze L0 kroz sve radnje i natrag; strelice pokazuju smjer vožnje.");

    public PlanPageViewModel ViewModel { get; private set; } = null!;

    /// <param name="nacin">"voznja" for WorkdayMode.DrivingOnly; anything else is the default WorkdayMode.Total.</param>
    public void OnGet(string? nacin) =>
        ViewModel = PlanViewModelFactory.Create(plans.Run(PlanViewModelFactory.ParseMode(nacin)), Header);
}
