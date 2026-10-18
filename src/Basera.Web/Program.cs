using Basera.Shared;
using Basera.Shared.Modules;
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

builder.Services.AddControllersWithViews();
builder.Services.AddBaseraShared();
builder.Services.AddHealthChecks();
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
// UseAuthentication() goes here when Identity arrives; tenancy reads the claims it produces.
app.UseBaseraTenancy();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapModules();

app.Run();

// Lets WebApplicationFactory<Program> in the integration tests find the entry point.
public partial class Program
{
}
