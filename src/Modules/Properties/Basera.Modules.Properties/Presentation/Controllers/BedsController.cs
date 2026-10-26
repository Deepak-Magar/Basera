using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Modules.Properties.Presentation.Controllers;

/// <summary>One bed: where it is, its status, rate and history. Authorized through the bed's property.</summary>
[Area("Owner")]
[Route("owner/beds")]
[Authorize(Policy = BaseraPolicies.RequireManagement)]
public sealed class BedsController(BedService beds, PropertyStore store, IAuthorizationService authorization) : Controller
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id, CancellationToken cancellationToken)
    {
        var property = await store.FindRefOfBedAsync(id, cancellationToken);
        if (await this.DenyUnlessAsync(authorization, property, PropertyAccessRequirement.View) is { } denied)
        {
            return denied;
        }

        return View((await beds.GetAsync(id, cancellationToken))!);
    }
}
