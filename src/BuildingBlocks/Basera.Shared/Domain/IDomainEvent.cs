namespace Basera.Shared.Domain;

/// <summary>
/// Something that happened in the domain, named in the past tense (<c>ResidentCheckedIn</c>). Raised by an
/// <see cref="Entity"/> and handled after the change is committed (ADR-011). Events are immutable records of
/// plain data (ids, values), never entities, because reliable ones are stored as JSON and read back later.
/// </summary>
public interface IDomainEvent
{
}
