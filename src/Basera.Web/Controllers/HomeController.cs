using Basera.Shared.Authorization;
using Basera.Shared.Modules;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Controllers;

public sealed class HomeController(ModuleCatalog modules) : Controller
{
    /// <summary>The landing page; a signed-in user goes straight to their role's area.</summary>
    public IActionResult Index() => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(BaseraRoles.HomePath(User.FindFirst(BaseraClaimTypes.Role)?.Value))
        : View(modules.Modules.Select(m => m.Name).ToList());
}
