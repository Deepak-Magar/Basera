namespace Basera.Shared.Domain;

/// <summary>
/// Who created and last changed a row, and when. Set by <c>AuditInterceptor</c> on save; entities expose
/// the properties with private setters. A null user means the system (a background job).
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid? CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    Guid? UpdatedBy { get; }
}
