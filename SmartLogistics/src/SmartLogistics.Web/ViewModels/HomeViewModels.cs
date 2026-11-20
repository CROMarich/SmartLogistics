namespace SmartLogistics.Web.ViewModels;

/// <summary>Status of a project phase; mapped to a CSS class and a Croatian label.</summary>
public enum PhaseStatus
{
    Done,
    AwaitingApproval,
    Planned,
}

/// <summary>One row of the project progress table on the home page.</summary>
public sealed record PhaseViewModel(string Number, string Title, string Description, PhaseStatus Status)
{
    public string StatusText => Status switch
    {
        PhaseStatus.Done => "Gotovo",
        PhaseStatus.AwaitingApproval => "Čeka potvrdu",
        _ => "Planirano",
    };

    public string StatusCssClass => Status switch
    {
        PhaseStatus.Done => "status-badge status-done",
        PhaseStatus.AwaitingApproval => "status-badge status-review",
        _ => "status-badge status-planned",
    };
}

/// <summary>Home page: project progress and available documents.</summary>
public sealed record HomeViewModel(IReadOnlyList<PhaseViewModel> Phases, IReadOnlyList<DocumentCardViewModel> Documents);
