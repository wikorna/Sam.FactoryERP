using MediatR;

namespace FactoryERP.Abstractions.Cqrs;

/// <summary>Command with no return value (side-effect only).</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Command returning a typed result.</summary>
public interface ICommand<T> : IRequest<Result<T>>;

/// <summary>Query returning a typed result. Always read-only.</summary>
public interface IQuery<T> : IRequest<Result<T>>;
