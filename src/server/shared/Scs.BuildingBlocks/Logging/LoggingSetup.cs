using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Scs.BuildingBlocks.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// One JSON object per line on standard output (contracts/conventions.md §5). Framework
    /// logging is held at Warning because its Information entries include request paths and
    /// query strings. Sinks, enrichers and destructuring policies registered in the container
    /// are picked up, which is how tests capture output and how redaction is plugged in.
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
                .Enrich.FromLogContext()
                .Enrich.WithProperty("env", options.Environment)
                .Enrich.WithProperty("service", options.ServiceName)
                .Enrich.WithProperty("version", options.Version)
                .WriteTo.Console(new CompactJsonFormatter()),
            preserveStaticLogger: true);

        return services;
    }
}
