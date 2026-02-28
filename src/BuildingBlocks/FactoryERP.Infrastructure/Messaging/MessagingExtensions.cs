using FactoryERP.Abstractions.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Infrastructure.Messaging;

/// <summary>
/// MassTransit + RabbitMQ (or in-memory) + EF Core Outbox/Inbox registration.
/// Call exactly once per host (ApiHost / WorkerHost).
/// </summary>
public static class MessagingExtensions
{
    /// <summary>Marker service to detect duplicate registrations.</summary>
    private sealed class MassTransitRegistered;

    private static readonly Action<ILogger, string, int, string, string, Exception?> LogRabbitMqConfiguring =
        LoggerMessage.Define<string, int, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogRabbitMqConfiguring)),
            "Configuring MassTransit RabbitMQ — Host={Host}, Port={Port}, VHost={VHost}, User={User}");

    /// <summary>
    /// Registers MassTransit (RabbitMQ when <c>RabbitMQ:Enabled=true</c>, otherwise in-memory),
    /// EF Core Outbox/Inbox, environment-prefixed endpoint naming, and <see cref="IEventBus"/>.
    /// </summary>
    /// <typeparam name="TDbContext">
    /// The module DbContext that hosts the Outbox/Inbox tables.
    /// </typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration (reads <c>RabbitMQ</c> section).</param>
    /// <param name="configureConsumers">
    /// Optional — register module consumers and their definitions
    /// (e.g. <c>cfg.AddConsumer&lt;MyConsumer, MyConsumerDefinition&gt;()</c>).
    /// Typically only supplied by WorkerHost; ApiHost omits this for publish-only mode.
    /// </param>
    public static IServiceCollection AddFactoryErpMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext
    {
        // Guard: AddMassTransit() must be called exactly once per container.
        if (services.Any(sd => sd.ServiceType == typeof(MassTransitRegistered)))
            return services;

        services.AddSingleton<MassTransitRegistered>();
        var rabbitOptions = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddMassTransit(bus =>
        {
            // ── Consumer registrations (module-supplied) ──────────────────────
            configureConsumers?.Invoke(bus);

            // ── EF Core Outbox / Inbox ────────────────────────────────────────
            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay   = TimeSpan.FromSeconds(1);
                outbox.QueryTimeout = TimeSpan.FromSeconds(30);
            });

            // ── Transport ─────────────────────────────────────────────────────
            if (rabbitOptions.Enabled)
            {
                bus.UsingRabbitMq((context, cfg) =>
                {
                    var logger = context.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("FactoryERP.Infrastructure.Messaging");

                    LogRabbitMqConfiguring(
                        logger,
                        rabbitOptions.Connection.HostName,
                        rabbitOptions.Connection.Port,
                        rabbitOptions.Connection.VirtualHost,
                        rabbitOptions.Connection.UserName,
                        null);

                    cfg.Host(
                        rabbitOptions.Connection.HostName,
                        (ushort)rabbitOptions.Connection.Port,
                        rabbitOptions.Connection.VirtualHost,
                        h =>
                        {
                            h.Username(rabbitOptions.Connection.UserName);
                            h.Password(rabbitOptions.Connection.Password);

                            if (rabbitOptions.Connection.UseSsl)
                            {
                                h.UseSsl(ssl =>
                                    ssl.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                            }
                        });

                    // Auto-configure all registered consumers using env-prefixed queue names.
                    // Retry + Inbox policies are owned by each ConsumerDefinition.
                    cfg.ConfigureEndpoints(context,
                        new EnvironmentEndpointNameFormatter(rabbitOptions.EnvironmentPrefix));
                });
            }
            else
            {
                // In-memory transport — used in development / integration tests
                // when RabbitMQ is not available.
                bus.UsingInMemory((context, cfg) =>
                    cfg.ConfigureEndpoints(context,
                        new EnvironmentEndpointNameFormatter(rabbitOptions.EnvironmentPrefix)));
            }
        });

        // Transport-agnostic publish abstraction for application/domain layers.
        services.AddScoped<IEventBus, MassTransitEventBus>();

        return services;
    }
}

/// <summary>
/// Custom endpoint name formatter that prefixes queue names with the environment.
/// </summary>
public sealed class EnvironmentEndpointNameFormatter(string environmentPrefix, bool includeNamespace = false)
    : DefaultEndpointNameFormatter(includeNamespace)
{
    public override string SanitizeName(string name)
        => $"{environmentPrefix}-{base.SanitizeName(name)}";
}
