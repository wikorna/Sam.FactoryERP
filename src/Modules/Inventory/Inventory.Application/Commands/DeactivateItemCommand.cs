using FactoryERP.Abstractions.Cqrs;

namespace Inventory.Application.Commands;

/// <summary>Soft-deletes (deactivates) an item.</summary>
public sealed record DeactivateItemCommand(Guid Id) : ICommand;
