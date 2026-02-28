using FactoryERP.Abstractions.Cqrs;
using FluentValidation;
using MediatR;

namespace FactoryERP.Abstractions.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs all registered FluentValidation validators
/// for the current request. Failures are collected and returned as <see cref="Result.Failure"/>
/// with a "Validation" error code — no exceptions are thrown.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        // Build combined error message with error codes
        var messages = failures
            .Select(f => $"[{f.ErrorCode}] {f.PropertyName}: {f.ErrorMessage}");
        var error = AppError.Validation(string.Join(" | ", messages));

        // If TResponse is Result or Result<T>, return failure directly
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(AppError)])!
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);
            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        // Fallback for non-Result handlers (shouldn't happen if conventions are followed)
        throw new ValidationException(failures);
    }
}
