// ═══════════════════════════════════════════════════════════════════════════════
// Sam.FactoryERP — ApiHost Composition Root (Program.cs)
// Production-grade Modular-Monolith startup with JWT RS256 + Serilog + MassTransit
// ═══════════════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Auth.Application.Interfaces;
using Auth.Infrastructure;
using Auth.Infrastructure.Options;
using Auth.Infrastructure.Seeding;
using FactoryERP.Abstractions.Extensions;
using FactoryERP.ApiHost.Auth;
using FactoryERP.ApiHost.Middleware;
using FactoryERP.ApiHost.Modules;
using FactoryERP.Infrastructure;
using FactoryERP.Infrastructure.Email;
using FactoryERP.Infrastructure.Messaging;
using Labeling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using DbFingerprint = FactoryERP.ApiHost.DbFingerprint;

// ──────────────────────────────────────────────────────────────────────────────
// 1. Builder configuration
// ──────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ── Serilog (bootstrap → full pipeline) ──────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information,
        formatProvider: CultureInfo.InvariantCulture,
        outputTemplate: "[BOOT] {Timestamp:HH:mm:ss} {Level:u3} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    Serilog.Debugging.SelfLog.Enable(m => Console.Error.WriteLine($"[SelfLog]: {m}"));
    var enrichers = services.GetServices<ILogEventEnricher>();

    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext()
       .Enrich.With(enrichers.ToArray())
       .Filter.ByExcluding(le =>
           le.Exception is TaskCanceledException &&
           le.Properties.TryGetValue("SourceContext", out var v) &&
           v.ToString().Contains("HealthChecks.Uris.UriHealthCheck"));
});

// ──────────────────────────────────────────────────────────────────────────────
// 2. Service registration
// ──────────────────────────────────────────────────────────────────────────────

// Global CQRS pipeline behaviors (Logging → Performance → Validation)
builder.Services.AddCqrsBehaviors();

// HybridCache L1 + optional Redis L2
builder.Services.AddFactoryErpCaching(builder.Configuration);

// Email infrastructure
builder.Services.AddEmailInfrastructure(builder.Configuration);


// JWT signing-key cache — preloads keys so the resolver never blocks.
builder.Services.AddSingleton<SigningKeyCacheService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SigningKeyCacheService>());

// Module registrations (Sales, Production, Purchasing, Inventory, etc.)
builder.Services.AddModules(builder.Configuration);
builder.Services
    .AddControllers()
    // Discover [ApiController] classes from module assemblies that are not auto-loaded
    // by the host because they are separate class-library projects (not .Web SDK).
    .AddApplicationPart(typeof(EDI.Api.Controllers.EdiFilesController).Assembly);

// Raise multipart body limit to 10 MB (same as the controller's file-size guard)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});
builder.Services.AddEndpointsApiExplorer();

// ── Swagger / OpenAPI (Swashbuckle) ──────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title   = "Sam.FactoryERP API",
        Version = "v1",
        Description = @"
Factory ERP backend service for small-to-medium manufacturing businesses.

Core Modules:
- Authentication & Authorization (JWT + Refresh Token)
- Inventory Management
- Production Planning
- Order Management
- Reporting

Environment: Development
"
    });

    // ✅ Fix: schemaId collision (same type name in different namespaces / nested types)
    c.CustomSchemaIds(type =>
    {
        var name = type.FullName ?? type.Name;
        return name.Replace("+", "."); // nested type: Outer+Inner -> Outer.Inner
    });

    // JWT Bearer "Authorize" button in Swagger UI
    const string schemeName = "Bearer";

    c.AddSecurityDefinition(schemeName, new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.ParameterLocation.Header,
        Description  = "Enter your JWT token. Example: eyJhbGciOi...",
    });

    c.AddSecurityRequirement(doc =>
    {
        var schemeRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference(schemeName, doc);
        var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement();
        requirement[schemeRef] = new List<string>();
        return requirement;
    });
});

