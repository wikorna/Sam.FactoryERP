using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using FactoryERP.Abstractions.Pagination;
using Inventory.Application.Caching;
using Inventory.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Queries;

/// <summary>Fiori Value Help (F4) query for item dropdowns.</summary>
public sealed record GetItemValueHelpQuery : ValueHelpQuery;

/// <summary>Returns id + display text for item value help.</summary>
public sealed class GetItemValueHelpQueryHandler(IInventoryDbContext db, ICacheService cache)
    : IRequestHandler<GetItemValueHelpQuery, Result<ValueHelpResponse>>
{
    public async Task<Result<ValueHelpResponse>> Handle(
        GetItemValueHelpQuery request, CancellationToken cancellationToken)
    {
        var filterHash = ComputeFilterHash(request);
        var cacheKey = InventoryCacheKeys.ItemValueHelp(filterHash);
        var settings = InventoryCacheKeys.ValueHelp(InventoryCacheKeys.TagItems);

        var response = await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var query = db.Items.AsNoTracking()
                    .Where(i => i.Status == Inventory.Domain.Enums.ItemStatus.Active);

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var pattern = $"%{request.Search.Trim()}%";
                    query = query.Where(i =>
                        EF.Functions.Like(i.ItemNumber, pattern) ||
                        EF.Functions.Like(i.Description, pattern));
                }

                var totalCount = await query.CountAsync(ct);

                var rows = await query
                    .OrderBy(i => i.ItemNumber)
                    .ApplyPaging(request.Page, request.PageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.ItemNumber,
                        i.Description,
                        i.BaseUom,
                        MaterialGroup = i.MaterialGroup ?? ""
                    })
                    .ToListAsync(ct);

                var items = rows.Select(r => new ValueHelpItem
                {
                    Id = r.Id.ToString(),
                    Text = $"{r.ItemNumber} — {r.Description}",
                    AdditionalColumns = new Dictionary<string, string>
                    {
                        ["BaseUom"] = r.BaseUom,
                        ["MaterialGroup"] = r.MaterialGroup
                    }
                }).ToList();

                return new ValueHelpResponse { Items = items, TotalCount = totalCount };
            },
            settings,
            cancellationToken);

        return response;
    }

    private static string ComputeFilterHash(GetItemValueHelpQuery request)
    {
        var json = JsonSerializer.Serialize(new { request.Search, request.Page, request.PageSize });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hash)[..8];
    }
}
