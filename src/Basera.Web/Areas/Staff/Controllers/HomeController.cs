using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Staff.Controllers;

/// <summary>Placeholder landing page for the Staff area; real screens live in the modules.</summary>
[Area("Staff")]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
