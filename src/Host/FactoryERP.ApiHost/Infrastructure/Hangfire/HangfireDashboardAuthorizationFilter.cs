using Hangfire.Dashboard;

namespace FactoryERP.ApiHost.Infrastructure.Hangfire;

/// <summary>
/// Restricts the Hangfire Dashboard to authenticated users who hold the "Admin" role.
/// Called synchronously by Hangfire before serving any dashboard request.
/// </summary>
/// <remarks>
/// Intentionally does NOT use the Hangfire.Dashboard.Authorization NuGet package — the
/// same result is achieved here with zero extra dependencies by reading directly from the
/// ASP.NET Core <see cref="HttpContext"/>.
/// </remarks>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private const string AdminRole = "Admin";

    /// <summary>Returns <c>true</c> only when the request is authenticated and the caller holds the Admin role.</summary>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Reject unauthenticated requests immediately.
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        // Only users with the Admin role may access the dashboard.
        return httpContext.User.IsInRole(AdminRole);
    }
}

