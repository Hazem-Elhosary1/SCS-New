using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Configuration;
using Serilog;
using Serilog.Events;

namespace Scs.BuildingBlocks.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// One JSON object per line on standard output (contracts/conventions.md §5), protected in
    /// layers (FR-021): framework logging is held at Warning because its Information entries
    /// include request paths and query strings; classified members and domain objects are
    /// masked whether logged as structure or as text; and every line passes the scrubber.
    /// Sinks registered in the container are picked up, which is how tests capture output.
    /// </summary>
    public static IServiceCollection AddScsLogging(this IServiceCollection services, ScsOptions options)
    {
        services.AddSerilog(
            (provider, logger) => logger
                .ReadFrom.Services(provider)
                .MinimumLevel.Is(options.IsLocal ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
                .Destructure.With<ClassifiedDataDestructuringPolicy>()
                .Enrich.FromLogContext()
                .Enrich.With<ClassifiedScalarEnricher>()
                .Enrich.WithProperty("env", options.Environment)
                .Enrich.WithProperty("service", options.ServiceName)
                .Enrich.WithProperty("version", options.Version)
                .WriteTo.Console(new ScrubbingJsonFormatter()),
            preserveStaticLogger: true);

        return services;
    }
}
