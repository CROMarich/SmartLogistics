using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class DokumentacijaModel(ProjectDocumentStore documents) : PageModel
{
    public DocumentPageViewModel ViewModel { get; private set; } = null!;

    public IActionResult OnGet(string? slug)
    {
        // Without a slug show the specification, the most useful document for review.
        var document = slug is null ? documents.Find("spec") : documents.Find(slug);
        if (document is null)
        {
            return NotFound();
        }

        ViewModel = DocumentViewModelFactory.CreatePage(documents, document);
        return Page();
    }
}
