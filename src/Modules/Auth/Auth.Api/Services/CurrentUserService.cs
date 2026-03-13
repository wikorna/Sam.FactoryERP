using System.Security.Claims;
using FactoryERP.Abstractions.Identity;
using Microsoft.AspNetCore.Http;

namespace Auth.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

            return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
        }
    }

    public string? RequestedBy => _httpContextAccessor.HttpContext?.User?.Identity?.Name
                                  ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;

    public Guid? DepartmentId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("department_id")?.Value;
            return Guid.TryParse(claim, out var guid) ? guid : null;
        }
    }

    public Guid? StoreId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("store_id")?.Value;
            return Guid.TryParse(claim, out var guid) ? guid : null;
        }
    }

    public bool HasPermission(string permission)
    {
        return _httpContextAccessor.HttpContext?.User?.HasClaim("permissions", permission) ?? false;
        // Or whatever claim type is used for permissions in this system.
        // Often "permissions" or "scope".
    }

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}

