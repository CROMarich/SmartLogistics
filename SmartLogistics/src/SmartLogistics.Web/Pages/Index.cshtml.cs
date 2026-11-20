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
        new("…", "Sljedeće faze",
            "Domenski model i ulazni podaci, validator, testovi poslovnih ograničenja, planer ruta, prikaz plana i karta.",
            PhaseStatus.Planned),
    ];

    public HomeViewModel ViewModel { get; private set; } = null!;

    public void OnGet()
    {
        var cards = ProjectDocumentStore.All
            .Select(d => DocumentViewModelFactory.CreateCard(d, documents.Read(d)))
            .ToList();

        ViewModel = new HomeViewModel(Phases, cards);
    }
}
