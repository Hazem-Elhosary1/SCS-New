using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, edge case "the email service is slow rather than failing": the request waits a
/// bounded time, then gets the one answer "it was not sent", and nothing about the recipient
/// or the server is logged.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    private const string Recipient = "zzq.slow.recipient@zzqmail.example";

    private sealed class Captured : ILogger<SmtpEmailSender>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)} {exception}");
    }

    private static SmtpEmailSender Sender(int port, Captured logs, int timeoutSeconds = 1) => new(
        Options.Create(new ScsOptions
        {
            Mail = new MailOptions
            {
                Host = "127.0.0.1",
                Port = port,
                UseTls = false,
                From = "no-reply@scs.test",
                TimeoutSeconds = timeoutSeconds,
            },
        }),
        logs);

    private static EmailMessage Message() => new(Recipient, "subject", "text 123456", "<p>123456</p>");

    [Fact]
    public async Task A_mail_server_that_never_answers_fails_the_send_within_the_time_limit()
    {
        // Accepts the connection and then says nothing, as a hung mail server does.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var port = ((IPEndPoint)silent.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource();
        var accepting = Task.Run(async () =>
        {
            var held = new List<TcpClient>();
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    held.Add(await silent.AcceptTcpClientAsync(stop.Token));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                held.ForEach(client => client.Dispose());
            }
        });

        var logs = new Captured();
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<EmailNotSentException>(() => Sender(port, logs).SendAsync(Message(), CancellationToken.None));

        watch.Stop();
        await stop.CancelAsync();
        await accepting;

        Assert.InRange(watch.Elapsed.TotalSeconds, 0.5, 5);
        AssertNothingPrivate(logs);
    }

    [Fact]
    public async Task A_mail_server_that_cannot_be_reached_fails_the_send_the_same_way()
    {
        // A port nothing listens on.
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var logs = new Captured();

        var failure = await Assert.ThrowsAsync<EmailNotSentException>(
            () => Sender(port, logs, timeoutSeconds: 3).SendAsync(Message(), CancellationToken.None));

        Assert.DoesNotContain(Recipient, failure.ToString(), StringComparison.OrdinalIgnoreCase);
        AssertNothingPrivate(logs);
    }

    private static void AssertNothingPrivate(Captured logs)
    {
        var line = Assert.Single(logs.Lines);
        Assert.StartsWith("Warning: An email could not be sent", line, StringComparison.Ordinal);
        Assert.DoesNotContain("zzq", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456", line, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", line, StringComparison.Ordinal);
    }
}
