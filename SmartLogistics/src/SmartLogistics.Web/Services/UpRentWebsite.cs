using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace SmartLogistics.Web.Services;

/// <summary>
/// The static copy of the UpRent website (repo folder <c>web/</c>), served under <see cref="RequestPath"/>.
/// The folder is found like <c>docs/</c> and <c>data/</c> (<see cref="ProjectPaths.FindDirectory"/>), so it works in
/// development and after publish. When it is missing, <see cref="IsAvailable"/> is false: nothing is served and the
/// navigation shows the entry as unavailable instead of a broken link.
/// </summary>
public sealed class UpRentWebsite
{
    public const string FolderName = "web";
    public const string RequestPath = "/uprent";
    public const string StartPage = RequestPath + "/index.html";
    private const string StartFile = "index.html";

    private UpRentWebsite(string? root) => Root = root;

    /// <summary>Absolute path of the site folder; null when the folder (or its start page) does not exist.</summary>
    public string? Root { get; }

    public bool IsAvailable => Root is not null;

    public static UpRentWebsite FromEnvironment(IWebHostEnvironment environment)
    {
        var root = ProjectPaths.FindDirectory(environment, FolderName);
        return new UpRentWebsite(root is not null && File.Exists(Path.Combine(root, StartFile)) ? root : null);
    }

    /// <summary>
    /// File provider for the static-files middleware. Only the published part of the site is exposed
    /// (top-level <c>*.html</c> and <c>assets/**</c>, the same set the .csproj copies on publish), so sources such as
    /// <c>web/src/</c> or <c>web/build.js</c> are not served in development either. No directory listing.
    /// </summary>
    public IFileProvider CreateFileProvider() =>
        Root is null
            ? new NullFileProvider()
            : new AllowListFileProvider(new PhysicalFileProvider(Root), IsPublished);

    /// <summary>
    /// True for a top-level "*.html" or a file under "assets/" (subpath relative to the site root, as the static-files
    /// middleware passes it, e.g. "/assets/site.css"). Anything that could step outside that set is refused before it
    /// reaches the physical provider: backslashes (Kestrel decodes "%5C" to "\" but removes dot segments only around
    /// "/"), "." and ".." segments, empty segments, drive/stream separators (":") and leftover escapes ("%").
    /// </summary>
    public static bool IsPublished(string? subpath)
    {
        if (string.IsNullOrEmpty(subpath) || subpath.IndexOfAny(['\\', ':', '%', '\0']) >= 0)
        {
            return false;
        }

        var path = subpath.StartsWith('/') ? subpath[1..] : subpath;
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".."))
        {
            return false;
        }

        return segments.Length == 1
            ? path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            : segments[0].Equals("assets", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AllowListFileProvider(IFileProvider inner, Func<string, bool> isAllowed) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath) =>
            isAllowed(subpath) ? inner.GetFileInfo(subpath) : new NotFoundFileInfo(subpath);

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => inner.Watch(filter);
    }
}
