using Microsoft.AspNetCore.Html;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>Summary card for one project document (home page and documentation navigation).</summary>
public sealed record DocumentCardViewModel(
    string Slug,
    string Title,
    string Description,
    string FileName,
    string Icon,
    bool Exists,
    string LastModifiedText,
    IReadOnlyList<string> Stats);

/// <summary>Entry in the document outline (table of contents).</summary>
public sealed record OutlineItemViewModel(string Text, string Anchor, bool IsSubsection);

/// <summary>The documentation page: header, navigation and the selected document.</summary>
public sealed record DocumentPageViewModel(
    PageHeaderViewModel Header,
    IReadOnlyList<DocumentCardViewModel> Documents,
    DocumentCardViewModel Current,
    IHtmlContent? Body,
    IReadOnlyList<OutlineItemViewModel> Outline);

/// <summary>Maps project documents to view models; all formatting happens here, not in .cshtml.</summary>
public static class DocumentViewModelFactory
{
    private static readonly Dictionary<string, string> IconBySlug = new()
    {
        ["zadatak"] = "file",
        ["spec"] = "list",
        ["odluke"] = "scale",
        ["rezultati"] = "route",
        ["vrijeme"] = "clock",
        ["ai-log"] = "robot",
        ["ui-dizajn"] = "palette",
    };

    public static DocumentCardViewModel CreateCard(ProjectDocument document, ProjectDocumentContent? content)
    {
        var icon = IconBySlug.GetValueOrDefault(document.Slug, "file");

        if (content is null)
        {
            return new DocumentCardViewModel(
                document.Slug, document.Title, document.Description, $"docs/{document.FileName}", icon,
                Exists: false, LastModifiedText: "još ne postoji", Stats: []);
        }

        return new DocumentCardViewModel(
            document.Slug, document.Title, document.Description, $"docs/{document.FileName}", icon,
            Exists: true,
            LastModifiedText: content.LastModifiedLocal.ToString("dd.MM.yyyy. HH:mm"),
            Stats: CreateStats(content.Markdown, document.DefinesRuleIds));
    }

    public static DocumentPageViewModel CreatePage(ProjectDocumentStore store, ProjectDocument current)
    {
        var cards = ProjectDocumentStore.All.Select(d => CreateCard(d, store.Read(d))).ToList();
        var currentCard = cards.Single(c => c.Slug == current.Slug);
        var header = new PageHeaderViewModel("Dokumentacija", current.Title, current.Description);
        var content = store.Read(current);

        if (content is null)
        {
            return new DocumentPageViewModel(header, cards, currentCard, Body: null, Outline: []);
        }

        var rendered = MarkdownRenderer.Render(content.Markdown);
        var outline = rendered.Headings
            .Where(h => h.Level is 2 or 3)
            .Select(h => new OutlineItemViewModel(h.Text, "#" + h.Id, IsSubsection: h.Level == 3))
            .ToList();

        return new DocumentPageViewModel(header, cards, currentCard, new HtmlString(rendered.Html), outline);
    }

    private static IReadOnlyList<string> CreateStats(string markdown, bool definesRuleIds)
    {
        var stats = new List<string>();
        if (definesRuleIds)
        {
            stats.Add($"{ProjectDocumentStore.CountDistinctIds(markdown, 'R')} pravila");
            stats.Add($"{ProjectDocumentStore.CountDistinctIds(markdown, 'D')} dvosmislenosti");
        }

        stats.Add($"{markdown.Split('\n').Length} redaka");
        return stats;
    }
}
