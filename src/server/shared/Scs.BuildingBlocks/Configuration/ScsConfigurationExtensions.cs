using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Scs.BuildingBlocks.Configuration;

public static class ScsConfigurationExtensions
{
    private const string Prefix = "SCS_";

    /// <summary>
    /// Maps <c>SCS_*</c> environment variables into the <c>Scs</c> section
    /// (<c>SCS_DB__CONNECTION</c> becomes <c>Scs:DB:CONNECTION</c>). They are kept in their own
    /// section so <c>SCS_ENVIRONMENT</c> cannot be confused with the host's own environment name.
    /// </summary>
    public static IConfigurationBuilder AddScsEnvironmentVariables(this IConfigurationBuilder builder)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var path = key[Prefix.Length..].Replace("__", ":", StringComparison.Ordinal);
                values[$"{ScsOptions.SectionName}:{path}"] = entry.Value as string;
            }
        }

        return builder.AddInMemoryCollection(values);
    }

    /// <summary>Binds and validates <see cref="ScsOptions"/>; validation runs when the host starts.</summary>
    public static ScsOptions AddScsOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ScsOptions.SectionName);

        services.AddSingleton<IValidateOptions<ScsOptions>, ScsOptionsValidator>();
        services.AddOptions<ScsOptions>().Bind(section).ValidateOnStart();

        // A copy for decisions the host must make while it is still being composed. It is not
        // validated here; the validated instance comes from IOptions<ScsOptions>.
        return section.Get<ScsOptions>() ?? new ScsOptions();
    }
}
