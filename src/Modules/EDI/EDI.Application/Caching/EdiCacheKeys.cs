using FactoryERP.Abstractions.Caching;

namespace EDI.Application.Caching;

/// <summary>
/// Centralized cache key/tag/TTL constants for the EDI module.
/// Convention: <c>{module}:{entity}:{version}:{identifier}</c> — all lowercase via <see cref="CacheKey.Create"/>.
/// </summary>
public static class EdiCacheKeys
{
    private const string Version = "v1";

    // ── Tags ──
    public const string TagJobs = "edi:jobs";
    public const string TagPartners = "edi:partners";
    public const string TagFileTypeConfigs = "edi:filetypeconfigs";

    public static string TagJob(Guid jobId) => $"edi:job:{jobId}";
    public static string TagPartner(string partnerCode) => $"edi:partner:{partnerCode.ToLowerInvariant()}";

    // ── Keys ──
    public static string JobById(Guid id)
        => CacheKey.Create("edi", "job", Version, id.ToString());

    public static string JobsList(string filterHash)
        => CacheKey.Create("edi", "jobs", "list", Version, filterHash);

    public static string PartnerProfile(string partnerCode)
        => CacheKey.Create("edi", "partner", Version, partnerCode);

    /// <summary>All active file type configs (config-driven EDI).</summary>
    public static readonly string FileTypeConfigs =
        CacheKey.Create("edi", "filetypeconfigs", Version);

    // ── TTL presets ──

    /// <summary>PartnerProfile: master data, rarely changes. L2=120 min, L1=15 min.</summary>
    public static CacheEntrySettings PartnerMasterData(string partnerCode) => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(120),
        L1Expiration = TimeSpan.FromMinutes(15),
        Tags = [TagPartners, TagPartner(partnerCode)]
    };

    /// <summary>Single job detail: mutable state during pipeline. L2=60 sec, L1=15 sec.</summary>
    public static CacheEntrySettings JobDetail(Guid jobId) => new()
    {
        AbsoluteExpiration = TimeSpan.FromSeconds(60),
        L1Expiration = TimeSpan.FromSeconds(15),
        Tags = [TagJobs, TagJob(jobId)]
    };

    /// <summary>Paginated job list: dashboard polling. L2=5 min, L1=1 min.</summary>
    public static CacheEntrySettings JobList() => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(5),
        L1Expiration = TimeSpan.FromMinutes(1),
        Tags = [TagJobs]
    };

    /// <summary>File type configs: master data, rarely changes. L2=60 min, L1=10 min.</summary>
    public static CacheEntrySettings FileTypeConfigSettings() => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(60),
        L1Expiration = TimeSpan.FromMinutes(10),
        Tags = [TagFileTypeConfigs]
    };
}

