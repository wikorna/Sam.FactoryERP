using EDI.Application.Abstractions;
using EDI.Infrastructure.Persistence;
using FactoryERP.SharedKernel.SeedWork;
using System.Text.Json;

namespace EDI.Infrastructure.Outbox;

public sealed class SqlOutboxPublisher : IOutboxPublisher
{
    private readonly EdiDbContext _dbContext;

    public SqlOutboxPublisher(EdiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = domainEvent.GetType().Name,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOnUtc = DateTime.UtcNow
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(ct);
    }
}
