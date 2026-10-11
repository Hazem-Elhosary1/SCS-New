using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Jobs;
using Scs.Identity.Contracts;
using Scs.Identity.Infrastructure;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>Helpers for the password and activation flows of spec 004.</summary>
public static partial class Passwords
{
    /// <summary>A password that meets the rules and differs from <see cref="SignIn.Password"/>.</summary>
    public const string New = "Brand-New-Pass-7!";

    [GeneratedRegex(@"/set-password#t=([A-Za-z0-9_\-]+)")]
    private static partial Regex LinkPattern();

    /// <summary>The token in the newest emailed link to this address.</summary>
    public static string LatestLinkTokenFor(this FakeEmailSender mail, string email)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var message = mail.To(email).LastOrDefault(m => LinkPattern().IsMatch(m.TextBody))
                      ?? throw new InvalidOperationException("No link was emailed to that address.");
        return LinkPattern().Match(message.TextBody).Groups[1].Value;
    }

    public static int LinksSentTo(this FakeEmailSender mail, string email)
    {
        ArgumentNullException.ThrowIfNull(mail);
        return mail.To(email).Count(m => LinkPattern().IsMatch(m.TextBody));
    }

    /// <summary>
    /// Runs the clinic's account-email job exactly as the job server does: through the runner
    /// that establishes the clinic. Tests host the application without a worker.
    /// </summary>
    public static async Task RunAccountEmailJobAsync(this ApiFactory factory, Guid clinicId)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<AccountEmailJob>>()
            .RunAsync(clinicId, CancellationToken.None);
    }

    public static Task<HttpResponseMessage> ForgotAsync(this HttpClient browser, string? email) =>
        browser.PostAsJsonAsync("/v1/auth/password/forgot", new { email });

    public static Task<HttpResponseMessage> InspectLinkAsync(this HttpClient browser, string? token) =>
        browser.PostAsJsonAsync("/v1/auth/password/link", new { token });

    public static Task<HttpResponseMessage> SetWithLinkAsync(this HttpClient browser, string? token, string? password) =>
        browser.PostAsJsonAsync("/v1/auth/password/link/set", new { token, password });

    public static Task<HttpResponseMessage> ChangePasswordAsync(this HttpClient browser, string? password) =>
        browser.PostAsJsonAsync("/v1/auth/password/change", new { password });

    /// <summary>Asks for a reset link and lets the background job send it; returns the token.</summary>
    public static async Task<string> ResetTokenForAsync(this ApiFactory factory, Guid clinicId, string email)
    {
        using var browser = factory.Browser();
        using var asked = await browser.ForgotAsync(email);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, asked.StatusCode);
        await factory.RunAccountEmailJobAsync(clinicId);
        return factory.Mail.LatestLinkTokenFor(email);
    }

    /// <summary>Runs one hand-over operation for a clinic, as specs 015 and 026 will.</summary>
    public static Task<AccountHandoverResult> HandOverAsync(
        this ApiFactory factory, Guid clinicId, Func<IAccountHandover, Task<AccountHandoverResult>> operation) =>
        factory.ForClinicAsync(clinicId, services => operation(services.GetRequiredService<IAccountHandover>()));

    /// <summary>Status, code and the whole body of an answer.</summary>
    public static async Task<(int Status, string? Code, JsonElement Body)> ReadAsync(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var text = await response.Content.ReadAsStringAsync();
        if (text.Length == 0)
        {
            return ((int)response.StatusCode, null, default);
        }

        using var document = JsonDocument.Parse(text);
        var body = document.RootElement.Clone();
        var code = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var found) ? found.GetString() : null;
        return ((int)response.StatusCode, code, body);
    }

    /// <summary>The field codes of a refused password, in the order the server gave them.</summary>
    public static string[] PasswordCodes(this JsonElement body) =>
        [.. body.GetProperty("errors").GetProperty("password").EnumerateArray().Select(e => e.GetString()!)];
}
