using EDI.Application.Abstractions;
using EDI.Application.Caching;
using FactoryERP.Abstractions.Caching;
using MediatR;

namespace EDI.Application.Features.GetFileTypeConfigs;

public sealed class GetFileTypeConfigsQueryHandler(
    IEdiFileTypeConfigRepository configRepo,
    ICacheService cache)
    : IRequestHandler<GetFileTypeConfigsQuery, GetFileTypeConfigsResponse>
{
    public async Task<GetFileTypeConfigsResponse> Handle(
        GetFileTypeConfigsQuery request,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            EdiCacheKeys.FileTypeConfigs,
            async ct =>
            {
                var configs = await configRepo.GetAllActiveAsync(ct);

                var dtos = configs.Select(c => new FileTypeConfigDto(
                    c.Id,
                    c.FileTypeCode,
                    c.DisplayName,
                    c.FilenamePrefixPattern,
                    c.Delimiter,
                    c.HasHeaderRow,
                    c.HeaderLineCount,
                    c.SkipLines,
                    c.SchemaVersion,
                    c.MaxFileSizeBytes,
                    c.Columns.OrderBy(col => col.Ordinal).Select(col => new ColumnDefinitionDto(
                        col.Ordinal,
                        col.ColumnName,
                        col.DataType,
                        col.IsRequired,
                        col.MaxLength,
                        col.ValidationRegex,
                        col.DisplayLabel)).ToList()
                )).ToList();

                return new GetFileTypeConfigsResponse(dtos);
            },
            EdiCacheKeys.FileTypeConfigSettings(),
            cancellationToken);
    }
}

