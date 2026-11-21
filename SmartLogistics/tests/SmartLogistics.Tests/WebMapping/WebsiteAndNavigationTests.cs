using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Tests.WebMapping;

/// <summary>
/// The static UpRent website under /uprent/ (only top-level *.html and assets/** may be served, also against encoded
/// path tricks), the navigation entries that link to it and to the test scenarios, and the computed project phases.
/// </summary>
public class WebsiteAndNavigationTests
{
    // ---------- which subpaths are published ----------

    [Theory]
    [InlineData("/index.html")]
    [InlineData("/kontakt.html")]
    [InlineData("index.html")]
    [InlineData("/assets/site.css")]
    [InlineData("/assets/strojevi/17630.jpg")]
    [InlineData("/ASSETS/site.js")]
    public void Published_part_of_the_site_is_allowed(string subpath) =>
        Assert.True(UpRentWebsite.IsPublished(subpath));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/build.js")]
    [InlineData("/src/pages/index.html")]
    [InlineData("/assets")]
    [InlineData("/assets/")]
    [InlineData("/assets/../build.js")]
    [InlineData("/assets/./site.css")]
    [InlineData("/assets\\..\\build.js")] // Kestrel decodes %5C to '\' but removes dot segments only around '/'
    [InlineData("/assets/%2e%2e/build.js")]
    [InlineData("//index.html")]
    [InlineData("/c:/index.html")]
    [InlineData("/index.html::$DATA")]
    [InlineData("/index.html\0.css")]
    public void Anything_outside_html_and_assets_or_with_path_tricks_is_refused(string? subpath) =>
        Assert.False(UpRentWebsite.IsPublished(subpath));

    [Fact]
    public void File_provider_serves_pages_and_assets_but_not_sources_or_listings()
    {
        var root = Directory.CreateTempSubdirectory("uprent-test-");
        try
        {
            var web = Directory.CreateDirectory(Path.Combine(root.FullName, "web"));
            File.WriteAllText(Path.Combine(web.FullName, "index.html"), "<h1>UpRent</h1>");
            File.WriteAllText(Path.Combine(web.FullName, "build.js"), "secret");
            Directory.CreateDirectory(Path.Combine(web.FullName, "assets"));
            File.WriteAllText(Path.Combine(web.FullName, "assets", "site.css"), "body{}");
            Directory.CreateDirectory(Path.Combine(web.FullName, "src"));
            File.WriteAllText(Path.Combine(web.FullName, "src", "page.html"), "source");
            var contentRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "SmartLogistics.Web"));

            var site = UpRentWebsite.FromEnvironment(new FakeEnvironment(contentRoot.FullName));
            Assert.True(site.IsAvailable);
            var provider = site.CreateFileProvider();

            Assert.True(provider.GetFileInfo("/index.html").Exists);
            Assert.True(provider.GetFileInfo("/assets/site.css").Exists);
            Assert.False(provider.GetFileInfo("/build.js").Exists);
            Assert.False(provider.GetFileInfo("/assets\\..\\build.js").Exists);
            Assert.False(provider.GetFileInfo("/src/page.html").Exists);
            Assert.False(provider.GetDirectoryContents("/").Exists);
            Assert.False(provider.GetDirectoryContents("/assets").Exists);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    // ---------- navigation ----------

    [Fact]
    public void UpRent_entry_opens_the_site_in_a_new_tab_when_it_is_served()
    {
        var item = FindItem(NavigationMenu.Build(new NavigationContext("/Plan", "/uprent/index.html")), "UpRent web");

        Assert.True(item.IsLink);
        Assert.Equal("/uprent/index.html", item.Href);
        Assert.Equal("_blank", item.Target);
        Assert.Equal("noopener", item.Rel);
        Assert.NotNull(item.NewTabHint);
    }

    [Fact]
    public void UpRent_entry_is_disabled_instead_of_a_broken_link_when_the_site_is_missing()
    {
        var item = FindItem(NavigationMenu.Build(new NavigationContext("/Plan", null)), "UpRent web");

        Assert.False(item.IsAvailable);
        Assert.Null(item.Href);
        Assert.Contains("sl-nav-link-disabled", item.CssClass, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(item.Badge));
    }

    [Fact]
    public void Test_scenarios_entry_is_a_page_in_the_planning_section_and_active_on_its_page()
    {
        var sections = NavigationMenu.Build(new NavigationContext("/Scenariji", null));
        var planning = Assert.Single(sections, s => s.Title == "Planiranje");
        var item = Assert.Single(planning.Items, i => i.Title == "Testni scenariji");

        Assert.Equal("/Scenariji", item.Page);
        Assert.True(item.IsActive);
        Assert.Single(sections.SelectMany(s => s.Items), i => i.IsActive);
    }

    // ---------- project phases ----------

    [Fact]
    public void Exact_planner_phase_shows_the_computed_optimum_not_a_typed_in_one()
    {
        var phase = Assert.Single(ProjectPhases.Create(new ComputedOptima(123.4, 99.9)), p => p.Number == "4");

        Assert.Contains("123,4 km", phase.Description, StringComparison.Ordinal);
        Assert.Contains("99,9 km", phase.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("200,1", phase.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_planner_phase_has_no_numbers_when_the_optimum_could_not_be_computed()
    {
        var phase = Assert.Single(ProjectPhases.Create(new ComputedOptima(null, null)), p => p.Number == "4");

        Assert.DoesNotContain(" km", phase.Description, StringComparison.Ordinal);
    }

    private static NavItemViewModel FindItem(IReadOnlyList<NavSectionViewModel> sections, string title) =>
        Assert.Single(sections.SelectMany(s => s.Items), i => i.Title == title);

    private sealed class FakeEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmartLogistics.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
    }
}
