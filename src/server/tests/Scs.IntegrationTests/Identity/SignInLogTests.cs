using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, FR-054 and SC-011: a password, a code, a cookie value or an email address never
/// reaches the logs, on success or on any failure; signed-in work is marked with identifiers.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "LogRedaction")]
public sealed class SignInLogTests(SqlServerFixture sql)
{
    private const string Password = "Zzq-Secret-Pass-9!";
    private const string WrongPassword = "Zzq-Wrong-Pass-7!";

    [Fact]
    public async Task Nothing_typed_or_issued_at_sign_in_reaches_the_logs()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var localPart = $"zzq.mariam.{Guid.NewGuid():N}";
        var email = $"{localPart}@zzqclinic.example";
        var (id, _) = await world.Factory.CreateAccountAsync(world.Clinic, email: email, password: Password);
        var unknown = $"zzq.nobody.{Guid.NewGuid():N}@zzqclinic.example";
        var secrets = new List<string> { Password, WrongPassword, localPart, "zzqclinic", "zzq.nobody" };

        using var browser = world.Factory.Browser();

        // Every failure before a correct password.
        using (await browser.StepOneAsync(unknown, WrongPassword)) { }
        using (await browser.StepOneAsync(email, WrongPassword)) { }
        using (await browser.StepOneAsync("not-an-email-zzq", WrongPassword)) { }

        // The code cannot be sent, then it can; a wrong code; a resend that fails; the right code.
        world.Factory.Mail.Failing = true;
        using (await browser.StepOneAsync(email, Password)) { }
        world.Factory.Mail.Failing = false;

        using var first = await browser.StepOneAsync(email, Password);
        var code = world.Factory.Mail.LatestCodeFor(email);
        var wrongCode = code == "000000" ? "000001" : "000000";
        secrets.Add(first.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1]);

        using (await browser.StepTwoAsync(wrongCode)) { }
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        world.Factory.Mail.Failing = true;
        using (await browser.ResendAsync()) { }
        world.Factory.Mail.Failing = false;

        using var second = await browser.StepTwoAsync(code);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        foreach (var cookie in second.Headers.GetValues("Set-Cookie"))
        {
            var value = cookie.Split(';')[0].Split('=', 2)[1];
            if (value.Length > 0)
            {
                secrets.Add(value);
            }
        }

        // Signed-in work, a renewal, ending the sessions from elsewhere, and the refusal after it.
        using (await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative))) { }
        using (await browser.GetAsync(new Uri("/probe/whoami", UriKind.Relative))) { }
        using (var renewed = await browser.RenewAsync())
        {
            secrets.AddRange(renewed.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0].Split('=', 2)[1]));
        }

        await world.Factory.ForClinicAsync(
            world.Clinic, services => services.GetRequiredService<IStaffSessions>().EndForAccountAsync(id, SessionEndings.EndedForAccount, default));
        using (await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative))) { }
        using (await browser.SignOutAsync()) { }

        var logs = world.Factory.Logs.All;

        // The test means something only if these requests were logged.
        Assert.Contains("\"route\":\"/v1/auth/sign-in\"", logs, StringComparison.Ordinal);
        Assert.Contains("\"route\":\"/v1/auth/sign-in/code\"", logs, StringComparison.Ordinal);

        foreach (var secret in secrets.Append(code).Append(wrongCode).Where(s => s.Length >= 6))
        {
            Assert.False(
                logs.Contains(secret, StringComparison.OrdinalIgnoreCase),
                $"The logs contain '{secret[..Math.Min(12, secret.Length)]}…':\n{logs.Split('\n').FirstOrDefault(l => l.Contains(secret, StringComparison.OrdinalIgnoreCase))}");
        }

        foreach (var property in new[] { "\"email\"", "\"password\"", "\"code\"", "Set-Cookie", SessionCookies.Access, SessionCookies.Renewal })
        {
            Assert.DoesNotContain(property, logs, StringComparison.OrdinalIgnoreCase);
        }

        // Work done with the session says who it was for, by identifier.
        var signedIn = world.Factory.Logs.Lines.Where(l => l.Contains("\"route\":\"/probe/whoami\"", StringComparison.Ordinal)).ToList();
        var entry = Assert.Single(signedIn);
        Assert.Contains($"\"clinicId\":\"{world.Clinic}\"", entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"\"accountId\":\"{id}\"", entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"sessionId\":\"", entry, StringComparison.Ordinal);
    }
}
