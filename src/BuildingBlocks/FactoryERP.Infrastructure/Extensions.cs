using FactoryERP.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryERP.Infrastructure;

/// <summary>
/// Top-level infrastructure DI entry points for host projects.
/// Caching implementation lives in
/// <see cref="ServiceCollectionExtensionsCaching.AddFactoryErpCaching"/>.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Registers HybridCache L1 + optional Redis L2, ICacheService, and
    /// Redis health check.  Hosts call this method only — zero Redis knowledge
    /// required at the host level.
    /// </summary>
    public static IServiceCollection AddFactoryErpCaching(
        this IServiceCollection services,
        IConfiguration configuration)
        => ServiceCollectionExtensionsCaching.AddFactoryErpCaching(services, configuration);
}
