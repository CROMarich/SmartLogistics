using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

/// <summary>
/// Project overview: key figures from the documents and the phase timeline. The optimum quoted in phase 4 is computed at
/// runtime by the exact planner on the original task data (the baseline and DrivingOnly cases of <see cref="ScenarioLab"/>,
/// cached and shared with /Scenariji), never typed in (T61).
/// </summary>
public class PregledModel(ProjectDocumentStore documents, ScenarioLab lab) : PageModel
{
    public HomeViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = HomeViewModelFactory.Create(documents, ProjectPhases.Create(ComputeOptima()));

    private ComputedOptima ComputeOptima()
    {
        if (lab.LoadError is not null)
        {
            return new ComputedOptima(null, null);
        }

        double? Km(string key) => lab.FindCase(key) is { } labCase ? lab.Run(labCase).Plan?.TotalKm : null;
        return new ComputedOptima(Km(ScenarioLab.BaselineKey), Km(ScenarioLab.DrivingOnlyKey));
    }
}
