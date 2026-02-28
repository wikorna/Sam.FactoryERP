using System.Globalization;
using FactoryERP.Infrastructure;
using FactoryERP.Infrastructure.Email;
using FactoryERP.Infrastructure.Messaging;
using Labeling.Infrastructure;
using Labeling.Infrastructure.Consumers;
using Labeling.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;
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

builder.Services.AddLabelingInfrastructure(builder.Configuration);
// Caching (HybridCache L1 + Redis L2)
builder.Services.AddFactoryErpCaching(builder.Configuration);
builder.Services.AddEmailInfrastructure(builder.Configuration);
// MassTransit + RabbitMQ + EF Core Outbox/Inbox + IEventBus (consumer side)
builder.Services.AddFactoryErpMessaging<LabelingDbContext>(
    builder.Configuration,
    cfg =>
    {
        cfg.AddConsumer<QrPrintRequestedConsumer>();
        cfg.AddConsumer<PrintZplCommandConsumer>();
    });


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DbFingerprint.LogAsync(scope.ServiceProvider, logger, CancellationToken.None);
}

await app.RunAsync();