// ── Authentication: RS256 JWT Bearer ─────────────────────────────────────────
builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .PostConfigure<SigningKeyCacheService, Microsoft.Extensions.Options.IOptions<JwtOptions>>(
        (options, keyCache, jwtOpt) =>
    {
        var jwt = jwtOpt.Value;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = jwt.Issuer,
            ValidateAudience         = true,
            ValidAudience            = jwt.Audience,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ClockSkew                = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),

            // O(1) in-memory read — SigningKeyCacheService refreshes keys in the background.
            // No I/O, no async blocking, no deadlock risk.
            IssuerSigningKeyResolver = (_, _, _, _) => keyCache.ValidationKeys,
        };

        options.Events = new JwtBearerEvents
        {
            // Reject tokens sent via query string (prevent URL logging/leaking).
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Query.ContainsKey("access_token"))
                    ctx.Fail("Tokens must not be sent via query string.");
                return Task.CompletedTask;
            },

            // JTI blacklist check — revoked tokens are rejected.
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    ctx.Fail("Token missing jti claim.");
                    return;
                }

                var blacklist = ctx.HttpContext.RequestServices.GetRequiredService<IJtiBlacklistService>();
                if (await blacklist.IsBlacklistedAsync(jti, ctx.HttpContext.RequestAborted))
                    ctx.Fail("Token has been revoked.");
            },
        };
    });

// Auth module (DbContext, Identity, key store, token services)
// Registered AFTER AddAuthentication().AddJwtBearer() so Identity's internal
// TryAddAuthentication is a no-op and the "Bearer" scheme is not duplicated.
builder.Services.AddAuthInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();

// Forwarded headers — safe defaults for reverse proxy (nginx, Traefik, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    if (builder.Environment.IsDevelopment())
    {
        opt.KnownProxies.Clear();
        opt.KnownIPNetworks.Clear();
    }
    // Production: DO NOT clear; configure KnownProxies/KnownNetworks explicitly in config.
});

// MassTransit + RabbitMQ + EF Core Outbox + IEventBus (publish-only, no consumers)
builder.Services.AddFactoryErpMessaging<LabelingDbContext>(builder.Configuration);

// CORS — must be registered before Build()
// Origins are read from config so appsettings.Development.json controls them without recompile.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:4200", "https://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaDevCors", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
        // ถ้าใช้ cookie/credential ค่อยเปิด:
        // .AllowCredentials();
    });
});

// ──────────────────────────────────────────────────────────────────────────────
// 3. Build & configure the middleware pipeline
// ──────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Pre-warm the signing-key cache before the host starts accepting requests.
// SigningKeyCacheService.ExecuteAsync will handle periodic refresh once the host starts.
{
    var keyCache = app.Services.GetRequiredService<SigningKeyCacheService>();
    var ks = app.Services.GetRequiredService<IKeyStoreService>();
    var keys = await ks.GetValidationKeysAsync();
    keyCache.SeedKeys(keys);
}

// ① Forwarded headers (must be first — corrects scheme/IP for everything downstream)
app.UseForwardedHeaders();

// ② Security response headers (CSP relaxed for Swagger, strict elsewhere)
app.UseMiddleware<SecurityHeadersMiddleware>();

// ③ HSTS (non-development only)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// ④ Structured HTTP request logging via Serilog
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent",   httpContext.Request.Headers.UserAgent.ToString());
    };
});

// ⑤ Swagger (development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sam.FactoryERP API v1");
        c.RoutePrefix = "swagger"; // → https://localhost:7076/swagger
    });
}

app.UseRouting();

app.UseCors("SpaDevCors");
// ⑥ Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
// ⑦ Endpoints
app.MapGet("/health", () => Results.Ok("OK"));
app.MapControllers();
app.MapModules();
// ──────────────────────────────────────────────────────────────────────────────
// 4. Startup diagnostics & run
// ──────────────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DbFingerprint.LogAsync(scope.ServiceProvider, logger, CancellationToken.None);
}

// Seed default dev user (Development only) — uses UserManager for correct hashing/normalization
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        await AuthIdentitySeeder.SeedDevUserAsync(scope.ServiceProvider, logger, CancellationToken.None);
    }
    catch (Exception ex)
    {
        throw ex.InnerException ?? ex; // Unwrap aggregate exceptions for clearer errors.
    }
}

app.Run();

