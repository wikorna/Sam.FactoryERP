using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FactoryERP.Abstractions.Identity;

namespace Labeling.Infrastructure.Services;

public class PrinterAccessService : IPrinterAccessService
{
    private readonly ILabelingDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PrinterAccessService> _logger;

    private void LogPrinterNotFoundOrDisabled(Guid printerId) => _logger.LogWarning("Access denied: Printer {PrinterId} not found or disabled", printerId);

    private void LogUserOverrideDenied(Guid printerId, Guid userId) => _logger.LogWarning("Access denied to printer {PrinterId} for user {UserId} by override", printerId, userId);

    public PrinterAccessService(
        ILabelingDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<PrinterAccessService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> CanAccessPrinterAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        // 0. Ensure Printer exists and is Enabled
        var isEnabled = await _dbContext.Printers
            .AsNoTracking()
            .AnyAsync(p => p.Id == printerId && p.IsEnabled, cancellationToken);

        if (!isEnabled)
        {
            LogPrinterNotFoundOrDisabled(printerId);
            return false;
        }

        // 1. Super Admin or Permission Override (Label.Print.AnyPrinter)
        if (_currentUser.HasPermission("Label.Print.AnyPrinter"))
        {
            return true;
        }

        var userId = _currentUser.UserId;
        var deptId = _currentUser.DepartmentId;
        var storeId = _currentUser.StoreId;

        // 2. User Override (Priority)
        // If an override exists for this User+Printer, it dictates access (Allow or Deny).
        var overrideRecord = await _dbContext.UserPrinterOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PrinterId == printerId, cancellationToken);

        if (overrideRecord != null)
        {
            if (overrideRecord.Access == PrinterAccessType.Deny)
            {
                LogUserOverrideDenied(printerId, userId);
                return false;
            }
            return true; // Allowed
        }

        // 3. Department Access & Store Access (Additive)
        // Check if printer maps to User's Department OR Store

        bool allowedByDept = false;
        if (deptId.HasValue)
        {
            allowedByDept = await _dbContext.DepartmentPrinters
                .AsNoTracking()
                .AnyAsync(x => x.DepartmentId == deptId.Value && x.PrinterId == printerId, cancellationToken);
        }

        if (allowedByDept) return true;

        bool allowedByStore = false;
        if (storeId.HasValue)
        {
            allowedByStore = await _dbContext.StorePrinters
                .AsNoTracking()
                .AnyAsync(x => x.StoreId == storeId.Value && x.PrinterId == printerId, cancellationToken);
        }

        if (allowedByStore) return true;

        return false;
    }

    public async Task<IEnumerable<Guid>> GetAuthorizedPrinterIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.HasPermission("Label.Print.AnyPrinter"))
        {
            return await _dbContext.Printers
                .Where(p => p.IsEnabled)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
        }

        var userId = _currentUser.UserId;
        var deptId = _currentUser.DepartmentId;
        var storeId = _currentUser.StoreId;

        // Base query for overrides
        var overrideQuery = _dbContext.UserPrinterOverrides
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var allowedOverrides = overrideQuery
            .Where(x => x.Access == PrinterAccessType.Allow)
            .Select(x => x.PrinterId);

        var deniedOverrides = overrideQuery
            .Where(x => x.Access == PrinterAccessType.Deny)
            .Select(x => x.PrinterId);

        // Department Printers
        IQueryable<Guid> deptPrinters = Enumerable.Empty<Guid>().AsQueryable();
        if (deptId.HasValue)
        {
            deptPrinters = _dbContext.DepartmentPrinters
                .AsNoTracking()
                .Where(x => x.DepartmentId == deptId.Value)
                .Select(x => x.PrinterId);
        }

        // Store Printers
        IQueryable<Guid> storePrinters = Enumerable.Empty<Guid>().AsQueryable();
        if (storeId.HasValue)
        {
            storePrinters = _dbContext.StorePrinters
                .AsNoTracking()
                .Where(x => x.StoreId == storeId.Value)
                .Select(x => x.PrinterId);
        }

        // Union all allowed, Exception denied
        // Note: EF Core translation of complex Unions/Excepts can be tricky.
        // We will fetch lists and combine in memory if lists are small, but let's try strict IQueryable if possible.
        // Better to execute separate lightweight queries or one composed query.

        var allowedIds = new HashSet<Guid>(await allowedOverrides.ToListAsync(cancellationToken));
        var deniedIds = new HashSet<Guid>(await deniedOverrides.ToListAsync(cancellationToken));

        if (deptId.HasValue)
        {
            var dIds = await deptPrinters.ToListAsync(cancellationToken);
            foreach (var id in dIds) allowedIds.Add(id);
        }

        if (storeId.HasValue)
        {
            var sIds = await storePrinters.ToListAsync(cancellationToken);
            foreach (var id in sIds) allowedIds.Add(id);
        }

        // Remove denied
        allowedIds.ExceptWith(deniedIds);

        return allowedIds;
    }
}
