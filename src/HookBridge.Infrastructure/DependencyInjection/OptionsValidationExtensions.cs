using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.DependencyInjection;

public static class OptionsValidationExtensions
{
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, IEnumerable<string>> validateFunc)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(validateFunc);

        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TOptions>>(
            new DelegateValidateOptions<TOptions>(sectionName, validateFunc));

        return services;
    }

    private sealed class DelegateValidateOptions<TOptions>(
        string sectionName,
        Func<TOptions, IEnumerable<string>> validateFunc) : IValidateOptions<TOptions>
        where TOptions : class
    {
        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var errors = validateFunc(options)
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .ToArray();

            return errors.Length == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(errors.Select(error => $"{sectionName}:{error}"));
        }
    }
}
