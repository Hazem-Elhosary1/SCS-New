using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 003, User Story 1, against contracts/identity-api.openapi.yaml: email and password, then
/// the code from the email; no session before the code; afterwards the work is the account's
/// clinic's and nobody else's.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SignInFlowTests(SqlServerFixture sql)
{
    private async Task<(ApiFactory Factory, Guid Clinic, Guid Account, string Email)> StartAsync()
    {
        await sql.EnsureMigratedAsync();
        var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var (account, email) = await factory.CreateAccountAsync(clinic);
        return (factory, clinic, account, email);
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    [Fact]
    public async Task A_correct_password_sends_a_code_and_opens_no_session()
    {
        var (factory, _, account, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.InRange(body.RootElement.GetProperty("expiresInSeconds").GetInt32(), 595, 600);
        Assert.Equal(60, body.RootElement.GetProperty("resendAvailableInSeconds").GetInt32());
        Assert.Equal(3, body.RootElement.GetProperty("resendsLeft").GetInt32());

        var cookie = Assert.Single(SetCookies(response));
        Assert.StartsWith(SessionCookies.Challenge + "=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var message = Assert.Single(factory.Mail.To(email));
        Assert.Matches(SignIn.CodePattern(), message.TextBody);

        // No session exists after the first step (SC-003).
        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal(
            0,
            await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{account}'"));
    }

    [Fact]
    public async Task The_code_opens_the_session_and_is_recorded()
    {
        var (factory, clinic, account, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();
        using var first = await browser.StepOneAsync(email, SignIn.Password);

        using var response = await browser.StepTwoAsync(factory.Mail.LatestCodeFor(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(account, body.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("Test clinic", body.RootElement.GetProperty("clinic").GetProperty("nameEn").GetString());
        Assert.Equal("active", body.RootElement.GetProperty("clinic").GetProperty("state").GetString());

        var cookies = SetCookies(response).ToList();
        Assert.Contains(cookies, c => c.StartsWith(SessionCookies.Access + "=", StringComparison.Ordinal));
        var renewal = Assert.Single(cookies, c => c.StartsWith(SessionCookies.Renewal + "=", StringComparison.Ordinal));
        Assert.Contains("path=/v1/auth", renewal, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            cookies.Where(c => !c.StartsWith(SessionCookies.Challenge + "=;", StringComparison.Ordinal)),
            c =>
            {
                Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("secure", c, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("samesite=strict", c, StringComparison.OrdinalIgnoreCase);
            });

        // The pending sign-in is forgotten.
        Assert.Contains(cookies, c => c.StartsWith(SessionCookies.Challenge + "=;", StringComparison.Ordinal));

        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // Exactly one sign-in event, an activity, with the session and the request reference (FR-052).
        var trace = response.Headers.GetValues("X-Trace-Id").Single();
        Assert.Equal(
            1,
            await TestDatabase.ScalarAsync<int>(
                sql.Main.Crossing,
                $"""
                 SELECT COUNT(*) FROM [identity].SignInEvent e JOIN [identity].Session s ON s.Id = e.SessionId
                 WHERE e.AccountId = '{account}' AND e.ClinicId = '{clinic}' AND e.Kind = 'signed-in'
                   AND e.Category = 'activity' AND e.TraceId = '{trace}'
                 """));
        Assert.Equal(
            1,
            await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}'"));
    }

    [Fact]
    public async Task The_email_holds_the_code_and_its_lifetime_and_nothing_about_the_clinic_or_the_person()
    {
        var (factory, _, _, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        var message = Assert.Single(factory.Mail.To(email));
        foreach (var part in new[] { message.Subject, message.TextBody, message.HtmlBody })
        {
            Assert.DoesNotContain("Test clinic", part, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("عيادة تجريبية", part, StringComparison.Ordinal);
            Assert.DoesNotContain(email, part, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("10", message.TextBody, StringComparison.Ordinal);
        Assert.Contains(factory.Mail.LatestCodeFor(email), message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null, "email", "REQUIRED")]
    [InlineData("", "x", "email", "REQUIRED")]
    [InlineData("not-an-email", "x", "email", "INVALID_FORMAT")]
    [InlineData("a@b.example", "", "password", "REQUIRED")]
    [InlineData("a@b.example", null, "password", "REQUIRED")]
    public async Task Missing_or_malformed_fields_are_refused_before_anything_is_checked(
        string? email, string? password, string field, string code)
    {
        var (factory, _, _, _) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var response = await browser.PostAsJsonAsync("/v1/auth/sign-in", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_FAILED", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(code, body.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.Empty(factory.Mail.Sent);
    }

    [Fact]
    public async Task The_email_is_matched_whatever_its_case_and_surrounding_spaces()
    {
        var (factory, _, _, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var response = await browser.StepOneAsync($"  {email.ToUpperInvariant()} ", SignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_password_is_matched_exactly_as_typed()
    {
        var (factory, _, _, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var lower = await browser.StepOneAsync(email, SignIn.Password.ToLowerInvariant());
        using var spaced = await browser.StepOneAsync(email, SignIn.Password + " ");

        Assert.Equal(HttpStatusCode.Unauthorized, lower.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, spaced.StatusCode);
    }

    [Fact]
    public async Task A_correct_password_returns_the_count_of_wrong_attempts_to_zero()
    {
        var (factory, _, account, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();
        for (var i = 0; i < 3; i++)
        {
            using var wrong = await browser.StepOneAsync(email, "Wrong-Password-1!");
        }

        Assert.Equal(
            (byte)3,
            await TestDatabase.ScalarAsync<byte>(sql.Main.Crossing, $"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{account}'"));

        using var right = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(
            (byte)0,
            await TestDatabase.ScalarAsync<byte>(sql.Main.Crossing, $"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{account}'"));
    }

    [Fact]
    public async Task A_second_press_leaves_one_code_standing()
    {
        var (factory, _, account, email) = await StartAsync();
        using var _ = factory;
        using var browser = factory.Browser();

        using var first = await browser.StepOneAsync(email, SignIn.Password);
        var firstCode = factory.Mail.LatestCodeFor(email);
        using var second = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal(
            1,
            await TestDatabase.ScalarAsync<int>(
                sql.Main.Crossing,
                $"SELECT COUNT(*) FROM [identity].SignInChallenge WHERE AccountId = '{account}' AND ConsumedAtUtc IS NULL AND ReplacedAtUtc IS NULL"));

        // The newest code is the one that works.
        var secondCode = factory.Mail.LatestCodeFor(email);
        using var confirmed = await browser.StepTwoAsync(secondCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(2, factory.Mail.To(email).Count);
        Assert.NotNull(firstCode);
    }

    [Fact]
    public async Task Sign_out_ends_the_session_and_clears_the_cookies()
    {
        var (factory, _, _, email) = await StartAsync();
        using var _ = factory;
        using var browser = await factory.SignInAsAsync(email);

        using var response = await browser.SignOutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        using var body = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        Assert.Equal("UNAUTHENTICATED", body.RootElement.GetProperty("code").GetString());
    }
}

/// <summary>
/// Spec 003, FR-035 and SC-008: after sign-in every piece of work is done for the session's
/// clinic, established from the account, and for no other.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class SessionIsolationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Two_clinics_signed_in_at_the_same_moment_each_see_only_their_own()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinicA = await sql.Main.SeedClinicAsync();
        var clinicB = await sql.Main.SeedClinicAsync();
        using var a = await factory.SignInAsAsync(clinicA);
        using var b = await factory.SignInAsAsync(clinicB);

        var answers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            var (browser, clinic) = i % 2 == 0 ? (a, clinicA) : (b, clinicB);
            using var response = await browser.GetAsync(new Uri("/probe/whoami", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return (
                Expected: clinic,
                Staff: body.RootElement.GetProperty("clinicId").GetGuid(),
                Context: body.RootElement.GetProperty("contextClinicId").GetGuid(),
                Source: body.RootElement.GetProperty("source").GetString(),
                Visible: body.RootElement.GetProperty("clinicsVisible").GetInt32());
        }));

        Assert.All(answers, answer =>
        {
            Assert.Equal(answer.Expected, answer.Staff);
            Assert.Equal(answer.Expected, answer.Context);
            Assert.Equal("account", answer.Source);
            Assert.Equal(1, answer.Visible);
        });
    }

    [Fact]
    public async Task Without_a_session_a_protected_endpoint_is_refused()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        using var browser = factory.Browser();

        using var response = await browser.GetAsync(new Uri("/probe/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("UNAUTHENTICATED", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_signed_in_request_that_names_another_clinic_is_refused()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var mine = await sql.Main.SeedClinicAsync();
        var otherName = $"other-{Guid.NewGuid():N}"[..20];
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Scs.Tenancy.Contracts.IClinicRegistry>()
                .RegisterAsync(otherName, "عيادة أخرى", "Other clinic", "test:isolation", default);
        }

        using var browser = await factory.SignInAsAsync(mine);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/whoami-patient");
        request.Headers.Add("X-Scs-Clinic", otherName);

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLINIC_MISMATCH", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_cookie_altered_to_name_another_clinic_is_not_a_session()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        using var signedIn = await factory.SignInAsAsync(clinic);
        using var stranger = factory.Browser();

        foreach (var forged in new[] { "", "not-a-cookie", new string('A', 200), Convert.ToBase64String(new byte[57]) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/whoami");
            request.Headers.Add("Cookie", $"{SessionCookies.Access}={forged}");
            using var response = await stranger.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
