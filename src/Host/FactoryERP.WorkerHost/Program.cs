using System.Globalization;
using EDI.Application;
using EDI.Infrastructure;
using EDI.Infrastructure.Worker;
using FactoryERP.Abstractions.Identity;
using FactoryERP.Abstractions.Realtime;
using FactoryERP.Infrastructure;
using FactoryERP.Infrastructure.Email;
using FactoryERP.Infrastructure.Messaging;
using FactoryERP.Infrastructure.Realtime;
using Hangfire;
using Hangfire.PostgreSql;
using Labeling.Application.Interfaces;
using Labeling.Infrastructure;
using Labeling.Infrastructure.Consumers;
using Labeling.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;
using FactoryERP.WorkerHost.Auth;
using DbFingerprint = FactoryERP.WorkerHost.DbFingerprint;


var builder = Host.CreateApplicationBuilder(args);

// Load appsettings.json and environment-specific settings
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Bootstrap logger — active until the full Serilog pipeline is built from appsettings
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information,
        formatProvider: CultureInfo.InvariantCulture,
        outputTemplate: "[BOOT] {Timestamp:HH:mm:ss} {Level:u3} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();
// Full Serilog pipeline — reads from appsettings.json "Serilog" section
builder.Services.AddSerilog((services, cfg) =>
    cfg.ReadFrom.Configuration(builder.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext());

// Background worker has no request context; provide a system-level identity
builder.Services.AddSingleton<ICurrentUserService, WorkerCurrentUserService>();

// WorkerHost has no SignalR hub — use a no-op dispatcher so consumers that
// inject INotificationDispatcher still work without any runtime failure.
builder.Services.AddSingleton<INotificationDispatcher, NullNotificationDispatcher>();

builder.Services.AddLabelingInfrastructure(builder.Configuration);
builder.Services.AddEdiApplication();
builder.Services.AddEdiInfrastructure(builder.Configuration);
// Caching (HybridCache L1 + Redis L2)
builder.Services.AddFactoryErpCaching(builder.Configuration);

// Hangfire — job server only; dashboard is intentionally NOT registered here
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = ["default", "edi", "printing"];
});

builder.Services.AddEmailInfrastructure(builder.Configuration);
// MassTransit + RabbitMQ + EF Core Outbox/Inbox + IEventBus (consumer side)
builder.Services.AddFactoryErpMessaging<LabelingDbContext>(
    builder.Configuration,
    cfg =>
    {
        cfg.AddConsumer<QrPrintRequestedConsumer>();
        cfg.AddConsumer<PrintZplCommandConsumer>();
    });

builder.Services.AddHostedService<EdiOutboxProcessorBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DbFingerprint.LogAsync(scope.ServiceProvider, logger, CancellationToken.None);
}

await app.RunAsync();
