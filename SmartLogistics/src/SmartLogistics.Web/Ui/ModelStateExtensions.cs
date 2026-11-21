using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SmartLogistics.Web.Ui;

public static class ModelStateExtensions
{
    /// <summary>Errors of the whole form (key ""), e.g. a failed store operation, for the _FormErrors partial.</summary>
    public static IReadOnlyList<string> FormErrors(this ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        return modelState.TryGetValue(string.Empty, out var entry)
            ? entry.Errors.Select(e => e.ErrorMessage).ToList()
            : [];
    }

    /// <summary>First error of one field (e.g. "maxMinuta"), or null.</summary>
    public static string? FirstError(this ModelStateDictionary modelState, string key)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        return modelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0 ? entry.Errors[0].ErrorMessage : null;
    }
}
