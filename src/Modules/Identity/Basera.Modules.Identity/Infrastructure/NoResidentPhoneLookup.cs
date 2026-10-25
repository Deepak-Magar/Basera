using Basera.Modules.Identity.Contracts;
using Basera.Shared.Domain;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// Stub until the Residents module implements <see cref="IResidentPhoneLookup"/> (Session 3): nobody is found,
/// so OTP sign-in is refused for every number that doesn't already have a user.
/// </summary>
internal sealed class NoResidentPhoneLookup : IResidentPhoneLookup
{
    public Task<PhoneHolder?> FindAsync(PhoneNumber phone, CancellationToken cancellationToken = default) =>
        Task.FromResult<PhoneHolder?>(null);
}
