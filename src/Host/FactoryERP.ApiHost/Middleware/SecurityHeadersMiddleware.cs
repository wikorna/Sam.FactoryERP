namespace FactoryERP.ApiHost.Middleware;

/// <summary>
/// Adds production-grade security response headers to every non-Swagger request.
/// Swagger UI requires inline scripts/styles that conflict with a strict CSP,
/// so <c>/swagger</c> paths receive relaxed headers while all other traffic gets
/// the full hardened set.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string StrictCsp =
        "default-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self';";

    // Swagger UI requires unsafe-inline styles and scripts + blob for API docs.
    private const string SwaggerCsp =
        "default-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "frame-ancestors 'none';";

    public Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Common security headers for ALL responses.
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"]        = "DENY";
        headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"]       = "0"; // Modern: rely on CSP, not XSS-Auditor.
        headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";

        // CSP — relaxed for Swagger UI, strict everywhere else.
        headers["Content-Security-Policy"] =
            context.Request.Path.StartsWithSegments("/swagger")
                ? SwaggerCsp
                : StrictCsp;

        return next(context);
    }
}

