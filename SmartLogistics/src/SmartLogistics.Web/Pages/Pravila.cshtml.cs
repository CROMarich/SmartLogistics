using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages;

public class PravilaModel(ProjectDocumentStore documents, IScenarioStore store) : PageModel
{
    public RulesPageViewModel ViewModel { get; private set; } = null!;

    public void OnGet() => ViewModel = RulesViewModelFactory.Create(documents, store.GetSnapshot());
}
