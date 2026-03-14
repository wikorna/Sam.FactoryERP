using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Abstractions;
using Notification.Infrastructure.Consumers;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Notification.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Notification module's infrastructure services.
    /// Call from both ApiHost and WorkerHost.
    /// </summary>
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

        services.AddScoped<INotificationDbContext>(
            sp => sp.GetRequiredService<NotificationDbContext>());

        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }

    /// <summary>
    /// Registers the WorkerHost-side consumers (creates notifications from domain events).
    /// Call inside the <c>configureConsumers</c> callback of <c>AddFactoryErpMessaging</c>.
    /// </summary>
    public static IBusRegistrationConfigurator AddNotificationWorkerConsumers(
        this IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<QrPrintFailedNotificationConsumer>();
        return cfg;
    }

    /// <summary>
    /// Registers the ApiHost-side consumer that receives <c>NotificationCreatedIntegrationEvent</c>
    /// and pushes the event to Angular clients via SignalR.
    /// Call inside the <c>configureConsumers</c> callback of <c>AddFactoryErpMessaging</c>.
    /// </summary>
    public static IBusRegistrationConfigurator AddNotificationApiHostConsumers(
        this IBusRegistrationConfigurator cfg)
    {
        // Pushes generic persistent notifications to the inbox
        cfg.AddConsumer<NotificationSignalRPushConsumer>();

        // Pushes transient print pipeline status updates (Queued, Printed, Failed)
        cfg.AddConsumer<ShipmentBatchPrintQueuedSignalRConsumer>();
        cfg.AddConsumer<ShipmentItemPrintedSignalRConsumer>();
        cfg.AddConsumer<ShipmentItemPrintFailedSignalRConsumer>();

        return cfg;
    }
}

