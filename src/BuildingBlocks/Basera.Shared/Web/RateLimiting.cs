using System.Globalization;
using System.Threading.RateLimiting;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Web;

/// <summary>Requests allowed per window for one caller. Bound from <c>RateLimits:Api</c> and <c>RateLimits:Auth</c>.</summary>
public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Rate limits on <c>/account/*</c> POSTs (policy <see cref="AuthPolicy"/>: register, login, OTP, reset;
/// ARCHITECTURE §12, ADR-004 SMS cost control) and <c>/api/*</c>, per signed-in user or, before sign-in, per
/// client IP. Applied by path, so endpoints added later are covered without opting in. Showing the login page
/// (GET) is not limited; submitting it is. Rejections are 429 with <c>Retry-After</c>, as an envelope on /api.
/// </summary>
public static class RateLimiting
{
    /// <summary>The partition name for <c>/account/*</c> POSTs; its rule is <c>RateLimits:Auth</c>.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddBaseraRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var api = Rule(configuration, "Api", permitLimit: 120);
        var auth = Rule(configuration, "Auth", permitLimit: 10);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var (name, rule) = RuleFor(http.Request, api, auth);
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

    private static (string Name, RateLimitRule? Rule) RuleFor(HttpRequest request, RateLimitRule api, RateLimitRule auth) =>
        HttpMethods.IsPost(request.Method) && request.Path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase)
            ? (AuthPolicy, auth)
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
