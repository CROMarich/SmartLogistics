namespace SmartLogistics.Web.ViewModels;

/// <summary>Header shown at the top of every page (small label, title, one-line description).</summary>
public sealed record PageHeaderViewModel(string Eyebrow, string Title, string? Subtitle = null);

/// <summary>One entry in the side navigation.</summary>
/// <param name="Page">Razor page path (rendered with <c>asp-page</c>); null for a static link or an unavailable entry.</param>
/// <param name="Href">
/// Address of a static file or an external site (rendered as a plain <c>href</c>, not through Razor routing);
/// null for Razor pages and unavailable entries.
/// </param>
/// <param name="OpensInNewTab">Static/external entries open in a new tab (<c>target="_blank" rel="noopener"</c>).</param>
public sealed record NavItemViewModel(
    string Title,
    string? Page,
    string Icon,
    bool IsActive,
    string? Badge,
    string? Href = null,
    bool OpensInNewTab = false)
{
    /// <summary>The entry links to a Razor page of this app.</summary>
    public bool IsPage => Page is not null;

    /// <summary>The entry links to a static file or an external site.</summary>
    public bool IsLink => Page is null && Href is not null;

    public bool IsAvailable => IsPage || IsLink;

    public string? Target => OpensInNewTab ? "_blank" : null;

    public string? Rel => OpensInNewTab ? "noopener" : null;

    /// <summary>Screen-reader hint appended to entries that open in a new tab.</summary>
    public string? NewTabHint => OpensInNewTab ? "(otvara se u novoj kartici)" : null;

    public string CssClass => (IsActive, IsAvailable) switch
    {
        (true, _) => "sl-nav-link sl-nav-link-active",
        (_, false) => "sl-nav-link sl-nav-link-disabled",
        _ when OpensInNewTab => "sl-nav-link sl-nav-link-external",
        _ => "sl-nav-link",
    };
}

/// <summary>A titled group of navigation entries.</summary>
public sealed record NavSectionViewModel(string Title, IReadOnlyList<NavItemViewModel> Items);

/// <summary>What the layout knows about optional link targets when it builds the navigation.</summary>
/// <param name="CurrentPage">Razor page path of the current request (route value "page").</param>
/// <param name="UpRentWebsiteHref">Start page of the static UpRent website; null when the app does not serve it.</param>
public sealed record NavigationContext(string? CurrentPage, string? UpRentWebsiteHref);

/// <summary>
/// Side navigation of the whole app (T41 a). When a phase adds a page, give its entry a page path
/// (and remove the "uskoro" badge) so it becomes clickable.
/// </summary>
public static class NavigationMenu
{
    private const string UnavailableBadge = "nedostupno";

    private enum LinkKind { Page, UpRentWebsite }

    /// <param name="Target">Razor page path for <see cref="LinkKind.Page"/>; other kinds take their address from the context.</param>
    /// <param name="AlsoActiveFor">Pages without an own entry that belong to this one (e.g. the order form pages).</param>
    private sealed record Entry(
        string Title,
        string? Target,
        string Icon,
        string? Badge = null,
        string[]? AlsoActiveFor = null,
        LinkKind Kind = LinkKind.Page);

    // Phases 6b/6c (T43): "Ulazni podaci" is the start page, "Nalozi" includes its form pages, "Pregled" moved to /Pregled.
    private static readonly (string Section, Entry[] Entries)[] Structure =
    [
        ("Planiranje",
        [
            new("Ulazni podaci", "/Index", "database"),
            new("Nalozi", "/Orders/Index", "list", AlsoActiveFor: ["/Orders/Create", "/Orders/Edit", "/Orders/Delete"]),
            new("Plan prijevoza", "/Plan", "route"),
            new("Testni scenariji", "/Scenariji", "flask"),
        ]),
        ("Projekt",
        [
            new("Pregled", "/Pregled", "home"),
            new("Dokumentacija", "/Dokumentacija", "book"),
            new("Pravila i provjere", "/Pravila", "check"),
        ]),
        ("Tvrtka",
        [
            new("UpRent web", null, "globe", Kind: LinkKind.UpRentWebsite),
        ]),
    ];

    public static IReadOnlyList<NavSectionViewModel> Build(NavigationContext context) =>
        Structure
            .Select(section => new NavSectionViewModel(
                section.Section,
                section.Entries.Select(e => ToItem(e, context)).ToList()))
            .ToList();

    private static NavItemViewModel ToItem(Entry entry, NavigationContext context) => entry.Kind switch
    {
        // A missing web/ folder must not produce a broken link: the entry stays visible but disabled.
        LinkKind.UpRentWebsite when context.UpRentWebsiteHref is null =>
            new NavItemViewModel(entry.Title, Page: null, entry.Icon, IsActive: false, UnavailableBadge),
        LinkKind.UpRentWebsite =>
            new NavItemViewModel(entry.Title, Page: null, entry.Icon, IsActive: false, entry.Badge,
                Href: context.UpRentWebsiteHref, OpensInNewTab: true),
        _ =>
            new NavItemViewModel(entry.Title, entry.Target, entry.Icon,
                IsActive: IsActive(entry, context.CurrentPage), entry.Badge),
    };

    private static bool IsActive(Entry entry, string? currentPage) =>
        currentPage is not null
        && entry.Target is not null
        && (string.Equals(entry.Target, currentPage, StringComparison.OrdinalIgnoreCase)
            || (entry.AlsoActiveFor ?? []).Any(p => string.Equals(p, currentPage, StringComparison.OrdinalIgnoreCase)));
}
