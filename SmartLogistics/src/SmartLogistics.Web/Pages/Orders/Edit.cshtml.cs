using Microsoft.AspNetCore.Mvc;
using SmartLogistics.Web.Services;
using SmartLogistics.Web.ViewModels;

namespace SmartLogistics.Web.Pages.Orders;

/// <summary>Edit an order (/Orders/Edit/N01); the ID stays, machine and locations change.</summary>
public class EditModel(IScenarioStore store) : OrderFormPageModel(store)
{
    public IActionResult OnGet(string id)
    {
        var scenario = Store.Get();
        BuildForm(scenario, id);
        if (OrderViewModelFactory.ToInput(scenario, id) is not { } input)
        {
            return OrderNotFound(id);
        }

        Input = input;
        return Page();
    }

    public IActionResult OnPost(string id) => Save(id, (order, machine) => Store.UpdateOrder(order, machine), "izmijenjen");
}
