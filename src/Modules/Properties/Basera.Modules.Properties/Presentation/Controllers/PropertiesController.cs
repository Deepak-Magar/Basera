using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Authorization;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Modules.Properties.Presentation.Controllers;

public sealed record PropertyOverviewViewModel(Property Property, DateOnly Today, bool CanManage);

/// <summary>
/// The owner's properties (PRODUCT_SPEC §5 "Properties"). Owner and Manager reach the area; which properties a
/// Manager sees is decided per property by <see cref="PropertyAccessRequirement"/>.
/// </summary>
[Area("Owner")]
[Route("owner/properties")]
[Authorize(Policy = BaseraPolicies.RequireManagement)]
public sealed class PropertiesController(
    PropertyService properties, PropertyStore store, PropertyAccess access, IAuthorizationService authorization, IClock clock)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var visible = await access.VisiblePropertyIdsAsync(User, cancellationToken);
        return View(await properties.ListAsync(visible, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Show(Guid id, CancellationToken cancellationToken)
    {
        var property = await store.FindRefAsync(id, cancellationToken);
        if (await this.DenyUnlessAsync(authorization, property, PropertyAccessRequirement.View) is { } denied)
        {
            return denied;
        }

        var aggregate = (await properties.FindAsync(id, cancellationToken))!;
        var canManage = (await authorization.AuthorizeAsync(User, property, PropertyAccessRequirement.Manage)).Succeeded;
        return View(new PropertyOverviewViewModel(aggregate, clock.Today, canManage));
    }
}
