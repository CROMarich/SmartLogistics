using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

/// <summary>
/// "Testni scenariji": edge cases built from the original task data (/Scenariji?slucaj=k1, /Scenariji?slucaj=kvar&amp;kvar=r11),
/// planned by the exact planner and checked by the independent validator. GET only, so every case can be shared by URL.
/// The store is only read (to say that the user's edits are not used); it is never changed here.
/// </summary>
public class ScenarijiModel(ScenarioLab lab, IScenarioStore store) : PageModel
{
    /// <summary>Case key, e.g. "osnova", "k1" … "k4", "kvar".</summary>
    [BindProperty(SupportsGet = true, Name = ScenarioLabViewModelFactory.CaseParameter)]
    public string? Case { get; set; }

    /// <summary>Defect key of the broken-plan case, e.g. "preopterecenje".</summary>
    [BindProperty(SupportsGet = true, Name = ScenarioLabViewModelFactory.DefectParameter)]
    public string? Defect { get; set; }

    public ScenarioLabPageViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = ScenarioLabViewModelFactory.Create(lab, Case, Defect, store.GetSnapshot());
}
