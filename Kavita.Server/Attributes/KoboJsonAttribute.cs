using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Kavita.Server.Attributes;

/// <summary>
/// Attribute to read & write JSON as Kobo wants it
/// </summary>
public class KoboJsonAttribute: ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult result)
        {
            result.Formatters.Clear();
            result.Formatters.Add(new SystemTextJsonOutputFormatter(KoboJsonOptions.JsonSerializerOptions));
        }
    }
}
