using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Core.Domain;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.Ui;
using SmartLogistics.Web.ViewModels.Transport;

namespace SmartLogistics.Web.Pages;

/// <summary>
/// "Plan prijevoza": GET form (/Plan?algoritam=exact|ortools&amp;nacin=ukupno|voznja&amp;maxMinuta=1..1440), so a
/// result can be shared by URL. Plans the current scenario of the store; an invalid form shows Croatian validation
/// messages and no plan. An algorithm without a registered planner shows a message instead of a plan.
/// </summary>
public class PlanModel(PlanService plans, IScenarioStore store) : PageModel
{
    /// <summary>"exact" (default) or "ortools" (<see cref="PlannerCatalog"/>).</summary>
    [BindProperty(SupportsGet = true, Name = PlanForm.AlgorithmParameter)]
    public string? Algorithm { get; set; }

    /// <summary>"ukupno" (WorkdayMode.Total, default) or "voznja" (WorkdayMode.DrivingOnly).</summary>
    [BindProperty(SupportsGet = true, Name = PlanForm.ModeParameter)]
    public string? Mode { get; set; }

    /// <summary>Working-day limit in minutes; default 540.</summary>
    [BindProperty(SupportsGet = true, Name = PlanForm.MaxMinutesParameter)]
    [Display(Name = "najdulje trajanje radnog dana")]
    [Range(PlanForm.MinMinutes, PlanForm.MaxMinutesLimit, ErrorMessage = PlanForm.MaxMinutesRangeMessage)]
    public int? MaxMinutes { get; set; }

    public PlanPageViewModel ViewModel { get; private set; } = null!;

    public void OnGet()
    {
        var mode = PlanForm.ParseMode(Mode);
        if (mode is null)
        {
            ModelState.AddModelError(PlanForm.ModeParameter, PlanForm.UnknownModeMessage);
        }

        var algorithm = PlanForm.ParseAlgorithm(Algorithm);
        if (algorithm is null)
        {
            ModelState.AddModelError(PlanForm.AlgorithmParameter, PlanForm.UnknownAlgorithmMessage);
        }

        var maxMinutes = MaxMinutes ?? PlanForm.DefaultMaxMinutes;
        var maxMinutesError = ModelState.FirstError(PlanForm.MaxMinutesParameter);

        // Show what the user typed when it was invalid (e.g. "abc" or "5000"), otherwise the bound value.
        var maxMinutesText = maxMinutesError is not null
            ? ModelState[PlanForm.MaxMinutesParameter]?.AttemptedValue ?? string.Empty
            : maxMinutes.ToString(CultureInfo.InvariantCulture);

        var form = PlanForm.Create(
            mode ?? WorkdayMode.Total,
            algorithm ?? PlannerCatalog.Exact,
            plans.IsAvailable,
            maxMinutesText,
            ModelState.FirstError(PlanForm.ModeParameter),
            ModelState.FirstError(PlanForm.AlgorithmParameter),
            maxMinutesError);

        if (!ModelState.IsValid)
        {
            ViewModel = PlanPageViewModelFactory.Create(form, null, store.GetSnapshot());
            return;
        }

        var options = PlanningOptions.Default with { WorkdayMode = mode!.Value, MaxMinutes = maxMinutes };
        var run = plans.Run(options, algorithm!);

        // Comparison only from the cache: the other algorithm runs only when the user asks for it (it may take seconds).
        var comparison = PlannerCatalog.All
            .Where(plans.IsAvailable)
            .Select(p => new PlanComparisonEntry(p, p.Key == algorithm!.Key ? run : plans.TryGetCached(options, p)))
            .ToList();

        ViewModel = PlanPageViewModelFactory.Create(form, run, store.GetSnapshot(), comparison);
    }
}
