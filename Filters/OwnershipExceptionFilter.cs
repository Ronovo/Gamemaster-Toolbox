using GM_Toolbox.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GM_Toolbox.Filters;

// Turns a save refused by OwnershipRules into a message on the page the user came from.
public class OwnershipExceptionFilter(ITempDataDictionaryFactory tempData) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not OwnershipException refused)
        {
            return;
        }
        tempData.GetTempData(context.HttpContext)["OwnershipError"] = refused.Message;
        var request = context.HttpContext.Request;
        var cameFrom = Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
                       && referer.Host == request.Host.Host ? referer.PathAndQuery : "/";
        context.Result = new RedirectResult(cameFrom);
        context.ExceptionHandled = true;
    }
}
