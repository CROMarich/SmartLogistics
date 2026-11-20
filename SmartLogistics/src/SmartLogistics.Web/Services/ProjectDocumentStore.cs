using System.Text.RegularExpressions;

namespace SmartLogistics.Web.Services;

/// <summary>A project document from the docs/ folder that the app can display.</summary>
/// <param name="DefinesRuleIds">True when the document defines rules (R1..Rn) and ambiguities (D1..Dn), so their counts are meaningful.</param>
public sealed record ProjectDocument(string Slug, string Title, string FileName, string Description, bool DefinesRuleIds = false);

/// <summary>Raw content of a document read from disk.</summary>
public sealed record ProjectDocumentContent(ProjectDocument Document, string Markdown, DateTime LastModifiedLocal);

/// <summary>
/// Reads project documents (docs/*.md). These are not domain data – they describe the task,
/// the specification and the AI log, so the running app shows the current state of the project.
/// </summary>
public sealed partial class ProjectDocumentStore
{
    /// <summary>Known documents, in display order.</summary>
    public static readonly IReadOnlyList<ProjectDocument> All =
    [
        new("zadatak", "Zadatak", "zadatak.md", "Izvorni tekst tehničkog zadatka s ulaznim podacima."),
        new("spec", "Specifikacija", "spec.md", "Pravila R1..Rn, dvosmislenosti, izvedene činjenice i rubni slučajevi.", DefinesRuleIds: true),
        new("odluke", "Odluke", "odluke.md", "Zapis potvrđenih pretpostavki i tehničkih odluka sa statusom i datumom."),
        new("rezultati", "Rezultati", "rezultati.md", "Rute, km i vremena za oba načina radnog dana te rezultat testova."),
        new("planer", "Planer", "planer.md", "Kako radi ExactDpPlanner: tok, stanje DP-a, optimalnost, složenost, izmjerene granice i rizična mjesta u kodu."),
        new("ai-log", "AI log", "ai-log.md", "Što je AI napravio krivo, kako je uhvaćeno i kako je popravljeno."),
        new("vrijeme", "Utrošeno vrijeme", "vrijeme.md", "Trajanje po fazama, izmjereno iz git commitova i datoteka."),
        new("ui-dizajn", "UI dizajn", "ui-dizajn.md", "Dizajn sustav: tokeni, komponente i pravila za sve stranice."),
    ];

    private readonly string? _docsDirectory;

    public ProjectDocumentStore(IWebHostEnvironment environment)
    {
        _docsDirectory = ProjectPaths.FindDirectory(environment, "docs");
    }

    public ProjectDocument? Find(string? slug) =>
        All.FirstOrDefault(d => string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the document content, or null when the file does not exist yet.</summary>
    public ProjectDocumentContent? Read(ProjectDocument document)
    {
        if (_docsDirectory is null)
        {
            return null;
        }

        var path = Path.Combine(_docsDirectory, document.FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return new ProjectDocumentContent(document, File.ReadAllText(path), File.GetLastWriteTime(path));
    }

    /// <summary>Counts distinct IDs with the given prefix, e.g. 'R' finds R1, R2, ... (used for spec statistics).</summary>
    public static int CountDistinctIds(string markdown, char prefix) =>
        IdPattern().Matches(markdown)
            .Where(m => m.Groups["prefix"].Value[0] == prefix)
            .Select(m => m.Value)
            .Distinct()
            .Count();

    /// <summary>Counts table cells whose whole content is the given status, e.g. "| Potvrđeno |" in docs/odluke.md.</summary>
    public static int CountTableStatus(string markdown, string status) =>
        Regex.Matches(markdown, $@"\|\s*{Regex.Escape(status)}\s*\|").Count;

    [GeneratedRegex(@"\b(?<prefix>[RD])\d+\b")]
    private static partial Regex IdPattern();
}
