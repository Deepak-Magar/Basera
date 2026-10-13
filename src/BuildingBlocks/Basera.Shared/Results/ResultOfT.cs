namespace Basera.Shared.Results;

/// <summary>A <see cref="Result"/> that carries a value when it succeeds.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(DomainError error)
        : base(error)
    {
    }

    /// <summary>The value. Reading it from a failed result is a bug and throws.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error}).");

    public static Result<T> Success(T value) =>
        value is null ? throw new ArgumentNullException(nameof(value)) : new Result<T>(value);

    public static new Result<T> Failure(DomainError error) =>
        new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(DomainError error) => Failure(error);

    /// <summary>Transforms the value of a success; a failure passes through unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error!);

    /// <summary>Runs the next step only if this one succeeded.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        IsSuccess ? next(_value!) : Result<TOut>.Failure(Error!);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);
}
