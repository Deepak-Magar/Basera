using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Resident.Controllers;

/// <summary>Placeholder landing page for the Resident area; real screens live in the modules.</summary>
[Area("Resident")]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
