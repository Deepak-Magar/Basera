namespace Basera.Shared.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way. Services return a failed result for
/// things a user can cause (bad input, rule broken, not found) and throw only for bugs (ARCHITECTURE §5).
/// </summary>
public class Result
{
    private static readonly Result SuccessInstance = new(null);

    protected Result(DomainError? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure, or <c>null</c> when the operation succeeded.</summary>
    public DomainError? Error { get; }

    public static Result Success() => SuccessInstance;

    public static Result Failure(DomainError error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result(DomainError error) => Failure(error);

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}
