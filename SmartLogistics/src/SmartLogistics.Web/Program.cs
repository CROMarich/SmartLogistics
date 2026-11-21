using Microsoft.AspNetCore.Rewrite;
using SmartLogistics.Core.Planning;
using SmartLogistics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        // Croatian messages for values that cannot be bound (e.g. "abc" in a number field).
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustBeANumberAccessor(field => $"Polje {field} mora biti broj.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Vrijednost mora biti broj.");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"Vrijednost \"{value}\" nije ispravna za polje {field}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Vrijednost \"{value}\" nije ispravna.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"Vrijednost nije ispravna za polje {field}.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Vrijednost nije ispravna.");
        messages.SetValueIsInvalidAccessor(value => $"Vrijednost \"{value}\" nije ispravna.");
        messages.SetValueMustNotBeNullAccessor(field => $"Polje {field} je obavezno.");
        messages.SetMissingBindRequiredValueAccessor(field => $"Nedostaje vrijednost za polje {field}.");
        messages.SetMissingKeyOrValueAccessor(() => "Nedostaje vrijednost.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "Zahtjev nema sadržaj.");
    });
builder.Services.AddSingleton<ProjectDocumentStore>();
// Current scenario (data/scenario.json + order edits), in memory for the demo: lost on restart.
builder.Services.AddSingleton<IScenarioStore>(services =>
    InMemoryScenarioStore.FromEnvironment(services.GetRequiredService<IWebHostEnvironment>()));
// Planners are keyed by PlannerCatalog keys (the "algoritam" query value of /Plan). An algorithm of the catalog without
// a registration here is shown as "nije dostupan" instead of throwing. New planners are registered in this block only.
builder.Services.AddKeyedSingleton<IRoutePlanner, ExactDpPlanner>(PlannerCatalog.ExactKey);
builder.Services.AddKeyedSingleton<IRoutePlanner, OrToolsPlanner>(PlannerCatalog.OrToolsKey);
builder.Services.AddSingleton<PlanService>();
// "Testni scenariji": edge cases built from the original task data (never from the store), planned on demand and cached.
builder.Services.AddSingleton(services =>
    ScenarioLab.FromEnvironment(services.GetRequiredService<IWebHostEnvironment>()));
// Static copy of the UpRent website (repo folder web/), linked from the navigation; null-safe when the folder is missing.
builder.Services.AddSingleton(services =>
    UpRentWebsite.FromEnvironment(services.GetRequiredService<IWebHostEnvironment>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Addresses of earlier phases (T43 b) redirect permanently (301); the query string is kept (?nacin=voznja still works).
app.UseRewriter(new RewriteOptions()
    .AddRedirect("(?i)^UlazniPodaci/?$", "/", StatusCodes.Status301MovedPermanently)
    .AddRedirect("(?i)^(PlanRuta|Karta)/?$", "/Plan", StatusCodes.Status301MovedPermanently));

// UpRent website at /uprent/ (web/*.html and web/assets/** only); /uprent and /uprent/ open its start page.
var upRentWebsite = app.Services.GetRequiredService<UpRentWebsite>();
if (upRentWebsite.IsAvailable)
{
    app.UseRewriter(new RewriteOptions()
        .AddRedirect("(?i)^uprent/?$", UpRentWebsite.StartPage, StatusCodes.Status302Found));
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = upRentWebsite.CreateFileProvider(),
        RequestPath = UpRentWebsite.RequestPath,
    });
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
