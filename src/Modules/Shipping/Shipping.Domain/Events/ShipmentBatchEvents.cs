using FactoryERP.SharedKernel.SeedWork;

namespace Shipping.Domain.Events;

/// <summary>Raised when Marketing submits a batch for warehouse review.</summary>
public sealed record ShipmentBatchSubmitted(Guid BatchId, string BatchNumber, int ItemCount) : IDomainEvent;

/// <summary>Raised when Warehouse approves a batch.</summary>
public sealed record ShipmentBatchApproved(Guid BatchId, string BatchNumber, Guid ReviewedByUserId) : IDomainEvent;

/// <summary>Raised when Warehouse rejects a batch.</summary>
public sealed record ShipmentBatchRejected(Guid BatchId, string BatchNumber, Guid ReviewedByUserId, string Reason) : IDomainEvent;

/// <summary>Raised when an approved batch has its print request published to the queue.</summary>
public sealed record ShipmentBatchPrintRequested(Guid BatchId, string BatchNumber, int ItemCount) : IDomainEvent;

/// <summary>Raised when all items in the batch have been printed successfully.</summary>
public sealed record ShipmentBatchCompleted(Guid BatchId, string BatchNumber) : IDomainEvent;

