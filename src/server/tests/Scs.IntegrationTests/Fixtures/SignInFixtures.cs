using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>Captures every email instead of sending it, and can be told to fail.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>While true, every send fails exactly as an unreachable mail server would.</summary>
    public bool Failing { get; set; }

    /// <summary>Addresses the mail server refuses while everyone else's mail goes through.</summary>
    public ConcurrentDictionary<string, bool> FailingFor { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (Failing || FailingFor.ContainsKey(message.To))
        {
            throw new EmailNotSentException();
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string email) =>
        [.. _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The six digits in the newest message to this address.</summary>
    public string LatestCodeFor(string email)
    {
        var messages = To(email);
        if (messages.Count == 0)
        {
            throw new InvalidOperationException("No email was sent to that address.");
        }

        return SignIn.CodePattern().Match(messages[^1].TextBody).Value;
    }
}

/// <summary>A clock tests move by hand, so a lock or a code can run out without waiting.</summary>
public sealed class MovableClock : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow().AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);
}

/// <summary>A blocked-network list tests fill by hand; in the product it is spec 025's.</summary>
public sealed class TestBlockedNetworks : IBlockedNetworks
{
    public bool BlockEveryone { get; set; }

    public ValueTask<bool> IsBlockedAsync(IPAddress? address, CancellationToken cancellation) =>
        ValueTask.FromResult(BlockEveryone);
}

/// <summary>Helpers for exercising sign-in the way a browser does: over HTTPS, keeping cookies.</summary>
public static partial class SignIn
{
    public const string Password = "Correct-Horse-9!";

    [GeneratedRegex(@"(?<![0-9])[0-9]{6}(?![0-9])")]
    public static partial Regex CodePattern();

    /// <summary>A browser: HTTPS, its own cookie jar, and the header our front ends send on every change.</summary>
    public static HttpClient Browser(this ApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add(RequestGuardExtensions.RequestHeader, "1");
        return client;
    }

    /// <summary>Creates an account in the clinic through the module's own operation.</summary>
    public static async Task<(Guid Id, string Email)> CreateAccountAsync(
        this ApiFactory factory, Guid clinicId, string state = StaffAccountStates.Active, string? email = null, string? password = Password)
    {
        email ??= $"person-{Guid.NewGuid():N}@clinic.example";
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(clinicId, ClinicSource.Job);
        var id = await scope.ServiceProvider.GetRequiredService<IStaffAccounts>()
            .CreateAsync(email, state, state == StaffAccountStates.Invited ? null : password, CancellationToken.None);
        return (id, email);
    }

    /// <summary>Runs work for a clinic with the module's services, as a later spec's code would.</summary>
    public static async Task<T> ForClinicAsync<T>(this ApiFactory factory, Guid clinicId, Func<IServiceProvider, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(clinicId, ClinicSource.Job);
        return await work(scope.ServiceProvider);
    }

    public static Task ForClinicAsync(this ApiFactory factory, Guid clinicId, Func<IServiceProvider, Task> work) =>
        factory.ForClinicAsync<object?>(clinicId, async services =>
        {
            await work(services);
            return null;
        });

    public static Task<HttpResponseMessage> StepOneAsync(this HttpClient browser, string email, string password) =>
        browser.PostAsJsonAsync("/v1/auth/sign-in", new { email, password });

    public static Task<HttpResponseMessage> StepTwoAsync(this HttpClient browser, string code) =>
        browser.PostAsJsonAsync("/v1/auth/sign-in/code", new { code });

    public static Task<HttpResponseMessage> ResendAsync(this HttpClient browser) =>
        browser.PostAsync(new Uri("/v1/auth/sign-in/code/resend", UriKind.Relative), content: null);

    public static Task<HttpResponseMessage> RenewAsync(this HttpClient browser) =>
        browser.PostAsync(new Uri("/v1/auth/session/renew", UriKind.Relative), content: null);

    public static Task<HttpResponseMessage> SignOutAsync(this HttpClient browser) =>
        browser.PostAsync(new Uri("/v1/auth/sign-out", UriKind.Relative), content: null);

    /// <summary>Completes both steps and returns a browser that carries the session.</summary>
    public static async Task<HttpClient> SignInAsAsync(this ApiFactory factory, string email, string password = Password)
    {
        var browser = factory.Browser();
        using var first = await browser.StepOneAsync(email, password);
        Assert.True(first.IsSuccessStatusCode, $"Step one answered {(int)first.StatusCode}.");
        using var second = await browser.StepTwoAsync(factory.Mail.LatestCodeFor(email));
        Assert.True(second.IsSuccessStatusCode, $"Step two answered {(int)second.StatusCode}.");
        return browser;
    }

    /// <summary>
    /// A signed-in browser for a clinic, creating an account when none is given. For tests of any
    /// module whose endpoints need a signed-in person (contracts/sessions.md §9).
    /// </summary>
    public static async Task<HttpClient> SignInAsAsync(this ApiFactory factory, Guid clinicId)
    {
        var (_, email) = await factory.CreateAccountAsync(clinicId);
        return await factory.SignInAsAsync(email);
    }
}
