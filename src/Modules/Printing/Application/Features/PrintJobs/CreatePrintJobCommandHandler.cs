using MediatR;
using Printing.Application.Abstractions;
using Printing.Domain;

namespace Printing.Application.Features.PrintJobs;

public class CreatePrintJobCommandHandler : IRequestHandler<CreatePrintJobCommand, Guid>
{
    private readonly IPrintJobRepository _printJobRepository;
    private readonly IPrinterRepository _printerRepository;
    private readonly IPrintRequestRepository _printRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    /*
    // BEFORE REFACTOR: Direct DbContext usage in handler
    private readonly PrintingDbContext _context;

    public CreatePrintJobCommandHandler(PrintingDbContext context)
    {
        _context = context;
    }
    */

    public CreatePrintJobCommandHandler(
        IPrintJobRepository printJobRepository,
        IPrinterRepository printerRepository,
        IPrintRequestRepository printRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _printJobRepository = printJobRepository;
        _printerRepository = printerRepository;
        _printRequestRepository = printRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreatePrintJobCommand request, CancellationToken cancellationToken)
    {
        /*
        // BEFORE REFACTOR: Logic with direct DbContext access
        var printer = await _context.Printers.FindAsync(request.PrinterId);
        if (printer == null)
        {
            throw new Exception("Printer not found");
        }

        var printRequest = await _context.PrintRequests.FindAsync(request.PrintRequestId);
        if (printRequest == null)
        {
            throw new Exception("Print request not found");
        }
        */

        // AFTER REFACTOR: Using repository abstractions
        var printer = await _printerRepository.GetByIdAsync(request.PrinterId, cancellationToken);
        if (printer == null)
        {
            throw new Exception("Printer not found");
        }

        var printRequest = await _printRequestRepository.GetByIdAsync(request.PrintRequestId, cancellationToken);
        if (printRequest == null)
        {
            throw new Exception("Print request not found");
        }

        var printJob = new PrintJob
        {
            PrinterId = request.PrinterId,
            PrintRequestId = request.PrintRequestId,
            Status = PrintJobStatus.Queued,
            CreatedUtc = DateTime.UtcNow
        };

        await _printJobRepository.AddAsync(printJob, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return printJob.Id;
    }
}

