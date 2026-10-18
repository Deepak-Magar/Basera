using Microsoft.AspNetCore.Http;

namespace Basera.Shared.Users;

/// <summary><see cref="ICurrentUser"/> from the <c>sub</c> claim of the current HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(BaseraClaimTypes.Subject)?.Value, out var id) ? id : null;
}
