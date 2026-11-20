using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class KartaModel(PlanService plans, ScenarioProvider scenarios) : PageModel
{
    private static readonly PageHeaderViewModel Header = new(
        "Planiranje",
        "Karta ruta",
        "Rute kamiona od baze L0 kroz sve radnje i natrag; strelice pokazuju smjer vožnje.");

    public PlanPageViewModel ViewModel { get; private set; } = null!;

    /// <param name="nacin">"voznja" for WorkdayMode.DrivingOnly; anything else is the default WorkdayMode.Total.</param>
    /// <param name="scenarij">Id of a scenario example from data/primjeri/primjeri.json; default is the task scenario.</param>
    public void OnGet(string? nacin, string? scenarij) =>
        ViewModel = PlanViewModelFactory.Create(
            plans.Run(PlanViewModelFactory.ParseMode(nacin), scenarij), scenarios.Examples(), Header);
}
