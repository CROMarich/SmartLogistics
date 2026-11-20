using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class UlazniPodaciModel(ScenarioProvider scenarios) : PageModel
{
    public InputDataPageViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = InputDataViewModelFactory.Create(scenarios.Load());
}
