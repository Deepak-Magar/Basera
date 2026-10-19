using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Jobs;

/// <summary>
/// Lets the Hangfire dashboard in only when an ASP.NET Core authorization policy succeeds. Hangfire answers
/// 401/403 itself on failure, so this works before any authentication scheme exists.
/// </summary>
public sealed class PolicyDashboardAuthorizationFilter(string policy) : IDashboardAsyncAuthorizationFilter
{
    public async Task<bool> AuthorizeAsync(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(http.User, http, policy)).Succeeded;
    }
}
