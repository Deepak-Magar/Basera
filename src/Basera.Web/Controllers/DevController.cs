using Basera.Shared.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Basera.Web.Controllers;

/// <summary>Development tools. Every action answers 404 outside Development.</summary>
[Route("dev")]
public sealed class DevController(IHostEnvironment environment, MockSmsInbox inbox) : Controller
{
    /// <summary>The mock SMS inbox (PRODUCT_SPEC §4.8): every SMS the app "sent", newest first.</summary>
    [HttpGet("sms")]
    public IActionResult Sms() => View(inbox.Messages);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!environment.IsDevelopment())
        {
            context.Result = NotFound();
        }
    }
}
