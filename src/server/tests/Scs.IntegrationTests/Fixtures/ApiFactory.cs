using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Logging;
using Scs.BuildingBlocks.Modules;
using Serilog.Core;
using Serilog.Events;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real application in memory. Settings can be changed or removed before the first
/// client is created; everything the application logs is captured in <see cref="Logs"/> exactly
/// as it would be written to standard output.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string FakeConnection = "Server=localhost,1;Database=scs_none;User Id=nobody;Password=unused;Encrypt=false;Connect Timeout=1";

    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scs:ENVIRONMENT"] = "local",
        ["Scs:DB:CONNECTION"] = FakeConnection,
        ["Scs:ROLE"] = "api",
    };

    public CapturedLogs Logs { get; } = new();

    /// <summary>Sets a configuration value; <c>null</c> removes it.</summary>
    public ApiFactory With(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    /// <summary>A client that speaks HTTPS, for environments that redirect plain HTTP.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in _settings)
        {
            if (value is not null)
            {
                builder.UseSetting(key, value);
            }
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILogEventSink>(Logs);
            services.AddSingleton<IEndpointContributor, ProbeEndpoints>();
        });
    }
}

/// <summary>Collects log entries rendered with the production formatter.</summary>
public sealed class CapturedLogs : ILogEventSink
{
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly ScrubbingJsonFormatter _formatter = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public string All => string.Join('\n', _lines);

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        _lines.Enqueue(writer.ToString());
    }
}
