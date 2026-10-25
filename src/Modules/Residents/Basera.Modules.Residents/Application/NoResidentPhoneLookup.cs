using Basera.Modules.Residents.Contracts;
using Basera.Shared.Domain;

namespace Basera.Modules.Residents.Application;

/// <summary>
/// Stub until the Residents module stores residents and guardians: nobody is found, so OTP sign-in is
/// refused for every number that doesn't already have a user. Replaced when Residents gets its schema.
/// </summary>
internal sealed class NoResidentPhoneLookup : IResidentPhoneLookup
{
    public Task<PhoneHolder?> FindAsync(PhoneNumber phone, CancellationToken cancellationToken = default) =>
        Task.FromResult<PhoneHolder?>(null);
}
