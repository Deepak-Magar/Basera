namespace Basera.Shared.Results;

/// <summary>The kinds of expected failure. Each maps to one wire code in API envelopes and one HTTP status.</summary>
public enum ErrorCode
{
    /// <summary>Input is malformed or incomplete. Wire code VALIDATION_ERROR.</summary>
    Validation,

    /// <summary>The thing asked for does not exist (or is not visible to this tenant). Wire code NOT_FOUND.</summary>
    NotFound,

    /// <summary>The request clashes with current state, e.g. a duplicate or a stale edit. Wire code CONFLICT.</summary>
    Conflict,

    /// <summary>The user may not do this. Wire code FORBIDDEN.</summary>
    Forbidden,

    /// <summary>Input is well-formed but a business rule says no, e.g. bed already occupied. Wire code RULE_VIOLATION.</summary>
    RuleViolation,

    /// <summary>Something failed that the user cannot fix. Wire code INTERNAL.</summary>
    Internal,
}

public static class ErrorCodeExtensions
{
    public static string ToWireCode(this ErrorCode code) => code switch
    {
        ErrorCode.Validation => "VALIDATION_ERROR",
        ErrorCode.NotFound => "NOT_FOUND",
        ErrorCode.Conflict => "CONFLICT",
        ErrorCode.Forbidden => "FORBIDDEN",
        ErrorCode.RuleViolation => "RULE_VIOLATION",
        ErrorCode.Internal => "INTERNAL",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
