using Microsoft.AspNetCore.Html;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.ViewModels;

/// <summary>Summary card for one project document (home page and documentation navigation).</summary>
public sealed record DocumentCardViewModel(
    string Slug,
    string Title,
    string Description,
    string FileName,
    bool Exists,
    string LastModifiedText,
    IReadOnlyList<string> Stats);

/// <summary>Entry in the document outline (table of contents).</summary>
public sealed record OutlineItemViewModel(string Text, string Anchor, bool IsSubsection);

/// <summary>The documentation page: navigation plus the selected document.</summary>
public sealed record DocumentPageViewModel(
    IReadOnlyList<DocumentCardViewModel> Documents,
    DocumentCardViewModel Current,
    IHtmlContent? Body,
    IReadOnlyList<OutlineItemViewModel> Outline);

/// <summary>Maps project documents to view models; all formatting happens here, not in .cshtml.</summary>
public static class DocumentViewModelFactory
{
    public static DocumentCardViewModel CreateCard(ProjectDocument document, ProjectDocumentContent? content)
    {
        if (content is null)
        {
            return new DocumentCardViewModel(
                document.Slug, document.Title, document.Description, $"docs/{document.FileName}",
                Exists: false, LastModifiedText: "još ne postoji", Stats: []);
        }

        return new DocumentCardViewModel(
            document.Slug, document.Title, document.Description, $"docs/{document.FileName}",
            Exists: true,
            LastModifiedText: content.LastModifiedLocal.ToString("dd.MM.yyyy. HH:mm"),
            Stats: CreateStats(content.Markdown));
    }

    public static DocumentPageViewModel CreatePage(ProjectDocumentStore store, ProjectDocument current)
    {
        var cards = ProjectDocumentStore.All.Select(d => CreateCard(d, store.Read(d))).ToList();
        var currentCard = cards.Single(c => c.Slug == current.Slug);
        var content = store.Read(current);

        if (content is null)
        {
            return new DocumentPageViewModel(cards, currentCard, Body: null, Outline: []);
        }

        var rendered = MarkdownRenderer.Render(content.Markdown);
        var outline = rendered.Headings
            .Where(h => h.Level is 2 or 3)
            .Select(h => new OutlineItemViewModel(h.Text, "#" + h.Id, IsSubsection: h.Level == 3))
            .ToList();

        return new DocumentPageViewModel(cards, currentCard, new HtmlString(rendered.Html), outline);
    }

    private static IReadOnlyList<string> CreateStats(string markdown)
    {
        var stats = new List<string>();
        var rules = ProjectDocumentStore.CountDistinctIds(markdown, 'R');
        var ambiguities = ProjectDocumentStore.CountDistinctIds(markdown, 'D');

        if (rules > 0)
        {
            stats.Add($"{rules} pravila");
        }

        if (ambiguities > 0)
        {
            stats.Add($"{ambiguities} dvosmislenosti");
        }

        var lineCount = markdown.Split('\n').Length;
        stats.Add($"{lineCount} redaka");
        return stats;
    }
}
