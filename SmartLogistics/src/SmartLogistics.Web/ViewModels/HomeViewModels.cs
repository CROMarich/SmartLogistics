using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>Status of a project phase; mapped to CSS classes, an icon and a Croatian label.</summary>
public enum PhaseStatus
{
    Done,
    AwaitingApproval,

    /// <summary>The phase being worked on now.</summary>
    InProgress,
    Planned,
}

/// <summary>One step of the project timeline on the home page.</summary>
public sealed record PhaseViewModel(string Number, string Title, string Description, PhaseStatus Status)
{
    public string StatusText => Status switch
    {
        PhaseStatus.Done => "Gotovo",
        PhaseStatus.AwaitingApproval => "Čeka potvrdu",
        PhaseStatus.InProgress => "U tijeku",
        _ => "Planirano",
    };

    public string BadgeCssClass => Status switch
    {
        PhaseStatus.Done => "sl-badge sl-badge-ok",
        PhaseStatus.AwaitingApproval => "sl-badge sl-badge-warning",
        PhaseStatus.InProgress => "sl-badge sl-badge-accent",
        _ => "sl-badge sl-badge-neutral",
    };

    public string ItemCssClass => Status switch
    {
        PhaseStatus.Done => "sl-timeline-item sl-timeline-item-done",
        PhaseStatus.AwaitingApproval => "sl-timeline-item sl-timeline-item-review",
        PhaseStatus.InProgress => "sl-timeline-item sl-timeline-item-current",
        _ => "sl-timeline-item sl-timeline-item-planned",
    };

    public string Icon => Status switch
    {
        PhaseStatus.Done => "check",
        PhaseStatus.AwaitingApproval => "clock",
        PhaseStatus.InProgress => "play",
        _ => "circle",
    };
}

/// <summary>Key figure tile (label, big value, short hint).</summary>
public sealed record StatTileViewModel(string Label, string Value, string Hint, string Icon, string ToneCssClass);

/// <summary>Home page: key figures, project timeline and documents.</summary>
public sealed record HomeViewModel(
    PageHeaderViewModel Header,
    IReadOnlyList<StatTileViewModel> Stats,
    string? PendingDecisionsText,
    IReadOnlyList<PhaseViewModel> Phases,
    IReadOnlyList<DocumentCardViewModel> Documents);

/// <summary>Builds the home page view model; figures are read from the project documents, not typed in.</summary>
public static class HomeViewModelFactory
{
    public static HomeViewModel Create(ProjectDocumentStore store, IReadOnlyList<PhaseViewModel> phases)
    {
        var documents = ProjectDocumentStore.All
            .Select(d => (Document: d, Content: store.Read(d)))
            .ToList();

        var spec = documents.Single(d => d.Document.Slug == "spec").Content;
        var decisions = documents.Single(d => d.Document.Slug == "odluke").Content;

        var rules = spec is null ? 0 : ProjectDocumentStore.CountDistinctIds(spec.Markdown, 'R');
        var confirmed = decisions is null
            ? 0
            : ProjectDocumentStore.CountTableStatus(decisions.Markdown, "Potvrđeno")
              + ProjectDocumentStore.CountTableStatus(decisions.Markdown, "CLAUDE.md");
        var pending = decisions is null ? 0 : ProjectDocumentStore.CountTableStatus(decisions.Markdown, "Prijedlog");
        var donePhases = phases.Count(p => p.Status == PhaseStatus.Done);
        var existingDocuments = documents.Count(d => d.Content is not null);

        var stats = new List<StatTileViewModel>
        {
            new("Završene faze", donePhases.ToString(), $"od {phases.Count} na vremenskoj liniji", "layers", "sl-stat-tile-accent"),
            new("Pravila u specifikaciji", rules.ToString(), "R1..Rn u docs/spec.md", "list", "sl-stat-tile-accent"),
            new("Potvrđene odluke", confirmed.ToString(),
                pending == 0 ? "sve odluke su potvrđene" : $"{pending} {Format.Plural(pending, "čeka", "čekaju", "čeka")} potvrdu", "scale",
                pending == 0 ? "sl-stat-tile-ok" : "sl-stat-tile-warning"),
            new("Dokumenti", $"{existingDocuments} / {documents.Count}", "u mapi docs/", "file", "sl-stat-tile-neutral"),
        };

        var header = new PageHeaderViewModel(
            "Pregled projekta",
            "Smart Logistics Planner",
            "Dodjela naloga kamionima, redoslijed preuzimanja i isporuka te minimalna ukupna kilometraža uz obavezan povrat u bazu L0.");

        // "3 odluke čekaju potvrdu." / "1 odluka čeka potvrdu." / "5 odluka čeka potvrdu."
        var pendingText = pending == 0
            ? null
            : $"{pending} {Format.Plural(pending, "odluka", "odluke", "odluka")} {Format.Plural(pending, "čeka", "čekaju", "čeka")} potvrdu.";

        var cards = documents.Select(d => DocumentViewModelFactory.CreateCard(d.Document, d.Content)).ToList();
        return new HomeViewModel(header, stats, pendingText, phases, cards);
    }
}
