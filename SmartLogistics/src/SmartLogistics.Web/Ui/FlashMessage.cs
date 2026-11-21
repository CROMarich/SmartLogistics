using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace SmartLogistics.Web.Ui;

/// <summary>
/// Success message that survives one Post-Redirect-Get (TempData); shown once by the _FlashMessage partial
/// as "sl-callout sl-callout-ok".
/// </summary>
public static class FlashMessage
{
    public const string Key = "sl-flash";

    public static void Set(ITempDataDictionary tempData, string message)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        tempData[Key] = message;
    }

    /// <summary>Reads and removes the message (null when there is none).</summary>
    public static string? Take(ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        return tempData[Key] as string;
    }
}
