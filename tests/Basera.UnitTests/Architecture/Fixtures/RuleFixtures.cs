// Deliberately good and bad code used only by RuleSelfTests to prove the rules can fail.
// Nothing here is part of Basera.

namespace Basera.UnitTests.Architecture.Fixtures.FakeModule.Contracts
{
    public sealed class PublicLookup;
}

namespace Basera.UnitTests.Architecture.Fixtures.FakeModule.Domain
{
    public sealed class InternalEntity;

    public sealed class LeakyEntity
    {
        // A domain type must not know about HTTP.
        public Microsoft.AspNetCore.Http.HttpContext? Context { get; set; }
    }
}

namespace Basera.UnitTests.Architecture.Fixtures.Consumers.Good
{
    public sealed class UsesContracts
    {
        public FakeModule.Contracts.PublicLookup? Lookup { get; set; }
    }
}

namespace Basera.UnitTests.Architecture.Fixtures.Consumers.Bad
{
    public sealed class UsesInternals
    {
        public FakeModule.Domain.InternalEntity? Entity { get; set; }
    }
}
