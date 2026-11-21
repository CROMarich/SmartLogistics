using Microsoft.AspNetCore.Mvc;
using SmartLogistics.Web.Services;

namespace SmartLogistics.Web.Pages.Orders;

/// <summary>New order; its ID (and the ID of a new machine) is the next free one (T43 d).</summary>
public class CreateModel(IScenarioStore store) : OrderFormPageModel(store)
{
    public void OnGet() => BuildForm(Store.Get(), orderId: null);

    public IActionResult OnPost() => Save(orderId: null, (order, machine) => Store.AddOrder(order, machine), "dodan");
}
