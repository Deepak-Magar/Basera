using Basera.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Staff.Controllers;

/// <summary>Placeholder landing page for the Staff area; real screens live in the modules.</summary>
[Area("Staff")]
// Owner, manager, warden and cook; everyone else is sent to sign in or to /account/denied.
[Authorize(Policy = BaseraPolicies.RequireStaff)]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
