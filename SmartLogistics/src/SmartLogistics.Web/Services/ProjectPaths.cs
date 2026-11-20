namespace SmartLogistics.Web.Services;

/// <summary>Finds repository folders (docs/, data/) both in development and after publish.</summary>
public static class ProjectPaths
{
    /// <summary>
    /// During development the live repo folder is used (src/SmartLogistics.Web -> ../../{name}),
    /// so edits are visible without a rebuild; after publish the copy next to the binaries is used.
    /// Returns null when neither exists.
    /// </summary>
    public static string? FindDirectory(IWebHostEnvironment environment, string name)
    {
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", name)),
            Path.Combine(AppContext.BaseDirectory, name),
        ];

        return candidates.FirstOrDefault(Directory.Exists);
    }
}
