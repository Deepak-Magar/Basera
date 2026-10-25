using Basera.Shared;
using Basera.Shared.DataProtection;
using Basera.Shared.Health;
using Basera.Shared.Jobs;
using Basera.Shared.Modules;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Basera.Shared.Users;
using Basera.Shared.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// In Development, CreateBuilder also loads user secrets (UserSecretsId in Basera.Web.csproj), after
// appsettings.Development.json and before environment variables and command-line arguments. That is
// where the real "ConnectionStrings:Default" lives; the file only holds a placeholder password.
// Calling AddUserSecrets again here would move secrets above environment variables, so we don't.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
builder.Services.AddBaseraShared();
builder.Services.AddBaseraWeb(builder.Configuration, builder.Environment);
builder.Services.AddBaseraDataProtection(builder.Configuration);
builder.Services.AddBaseraOutbox(builder.Configuration);
builder.Services.AddBaseraJobs(builder.Configuration);
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

// Middleware order: ARCHITECTURE §4, except forwarded headers run first (PROGRESS.md, "Middleware order"),
// so the scheme, client IP and everything after them see the real request.
app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error/{0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseBaseraStaticFiles();
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostics, http) =>
{
    diagnostics.Set("CorrelationId", http.TraceIdentifier);
    diagnostics.Set("TenantId", http.RequestServices.GetRequiredService<ITenantContext>().TenantId);
    diagnostics.Set("UserId", http.RequestServices.GetRequiredService<ICurrentUser>().UserId);
});
app.UseRouting();
app.UseRateLimiter();
// The Identity module registers the cookie scheme; tenancy reads the claims authentication produces.
app.UseAuthentication();
app.UseBaseraTenancy();
app.UseAuthorization();
app.UseBaseraLocalization();

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = BaseraHealthChecks.WriteResponse });
app.MapGet("/api/ping", (HttpContext http) => ApiResults.Ok(new { status = "ok" }, http));
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapBaseraJobs();
app.MapModules();

app.Run();

// Lets WebApplicationFactory<Program> in the integration tests find the entry point.
public partial class Program
{
}
