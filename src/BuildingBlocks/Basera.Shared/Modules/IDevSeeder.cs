namespace Basera.Shared.Modules;

/// <summary>
/// Demo data for local development (ARCHITECTURE §6: a DevSeeder run by <c>scripts/dev.ps1</c>, never in
/// migrations). A module registers one in <c>AddModule</c>; <c>dotnet run --project src/Basera.Web -- seed</c>
/// runs them all in <see cref="Order"/>, each in its own DI scope. Seeding must be idempotent: running it
/// twice leaves the same data.
/// </summary>
public interface IDevSeeder
{
    /// <summary>Lower runs first. Identity (tenants, users) is 0; modules that need a tenant come after.</summary>
    int Order { get; }

    Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}
