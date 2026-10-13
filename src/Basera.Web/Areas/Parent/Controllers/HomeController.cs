using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Parent.Controllers;

/// <summary>Placeholder landing page for the Parent area; real screens live in the modules.</summary>
[Area("Parent")]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
