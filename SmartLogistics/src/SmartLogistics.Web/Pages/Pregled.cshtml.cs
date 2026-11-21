using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class PregledModel(ProjectDocumentStore documents) : PageModel
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
        new("D", "Dokumentacija predaje",
            "README (upute, opis logike, testovi), rezultati, objašnjenje planera s mjerenjem skaliranja, AI log s osvrtom, utrošeno vrijeme i popis odluka koje čekaju potvrdu.",
            PhaseStatus.Done),
        new("5", "Neizvedivi nalozi – planer uvijek vraća plan",
            "Core/Feasibility.cs (K4/K1/K2), izmijenjeni R37 i novi R40, najmanji broj izostavljenih naloga kad radni dan ne dopušta sve; primjeri scenarija (u fazi 6b zamijenjeni uređivanjem naloga).",
            PhaseStatus.AwaitingApproval),
        new("6a", "Temelji sučelja za uređivanje i plan",
            "Spremište trenutnog scenarija u memoriji (izmjene naloga odmah mijenjaju plan), CSS klase s prefiksom sl-, nova navigacija (Planiranje / Projekt) i ViewModeli plana prijevoza s karticom ruta.",
            PhaseStatus.AwaitingApproval),
        new("6b", "Ulazni podaci i uređivanje naloga",
            "Ulazni podaci kao početna stranica (četiri tablice, matrica udaljenosti, karta naloga), stranice Nalozi s dodavanjem, izmjenom i brisanjem (i novi stroj s upisanom masom), \"Vrati izvorne podatke\", Post-Redirect-Get.",
            PhaseStatus.AwaitingApproval),
        new("6c", "Plan prijevoza s formom i kartom",
            "Forma (način radnog dana, max. minuta) dijeli se URL-om; sažetak s oznakom validatora, neizvedivi nalozi s razlogom, tablica ruta po kamionu i SVG karta s mrežom, osima, strelicama i brojevima koraka.",
            PhaseStatus.AwaitingApproval),
        new("7", "Drugi planer: OR-Tools",
            "solver-engineer: OrToolsPlanner (Google OR-Tools, preuzimanje i isporuka, heuristika s ograničenjem 5 s); na Planu prijevoza izbor algoritma, oznaka \"Optimum nije zajamčen (heuristika)\" (R38) i usporedba s egzaktnim planerom iz međuspremnika.",
            PhaseStatus.AwaitingApproval),
        new("…", "Sljedeće",
            "Odluke o prijedlozima (T3, T5–T50), čišćenje git repozitorija, demo i prezentacija.",
            PhaseStatus.Planned),
    ];

    public HomeViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = HomeViewModelFactory.Create(documents, Phases);
}
