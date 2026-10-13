namespace Basera.Shared.Results;

/// <summary>
/// An expected failure: what went wrong, in words a user can act on, and optionally which input
/// (<paramref name="Target"/>, e.g. "phone") it concerns.
/// </summary>
public sealed record DomainError(ErrorCode Code, string Message, string? Target = null)
{
    public static DomainError Validation(string message, string? target = null) => new(ErrorCode.Validation, message, target);

    public static DomainError NotFound(string message) => new(ErrorCode.NotFound, message);

    public static DomainError Conflict(string message) => new(ErrorCode.Conflict, message);

    public static DomainError Forbidden(string message) => new(ErrorCode.Forbidden, message);

    public static DomainError RuleViolation(string message, string? target = null) => new(ErrorCode.RuleViolation, message, target);

    public static DomainError Internal(string message) => new(ErrorCode.Internal, message);

    public override string ToString() =>
        Target is null ? $"{Code.ToWireCode()}: {Message}" : $"{Code.ToWireCode()} ({Target}): {Message}";
}
