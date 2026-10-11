using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Logging;
using Scs.BuildingBlocks.Mail;
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

    /// <summary>Base64 of 32 bytes; a test value only.</summary>
    public const string TestCodeKey = "dGVzdC1vbmx5LWtleS0zMi1ieXRlcy1sb25nLTAxMjM0NQ==";

    /// <summary>Where links in emails lead, in tests.</summary>
    public const string StaffApp = "https://app.scs.test";

    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scs:ENVIRONMENT"] = "local",
        ["Scs:DB:CONNECTION"] = FakeConnection,
        ["Scs:DB:REPORT_CONNECTION"] = FakeConnection,
        ["Scs:DB:CROSSING_CONNECTION"] = FakeConnection,
        ["Scs:ROLE"] = "api",
        ["Scs:AUTH:CODE_KEY"] = TestCodeKey,
        ["Scs:AUTH:STAFF_APP_URL"] = StaffApp,
        ["Scs:MAIL:HOST"] = "localhost",
        ["Scs:MAIL:PORT"] = "1",
        ["Scs:MAIL:USE_TLS"] = "false",
        ["Scs:MAIL:FROM"] = "no-reply@scs.test",
        ["Scs:SUPPORT:HOURS_AR"] = "من السبت إلى الخميس",
        ["Scs:SUPPORT:HOURS_EN"] = "Saturday to Thursday",
        ["Scs:SUPPORT:HOTLINE"] = "19999",
        ["Scs:SUPPORT:WHATSAPP"] = "201000000000",
        ["Scs:SUPPORT:EMAIL"] = "support@scs.test",
    };

    public CapturedLogs Logs { get; } = new();

    /// <summary>Every email the application "sent"; nothing leaves the test.</summary>
    public FakeEmailSender Mail { get; } = new();

    /// <summary>The application's clock; tests move it to let a lock or a code run out.</summary>
    public MovableClock Clock { get; } = new();

    private readonly List<Action<IServiceCollection>> _services = [];

    /// <summary>Replaces or adds services before the first client is created.</summary>
    public ApiFactory WithServices(Action<IServiceCollection> configure)
    {
        _services.Add(configure);
        return this;
    }

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
            services.AddSingleton<IEmailSender>(Mail);
            services.AddSingleton<TimeProvider>(Clock);

            foreach (var configure in _services)
            {
                configure(services);
            }
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
