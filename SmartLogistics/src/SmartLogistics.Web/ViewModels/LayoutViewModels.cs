namespace SmartLogistics.Web.ViewModels;

/// <summary>Header shown at the top of every page (small label, title, one-line description).</summary>
public sealed record PageHeaderViewModel(string Eyebrow, string Title, string? Subtitle = null);

/// <summary>One entry in the side navigation.</summary>
/// <param name="Page">Razor page path; null while the feature does not exist yet.</param>
public sealed record NavItemViewModel(string Title, string? Page, string Icon, bool IsActive, string? Badge)
{
    public bool IsAvailable => Page is not null;

    public string CssClass => (IsActive, IsAvailable) switch
    {
        (true, _) => "app-nav-link is-active",
        (_, false) => "app-nav-link is-disabled",
        _ => "app-nav-link",
    };
}

/// <summary>A titled group of navigation entries.</summary>
public sealed record NavSectionViewModel(string Title, IReadOnlyList<NavItemViewModel> Items);

/// <summary>
/// Side navigation of the whole app. When a phase adds a page, give its entry a page path
/// (and remove the "uskoro" badge) so it becomes clickable.
/// </summary>
public static class NavigationMenu
{
    private sealed record Entry(string Title, string? Page, string Icon, string? Badge = null);

    private static readonly (string Section, Entry[] Entries)[] Structure =
    [
        ("Projekt",
        [
            new("Pregled", "/Index", "home"),
            new("Dokumentacija", "/Dokumentacija", "book"),
            new("Pravila i provjere", "/Pravila", "check"),
        ]),
        ("Planiranje",
        [
            new("Ulazni podaci", "/UlazniPodaci", "database"),
            new("Plan ruta", null, "route", "uskoro"),
            new("Karta", null, "map", "uskoro"),
        ]),
    ];

    public static IReadOnlyList<NavSectionViewModel> Build(string? currentPage) =>
        Structure
            .Select(section => new NavSectionViewModel(
                section.Section,
                section.Entries
                    .Select(e => new NavItemViewModel(
                        e.Title, e.Page, e.Icon,
                        IsActive: e.Page is not null && string.Equals(e.Page, currentPage, StringComparison.OrdinalIgnoreCase),
                        e.Badge))
                    .ToList()))
            .ToList();
}
