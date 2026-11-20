using SmartLogistics.Core;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Web.Services;

/// <summary>
/// Loads data/scenario.json on every request (it is small), so edits to the file show up
/// immediately. A missing or malformed file is reported to the page instead of crashing the app.
/// </summary>
public sealed class ScenarioProvider(IWebHostEnvironment environment)
{
    public const string FileName = "scenario.json";

    public ScenarioLoadResult Load()
    {
        var dataDirectory = ProjectPaths.FindDirectory(environment, "data");
        if (dataDirectory is null)
        {
            return ScenarioLoadResult.Failure("Mapa data/ nije pronađena.");
        }

        var path = Path.Combine(dataDirectory, FileName);
        if (!File.Exists(path))
        {
            return ScenarioLoadResult.Failure($"Datoteka data/{FileName} ne postoji.");
        }

        try
        {
            return ScenarioLoadResult.Success(ScenarioLoader.LoadFromFile(path), File.GetLastWriteTime(path));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or NotSupportedException)
        {
            return ScenarioLoadResult.Failure($"Datoteku data/{FileName} nije moguće učitati: {ex.Message}");
        }
    }
}

/// <summary>Either a loaded scenario or an error message for the UI.</summary>
public sealed record ScenarioLoadResult(Scenario? Scenario, DateTime? LastModifiedLocal, string? Error)
{
    public static ScenarioLoadResult Success(Scenario scenario, DateTime lastModified) => new(scenario, lastModified, null);

    public static ScenarioLoadResult Failure(string error) => new(null, null, error);
}
