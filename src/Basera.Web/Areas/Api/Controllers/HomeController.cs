using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Areas.Api.Controllers;

/// <summary>Placeholder landing page for the Api area; real screens live in the modules.</summary>
[Area("Api")]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
