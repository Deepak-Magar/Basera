using Basera.Shared.Modules;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Controllers;

public sealed class HomeController(ModuleCatalog modules) : Controller
{
    public IActionResult Index() => View(modules.Modules.Select(m => m.Name).ToList());

    public IActionResult Error() => View();
}
