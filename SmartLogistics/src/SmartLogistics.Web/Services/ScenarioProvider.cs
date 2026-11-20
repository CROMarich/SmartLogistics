using System.Text.Json;
using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.Services;

/// <summary>
/// One selectable scenario from data/primjeri/primjeri.json: a scenario file (relative to data/) and an optional
/// working-day override, so edge cases can be shown in the demo without touching the task data.
/// </summary>
public sealed record ScenarioExample(string Id, string Title, string Description, string File, double? MaxMinutes = null);

/// <summary>
/// Loads scenarios from data/ on every request (they are small), so edits show up immediately.
/// A missing or malformed file is reported to the page instead of crashing the app.
/// </summary>
public sealed class ScenarioProvider(IWebHostEnvironment environment)
{
    public const string FileName = "scenario.json";
    private const string CatalogFile = "primjeri/primjeri.json";

    /// <summary>The task scenario; always available even when the example catalog is missing.</summary>
    public static readonly ScenarioExample Default = new("zadatak", "Zadatak", "Podaci iz zadatka (data/scenario.json).", FileName);

    private static readonly JsonSerializerOptions CatalogOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Examples in catalog order; the task scenario is always first.</summary>
    public IReadOnlyList<ScenarioExample> Examples()
    {
        var dataDirectory = ProjectPaths.FindDirectory(environment, "data");
        var catalogPath = dataDirectory is null ? null : Path.Combine(dataDirectory, CatalogFile);
        if (catalogPath is null || !File.Exists(catalogPath))
        {
            return [Default];
        }

        try
        {
            var examples = JsonSerializer.Deserialize<List<ScenarioExample>>(File.ReadAllText(catalogPath), CatalogOptions) ?? [];
            return examples.Any(e => e.Id == Default.Id) ? examples : [Default, .. examples];
        }
        catch (JsonException)
        {
            return [Default];
        }
    }

    /// <summary>Unknown or missing ids fall back to the task scenario.</summary>
    public ScenarioExample FindExample(string? id) =>
        Examples().FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>The task scenario (data/scenario.json).</summary>
    public ScenarioLoadResult Load() => Load(Default);

    public ScenarioLoadResult Load(ScenarioExample example)
    {
        var dataDirectory = ProjectPaths.FindDirectory(environment, "data");
        if (dataDirectory is null)
        {
            return ScenarioLoadResult.Failure(example, "Mapa data/ nije pronađena.");
        }

        // Only files inside data/ may be loaded, whatever the catalog says.
        var path = Path.GetFullPath(Path.Combine(dataDirectory, example.File));
        if (!path.StartsWith(Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioLoadResult.Failure(example, $"Datoteka {example.File} nije unutar mape data/.");
        }

        if (!File.Exists(path))
        {
            return ScenarioLoadResult.Failure(example, $"Datoteka data/{example.File} ne postoji.");
        }

        try
        {
            return ScenarioLoadResult.Success(example, ScenarioLoader.LoadFromFile(path), File.GetLastWriteTime(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return ScenarioLoadResult.Failure(example, $"Datoteku data/{example.File} nije moguće učitati: {ex.Message}");
        }
    }
}

/// <summary>Either a loaded scenario or an error message for the UI.</summary>
public sealed record ScenarioLoadResult(ScenarioExample Example, Scenario? Scenario, DateTime? LastModifiedLocal, string? Error)
{
    public static ScenarioLoadResult Success(ScenarioExample example, Scenario scenario, DateTime lastModified) =>
        new(example, scenario, lastModified, null);

    public static ScenarioLoadResult Failure(ScenarioExample example, string error) => new(example, null, null, error);
}
