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
        (true, _) => "sl-nav-link sl-nav-link-active",
        (_, false) => "sl-nav-link sl-nav-link-disabled",
        _ => "sl-nav-link",
    };
}

/// <summary>A titled group of navigation entries.</summary>
public sealed record NavSectionViewModel(string Title, IReadOnlyList<NavItemViewModel> Items);

/// <summary>
/// Side navigation of the whole app (T41 a). When a phase adds a page, give its entry a page path
/// (and remove the "uskoro" badge) so it becomes clickable.
/// </summary>
public static class NavigationMenu
{
    /// <param name="AlsoActiveFor">Pages without an own entry that belong to this one (e.g. the order form pages).</param>
    private sealed record Entry(string Title, string? Page, string Icon, string? Badge = null, string[]? AlsoActiveFor = null);

    // Phases 6b/6c (T43): "Ulazni podaci" is the start page, "Nalozi" includes its form pages, "Pregled" moved to /Pregled.
    private static readonly (string Section, Entry[] Entries)[] Structure =
    [
        ("Planiranje",
        [
            new("Ulazni podaci", "/Index", "database"),
            new("Nalozi", "/Orders/Index", "list", AlsoActiveFor: ["/Orders/Create", "/Orders/Edit", "/Orders/Delete"]),
            new("Plan prijevoza", "/Plan", "route"),
        ]),
        ("Projekt",
        [
            new("Pregled", "/Pregled", "home"),
            new("Dokumentacija", "/Dokumentacija", "book"),
            new("Pravila i provjere", "/Pravila", "check"),
        ]),
    ];

    public static IReadOnlyList<NavSectionViewModel> Build(string? currentPage) =>
        Structure
            .Select(section => new NavSectionViewModel(
                section.Section,
                section.Entries
                    .Select(e => new NavItemViewModel(
                        e.Title, e.Page, e.Icon,
                        IsActive: IsActive(e, currentPage),
                        e.Badge))
                    .ToList()))
            .ToList();

    private static bool IsActive(Entry entry, string? currentPage) =>
        currentPage is not null
        && entry.Page is not null
        && (string.Equals(entry.Page, currentPage, StringComparison.OrdinalIgnoreCase)
            || (entry.AlsoActiveFor ?? []).Any(p => string.Equals(p, currentPage, StringComparison.OrdinalIgnoreCase)));
}
