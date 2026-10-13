using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Owner.Controllers;

/// <summary>Placeholder landing page for the Owner area; real screens live in the modules.</summary>
[Area("Owner")]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
