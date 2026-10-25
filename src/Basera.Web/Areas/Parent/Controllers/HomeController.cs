using Basera.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Parent.Controllers;

/// <summary>Placeholder landing page for the Parent area; real screens live in the modules.</summary>
[Area("Parent")]
// Parents; everyone else is sent to sign in or to /account/denied.
[Authorize(Policy = BaseraPolicies.RequireParent)]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
