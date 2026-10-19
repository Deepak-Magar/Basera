using System.Globalization;
using System.Threading.RateLimiting;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Web;

/// <summary>Requests allowed per window for one caller. Bound from <c>RateLimits:Api</c> and <c>RateLimits:Account</c>.</summary>
public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Rate limits on <c>/account/*</c> (login, OTP: ARCHITECTURE §12, ADR-004 SMS cost control) and <c>/api/*</c>,
/// per signed-in user or, before sign-in, per client IP. Applied by path, so endpoints added later by Identity
/// and the modules are covered without opting in. Rejections are 429 with <c>Retry-After</c>, as an envelope on /api.
/// </summary>
public static class RateLimiting
{
    public static IServiceCollection AddBaseraRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var api = Rule(configuration, "Api", permitLimit: 120);
        var account = Rule(configuration, "Account", permitLimit: 10);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var (name, rule) = RuleFor(http.Request, api, account);
                if (rule is null)
                {
                    return RateLimitPartition.GetNoLimiter("unlimited");
                }

                return RateLimitPartition.GetFixedWindowLimiter($"{name}:{PartitionKey(http)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rule.PermitLimit,
                    Window = TimeSpan.FromSeconds(rule.WindowSeconds),
                    QueueLimit = 0,
                });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var (code, message) = ApiResults.ForStatus(StatusCodes.Status429TooManyRequests);
                if (http.Request.IsApiRequest())
                {
                    await http.Response.WriteAsJsonAsync(
                        new ApiEnvelope<object>(false, null, new ApiError(code, message), http.TraceIdentifier), cancellationToken);
                }
                else
                {
                    await http.Response.WriteAsync(message, cancellationToken);
                }
            };
        });
        return services;
    }

    private static (string Name, RateLimitRule? Rule) RuleFor(HttpRequest request, RateLimitRule api, RateLimitRule account) =>
        request.Path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase) ? ("account", account)
        : request.IsApiRequest() ? ("api", api)
        : ("none", null);

    // The user when signed in (so one hostel's shared Wi-Fi does not throttle all its staff), else the IP.
    private static string PartitionKey(HttpContext http) =>
        http.User.FindFirst(BaseraClaimTypes.Subject)?.Value is { } user
            ? $"user:{user}"
            : $"ip:{http.Connection.RemoteIpAddress}";

    private static RateLimitRule Rule(IConfiguration configuration, string name, int permitLimit)
    {
        var rule = new RateLimitRule { PermitLimit = permitLimit };
        configuration.GetSection($"RateLimits:{name}").Bind(rule);
        return rule;
    }
}
