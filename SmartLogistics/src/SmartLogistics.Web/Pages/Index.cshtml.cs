using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class IndexModel(ProjectDocumentStore documents) : PageModel
{
    // Project progress is project metadata (not domain data); it is updated at the end of each phase.
    private static readonly IReadOnlyList<PhaseViewModel> Phases =
    [
        new("0", "Kostur rješenja",
            ".NET 10 rješenje (Core, Web, Tests), pravila projekta u CLAUDE.md i šest podagenata.",
            PhaseStatus.Done),
        new("1", "Specifikacija domene",
            "Podagent domain-analyst napisao je docs/spec.md; tehnički vođa potvrdio je pretpostavke (pravila R29–R39, zapis u docs/odluke.md).",
            PhaseStatus.Done),
        new("UI", "Dizajn sustav",
            "Moderni izgled aplikacije: bočni izbornik, kartice, ikone i tokeni boja; pravila u docs/ui-dizajn.md.",
            PhaseStatus.Done),
        new("2", "Domenski model i ulazni podaci",
            "Record tipovi u Core/Domain, Distance (dionice i matrica), data/scenario.json, ScenarioLoader, IRoutePlanner i testovi udaljenosti.",
            PhaseStatus.Done),
        new("…", "Sljedeće faze",
            "Validator plana, testovi poslovnih ograničenja, planer ruta, prikaz plana po kamionima i karta ruta.",
            PhaseStatus.Planned),
    ];

    public HomeViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = HomeViewModelFactory.Create(documents, Phases);
}
