using EDI.Domain.Events;
using EDI.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EDI.Infrastructure.Worker;

public partial class EdiOutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<EdiOutboxProcessorBackgroundService> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "EDI Outbox Processor started.")]
    private static partial void LogServiceStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing EDI outbox messages.")]
    private static partial void LogProcessingError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process outbox message {MessageId}")]
    private static partial void LogMessageProcessingError(ILogger logger, Exception ex, Guid messageId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogProcessingError(logger, ex);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EdiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(stoppingToken);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                if (message.Type == nameof(EdiImportRequestedEvent))
                {
                    var domainEvent = JsonSerializer.Deserialize<EdiImportRequestedEvent>(message.Content);
                    if (domainEvent != null)
                    {
                        await mediator.Publish(domainEvent, stoppingToken);
                    }
                }

                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                LogMessageProcessingError(logger, ex, message.Id);
                message.Error = ex.Message;
                message.ProcessedOnUtc = DateTime.UtcNow; // Mark processed even on error to avoid infinite loop
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
