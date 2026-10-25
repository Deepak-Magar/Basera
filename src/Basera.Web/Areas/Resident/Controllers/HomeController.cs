using Basera.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Resident.Controllers;

/// <summary>Placeholder landing page for the Resident area; real screens live in the modules.</summary>
[Area("Resident")]
// Residents; everyone else is sent to sign in or to /account/denied.
[Authorize(Policy = BaseraPolicies.RequireResident)]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
