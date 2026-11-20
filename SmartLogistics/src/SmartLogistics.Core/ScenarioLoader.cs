using System.Text.Json;
using SmartLogistics.Core.Domain;

namespace SmartLogistics.Core;

/// <summary>
/// Reads a <see cref="Scenario"/> from JSON (camelCase property names, see data/scenario.json).
/// Only the JSON structure is checked here: missing properties, nulls and unknown properties
/// throw <see cref="JsonException"/>. Semantic checks (duplicate IDs, unknown references,
/// non-positive weights – spec R37) belong to input validation, not to the loader.
/// </summary>
public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,              // "id": null -> error
        RespectRequiredConstructorParameters = true,    // missing "capacityKg" -> error instead of 0
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, // typo in a name -> error
    };

    public static Scenario LoadFromFile(string path) => Load(File.ReadAllText(path));

    public static Scenario Load(string json) =>
        JsonSerializer.Deserialize<Scenario>(json, Options)
        ?? throw new JsonException("Scenario JSON is empty (null).");
}
