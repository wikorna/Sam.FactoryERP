using FactoryERP.Abstractions.Email;
using FactoryERP.Infrastructure.Email.Options;
using FactoryERP.Infrastructure.Email.Sending;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryERP.Infrastructure.Email;

public static class EmailRegistration
{
    public static IServiceCollection AddEmailInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(config.GetSection(EmailOptions.SectionName))
            .ValidateOnStart(); // Fail-fast on Boot

        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        // Register V2 and V1 backward compatible
        // services.AddTransient<IEmailSenderV2, MailKitSmtpEmailSender>();
        services.AddTransient<IEmailSender, MailKitSmtpEmailSender>();

        return services;
    }
}
