namespace FactoryERP.Abstractions.Cqrs;

/// <summary>
/// Discriminated result type. All CQRS handlers return <see cref="Result"/> or
/// <see cref="Result{T}"/> instead of throwing exceptions for expected failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, AppError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public AppError Error { get; }

    public static Result Success() => new(true, AppError.None);
    public static Result Failure(AppError error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, AppError.None);
    public static Result<T> Failure<T>(AppError error) => new(default, false, error);
}

/// <summary>Typed result carrying a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, AppError error) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The value. Throws if result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {Error.Code}");

    public static implicit operator Result<T>(T value) => Result.Success(value);
}
