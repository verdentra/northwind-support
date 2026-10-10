using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Presentation.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>Sla</c> configuration section, validates it when the application starts (so a
    /// bad value stops start-up with a clear message instead of failing on the first ticket), and
    /// registers the resulting <see cref="SlaPolicy"/> for the rest of the application.
    /// </summary>
    public static IServiceCollection AddSlaPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SlaOptions>()
            .Bind(configuration.GetSection(SlaOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SlaOptions>, SlaOptionsValidator>();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SlaOptions>>().Value.ToPolicy());

        return services;
    }

    private sealed class SlaOptionsValidator : IValidateOptions<SlaOptions>
    {
        public ValidateOptionsResult Validate(string? name, SlaOptions options)
        {
            var errors = options.Validate();

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
