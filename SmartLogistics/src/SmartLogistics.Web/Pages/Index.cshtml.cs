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
        new("3", "Validator i testovi",
            "Paralelno: validator-engineer piše PlanValidator iz spec.md, test-engineer pozitivne, negativne i referentne testove.",
            PhaseStatus.AwaitingApproval),
        new("4", "Egzaktni planer",
            "solver-engineer: ExactDpPlanner – sve raspodjele naloga (2^k) i DP po kamionu (lokacija + status naloga u bazi 3); Total 200,1 km, DrivingOnly 182,2 km.",
            PhaseStatus.AwaitingApproval),
        new("5", "Dokumentacija predaje",
            "README (upute, opis logike, testovi), rezultati, AI log s osvrtom, utrošeno vrijeme i popis odluka koje čekaju potvrdu.",
            PhaseStatus.Done),
        new("…", "Sljedeće",
            "Odluke o prijedlozima T3, T5–T30, čišćenje git repozitorija, demo i prezentacija.",
            PhaseStatus.Planned),
    ];

    public HomeViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = HomeViewModelFactory.Create(documents, Phases);
}
