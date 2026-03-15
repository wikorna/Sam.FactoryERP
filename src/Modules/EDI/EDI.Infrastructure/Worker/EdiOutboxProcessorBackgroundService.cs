using EDI.Domain.Events;
using EDI.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EDI.Infrastructure.Worker;

public class EdiOutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<EdiOutboxProcessorBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> _logServiceStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2900, nameof(LogServiceStarted)),
            "EDI Outbox Processor started.");

    private static readonly Action<ILogger, Exception?> _logProcessingError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2901, nameof(LogProcessingError)),
            "Error processing EDI outbox messages.");

    private static readonly Action<ILogger, Guid, Exception?> _logMessageProcessingError =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2902, nameof(LogMessageProcessingError)),
            "Failed to process outbox message {MessageId}.");

    private static void LogServiceStarted(ILogger logger) =>
        _logServiceStarted(logger, null);

    private static void LogProcessingError(ILogger logger, Exception ex) =>
        _logProcessingError(logger, ex);

    private static void LogMessageProcessingError(ILogger logger, Exception ex, Guid messageId) =>
        _logMessageProcessingError(logger, messageId, ex);

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
        // 1. Fetch pending message IDs (fast, read-only)
        List<Guid> messageIds;
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EdiDbContext>();
            messageIds = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null)
                .OrderBy(m => m.OccurredOnUtc)
                .Take(20)
                .Select(m => m.Id)
                .ToListAsync(stoppingToken);
        }

        if (messageIds.Count == 0) return;

        // 2. Process each message in an ISOLATED transaction/scope
        foreach (var id in messageIds)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EdiDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var message = await dbContext.OutboxMessages.FindAsync([id], stoppingToken);
            if (message == null || message.ProcessedOnUtc != null) continue;

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
                message.ProcessedOnUtc = DateTime.UtcNow; // Mark processed to avoid loop
            }

            // Save ONLY this message's outcome (and whatever the handler did in this scope)
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}

