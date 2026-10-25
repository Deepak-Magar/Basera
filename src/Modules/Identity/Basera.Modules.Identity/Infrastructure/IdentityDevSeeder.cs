using Basera.Modules.Identity.Domain;
using Basera.Shared.Domain;
using Basera.Shared.Localization;
using Basera.Shared.Modules;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// The demo hostel for local development: "Sunrise Boys Hostel, Kirtipur" with an owner, a warden and a
/// resident whose phone signs in by OTP (code on <c>/dev/sms</c>). Skipped when the owner already exists.
/// </summary>
internal sealed class IdentityDevSeeder : IDevSeeder
{
    public const string HostelName = "Sunrise Boys Hostel, Kirtipur";
    public const string OwnerEmail = "owner@basera.local";
    public const string WardenEmail = "warden@basera.local";
    public const string Password = "Passw0rd!";
    public const string OwnerPhone = "9800000000";
    public const string ResidentPhone = "9800000001";
    public const string WardenPhone = "9800000002";

    public int Order => 0;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILogger<IdentityDevSeeder>>();
        if (await services.GetRequiredService<SignInLookup>().FindAsync(OwnerEmail, cancellationToken) is not null)
        {
            logger.LogInformation("Dev seed: {Hostel} already exists, nothing to do.", HostelName);
            return;
        }

        var db = services.GetRequiredService<IdentityDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var today = services.GetRequiredService<IClock>().Today;

        var tenant = Tenant.Register(HostelName, Phone(OwnerPhone), today).Value;
        tenant.UpdateLegalDetails("Sunrise Boys Hostel Pvt. Ltd.", "609876543", "Kirtipur-3, Kathmandu");
        services.GetRequiredService<TenantContext>().Set(tenant.Id);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        await AddUserAsync("Sunil Maharjan", OwnerPhone, OwnerEmail, Password, Role.Owner, BaseraCultures.English);
        await AddUserAsync("Bikash Tamang", WardenPhone, WardenEmail, Password, Role.Warden, null);
        await AddUserAsync("Ram Thapa", ResidentPhone, null, null, Role.Resident, null);
        logger.LogInformation(
            "Dev seed: created {Hostel}. Owner {Owner} / {Password}, warden {Warden} / {Password}, resident OTP on {Resident}.",
            HostelName, OwnerEmail, Password, WardenEmail, Password, Phone(ResidentPhone).E164);

        async Task AddUserAsync(string name, string phone, string? email, string? password, Role role, string? language)
        {
            var e164 = Phone(phone).E164;
            var user = new ApplicationUser
            {
                UserName = e164,
                PhoneNumber = e164,
                PhoneNumberConfirmed = true,
                PhoneVerifiedAt = now,
                Email = email,
                EmailConfirmed = email is not null,
                DisplayName = name,
                PreferredLanguage = language,
            };
            var created = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Dev seed: {name}: {string.Join(" ", created.Errors.Select(e => e.Description))}");
            }

            db.Memberships.Add(new TenantMembership(user.Id, role));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static PhoneNumber Phone(string national) => PhoneNumber.Create(national).Value;
}
