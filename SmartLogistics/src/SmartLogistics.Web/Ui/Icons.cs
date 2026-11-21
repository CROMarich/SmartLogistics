using Microsoft.AspNetCore.Html;

namespace SmartLogistics.Web.Ui;

/// <summary>
/// Inline SVG icon set (24x24, stroke based). No icon fonts or CDNs: every icon is plain markup
/// styled by the <c>.sl-icon</c> class in site.css (colour follows the surrounding text).
/// Usage in Razor: <c>@Icons.Get("home")</c>.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["home"] = """<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V21h14V9.5"/><path d="M10 21v-6h4v6"/>""",
        ["book"] = """<path d="M4 19.5V5a2 2 0 0 1 2-2h14v16H6a2 2 0 0 0-2 2v0a2 2 0 0 0 2 2h14"/><path d="M8 7h8M8 11h6"/>""",
        ["file"] = """<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h4"/>""",
        ["list"] = """<path d="M9 6h11M9 12h11M9 18h11"/><path d="M4 6h.01M4 12h.01M4 18h.01"/>""",
        ["scale"] = """<path d="M12 3v18M7 21h10M5 7h14"/><path d="m5 7-3 7a3 3 0 0 0 6 0zM19 7l-3 7a3 3 0 0 0 6 0z"/>""",
        ["robot"] = """<rect x="4" y="8" width="16" height="12" rx="2"/><path d="M12 4v4M9 13h.01M15 13h.01M9 17h6"/>""",
        ["palette"] = """<path d="M12 21a9 9 0 1 1 9-9c0 2-1.5 3-3.5 3H15a2 2 0 0 0-1.5 3.3A1.6 1.6 0 0 1 12 21z"/><path d="M7.5 11h.01M10 7.5h.01M14.5 7.5h.01"/>""",
        ["database"] = """<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>""",
        ["route"] = """<circle cx="6" cy="19" r="2"/><circle cx="18" cy="5" r="2"/><path d="M8 19h8.5a3.5 3.5 0 0 0 0-7h-9a3.5 3.5 0 0 1 0-7H16"/>""",
        ["map"] = """<path d="M9 4 3 6v14l6-2 6 2 6-2V4l-6 2z"/><path d="M9 4v14M15 6v14"/>""",
        ["truck"] = """<path d="M3 6h11v10H3z"/><path d="M14 9h4l3 3v4h-7"/><circle cx="7" cy="17.5" r="1.5"/><circle cx="17" cy="17.5" r="1.5"/>""",
        ["check"] = """<path d="m5 12.5 4.5 4.5L19 7.5"/>""",
        ["clock"] = """<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>""",
        ["circle"] = """<circle cx="12" cy="12" r="4"/>""",
        ["alert"] = """<path d="M12 3 2 20h20z"/><path d="M12 10v4M12 17h.01"/>""",
        ["info"] = """<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/>""",
        ["menu"] = """<path d="M4 6h16M4 12h16M4 18h16"/>""",
        ["arrow-right"] = """<path d="M5 12h14M13 6l6 6-6 6"/>""",
        ["layers"] = """<path d="m12 3 9 5-9 5-9-5z"/><path d="m3 13 9 5 9-5"/>""",
        ["edit"] = """<path d="M4 20h4L19 9l-4-4L4 16z"/><path d="m13.5 6.5 4 4"/>""",
        ["trash"] = """<path d="M4 7h16M10 11v6M14 11v6"/><path d="M6 7l1 13h10l1-13M9 7V4h6v3"/>""",
        ["reset"] = """<path d="M4 4v5h5"/><path d="M4.5 9A8 8 0 1 1 4 13"/>""",
        ["plus"] = """<path d="M12 5v14M5 12h14"/>""",
        ["play"] = """<path d="M7 5v14l11-7z"/>""",
        ["external"] = """<path d="M14 4h6v6"/><path d="M20 4 11 13"/><path d="M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/>""",
        ["globe"] = """<circle cx="12" cy="12" r="9"/><path d="M3 12h18"/><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z"/>""",
        ["flask"] = """<path d="M9 3h6M10 3v6L4.5 18.5A1.7 1.7 0 0 0 6 21h12a1.7 1.7 0 0 0 1.5-2.5L14 9V3"/><path d="M7 15h10"/>""",
    };

    /// <summary>Returns the SVG markup for the icon; unknown names render an empty placeholder of the same size.</summary>
    public static IHtmlContent Get(string name, string? extraCssClass = null)
    {
        var css = extraCssClass is null ? "sl-icon" : $"sl-icon {extraCssClass}";
        var paths = Paths.GetValueOrDefault(name, string.Empty);
        return new HtmlString($"""<svg class="{css}" viewBox="0 0 24 24" aria-hidden="true" focusable="false">{paths}</svg>""");
    }
}
