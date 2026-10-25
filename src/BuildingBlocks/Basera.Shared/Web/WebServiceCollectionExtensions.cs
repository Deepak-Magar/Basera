using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Basera.Shared.Files;
using Basera.Shared.Health;
using Basera.Shared.Localization;
using Basera.Shared.Messaging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;

namespace Basera.Shared.Web;

/// <summary>The web plumbing every area and module relies on, registered once by the host.</summary>
public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddBaseraWeb(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Caddy/nginx in front (ARCHITECTURE §4.1). Loopback proxies are trusted by default; others come from config.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddExceptionHandler<BaseraExceptionHandler>();
        services.AddBaseraRateLimiting(configuration);

        services.AddLocalization();
        // Razor escapes everything outside Basic Latin by default, turning Nepali into &#x...; entities:
        // three times the bytes on a 3G phone. Devanagari is safe to emit as is.
        services.Configure<WebEncoderOptions>(options =>
            options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Devanagari));
        services.Configure<MvcOptions>(options =>
        {
            options.ModelBinderProviders.Insert(0, new NepaliDateModelBinderProvider());
            // §12: every unsafe-method form post is checked; HTMX sends the token in a header.
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            // ADR-008: FluentValidation fills ModelState before every action.
            options.Filters.Add<FluentValidationFilter>();
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "RequestVerificationToken";
            // Secure whenever the request is HTTPS, which outside Development it always is (HSTS + redirect behind Caddy).
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        services.TryAddSingleton<MockSmsInbox>();
        services.TryAddSingleton<ISmsSender, MockSmsSender>();

        services.Configure<FileStorageOptions>(configuration.GetSection("Files"));
        services.TryAddSingleton<IFileStorage>(provider => new LocalDiskFileStorage(Path.Combine(
            environment.ContentRootPath, provider.GetRequiredService<IOptions<FileStorageOptions>>().Value.LocalRoot)));

        services.AddBaseraHealthChecks();
        return services;
    }

    /// <summary>Request localization with the Basera cultures and area defaults.</summary>
    public static IApplicationBuilder UseBaseraLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(BaseraCultures.Options());

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    /// <summary>
    /// The baseline headers of §12 that need no per-page work. A Content-Security-Policy with nonces comes
    /// later; the Hangfire dashboard's inline scripts need an exception for it.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.XFrameOptions = "DENY";
            return Task.CompletedTask;
        });
        return next(context);
    });

    /// <summary>
    /// Static files, with the PWA manifest's media type and a service worker that browsers re-check on every
    /// visit (a cached sw.js would pin users to an old cache list).
    /// </summary>
    public static IApplicationBuilder UseBaseraStaticFiles(this IApplicationBuilder app)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
        return app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                if (context.File.Name == "sw.js")
                {
                    context.Context.Response.Headers.CacheControl = "no-cache";
                }
            },
        });
    }
}
