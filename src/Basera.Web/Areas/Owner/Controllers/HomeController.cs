using Basera.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Owner.Controllers;

/// <summary>Placeholder landing page for the Owner area; real screens live in the modules.</summary>
[Area("Owner")]
// Owners and managers; everyone else is sent to sign in or to /account/denied.
[Authorize(Policy = BaseraPolicies.RequireManagement)]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
