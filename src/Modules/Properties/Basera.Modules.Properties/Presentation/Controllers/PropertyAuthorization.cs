using Basera.Modules.Properties.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Modules.Properties.Presentation.Controllers;

/// <summary>
/// The one check every Properties action makes before touching a property or one of its beds: unknown in this
/// tenant → 404; known but not this user's to see or change (<see cref="PropertyAccessRequirement"/>) → 403.
/// </summary>
internal static class PropertyAuthorization
{
    /// <summary>Null when the user may go ahead; otherwise the result to return.</summary>
    public static async Task<IActionResult?> DenyUnlessAsync(
        this Controller controller, IAuthorizationService authorization, PropertyRef? property, PropertyAccessRequirement requirement)
    {
        if (property is null)
        {
            return controller.NotFound();
        }

        var result = await authorization.AuthorizeAsync(controller.User, property, requirement);
        return result.Succeeded ? null : controller.Forbid();
    }
}
