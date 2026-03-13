using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FactoryERP.Contracts.Labeling;
using FactoryERP.Abstractions.Identity;

namespace Labeling.Application.Features.PrintJobs;

public partial class CreateProductLabelJobHandler : IRequestHandler<CreateProductLabelJobCommand, CreatePrintJobResult>
{
    private readonly ILabelingDbContext _dbContext;
    private readonly IPrinterAccessService _printerAccessService;
    private readonly IZplTemplateRenderer _renderer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateProductLabelJobHandler> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Idempotency hit for key {IdempotencyKey}. Returning existing Job {JobId}")]
    private partial void LogIdempotencyHit(string idempotencyKey, Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Printer {PrinterId} not found")]
    private partial void LogPrinterNotFound(Guid printerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Printer {PrinterId} is disabled")]
    private partial void LogPrinterDisabled(Guid printerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} denied access to printer {PrinterId}")]
    private partial void LogAccessDenied(Guid userId, Guid printerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to render ZPL for Product Label")]
    private partial void LogRenderError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Print Job {JobId} created and queued for printer {PrinterName}")]
    private partial void LogJobCreated(Guid jobId, string printerName);

    public CreateProductLabelJobHandler(
        ILabelingDbContext dbContext,
        IPrinterAccessService printerAccessService,
        IZplTemplateRenderer renderer,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateProductLabelJobHandler> logger)
    {
        _dbContext = dbContext;
        _printerAccessService = printerAccessService;
        _renderer = renderer;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<CreatePrintJobResult> Handle(CreateProductLabelJobCommand request, CancellationToken cancellationToken)
    {
        // 1. Idempotency Check
        var existingJob = await _dbContext.PrintJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingJob != null)
        {
            LogIdempotencyHit(request.IdempotencyKey, existingJob.Id);
            return new CreatePrintJobResult(existingJob.Id, existingJob.Status.ToString(), existingJob.IdempotencyKey, existingJob.CreatedAtUtc, "Job already exists.");
        }

        // 2. Validate Printer Exists & Is Enabled
        var printer = await _dbContext.Printers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PrinterId, cancellationToken);

        if (printer == null)
        {
            LogPrinterNotFound(request.PrinterId);
            throw new KeyNotFoundException($"Printer {request.PrinterId} not found.");
        }

        if (!printer.IsEnabled)
        {
            LogPrinterDisabled(request.PrinterId);
            throw new InvalidOperationException($"Printer {printer.Name} is currently disabled.");
        }

        // 3. Authorization Check
        // Security: Ensure the user is allowed to use this printer
        var canAccess = await _printerAccessService.CanAccessPrinterAsync(request.PrinterId, cancellationToken);
        if (!canAccess)
        {
            LogAccessDenied(_currentUserService.UserId, request.PrinterId);
            throw new UnauthorizedAccessException("You are not authorized to use this printer.");
        }

        // 4. Render ZPL
        string zplPayload;
        try
        {
            zplPayload = _renderer.RenderProductLabel(request.LabelData, printer);
        }
        catch (Exception ex)
        {
            LogRenderError(ex);
            throw new InvalidOperationException("Failed to generate label template.", ex);
        }

        // 5. Create Job Entity
        var correlationId = Guid.NewGuid(); // Or from request context if available
        var requestedBy = _currentUserService.RequestedBy ?? "System";
        var job = PrintJob.Create(
            request.IdempotencyKey,
            request.PrinterId,
            zplPayload,
            request.Copies,
            correlationId,
            requestedBy
        );

        // 6. Persist
        _dbContext.PrintJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 7. Publish Command for Worker (PrintZplCommand)
        await _publishEndpoint.Publish(new PrintZplCommand(
            job.Id,
            "Default", // TenantId placeholder
            job.PrinterId,
            job.ZplPayload,
            job.Copies,
            requestedBy,
            job.CorrelationId,
            DateTime.UtcNow
        ), cancellationToken);

        LogJobCreated(job.Id, printer.Name);

        return new CreatePrintJobResult(job.Id, job.Status.ToString(), job.IdempotencyKey, job.CreatedAtUtc);
    }
}
