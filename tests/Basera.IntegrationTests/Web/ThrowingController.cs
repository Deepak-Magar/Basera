using Microsoft.AspNetCore.Mvc;

namespace Basera.IntegrationTests.Web;

/// <summary>Test-only endpoints that fail on purpose, added to the host as an application part.</summary>
[IgnoreAntiforgeryToken]
public sealed class ThrowingController : Controller
{
    [HttpGet("test/boom")]
    [HttpGet("api/test/boom")]
    public IActionResult Boom() => throw new InvalidOperationException("Boom for the test.");
}
