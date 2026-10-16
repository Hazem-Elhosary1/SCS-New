using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Mail;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 004, User Story 1: a person who forgot their password asks for a link, chooses a new
/// password from it, and signs in — without anyone's help.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordResetTests(SqlServerFixture sql)
{
    private static Task<RefusalWorld> WorldAsync(SqlServerFixture sql) =>
        RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));

    [Fact]
    public async Task A_forgotten_password_is_replaced_from_an_emailed_link_and_the_new_one_signs_in()
    {
        using var world = await WorldAsync(sql);
        var account = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        using var asked = await browser.ForgotAsync(account.Email);
        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        Assert.Equal("", await asked.Content.ReadAsStringAsync());
        Assert.Empty(world.Factory.Mail.Sent);

        await world.Factory.RunAccountEmailJobAsync(world.Clinic);

        var message = Assert.Single(world.Factory.Mail.To(account.Email));
        Assert.Contains($"{ApiFactory.StaffApp}/set-password#t=", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("إعادة تعيين كلمة المرور", message.Subject, StringComparison.Ordinal);
        Assert.Contains("30", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("عيادة تجريبية", message.TextBody, StringComparison.Ordinal);
        var token = world.Factory.Mail.LatestLinkTokenFor(account.Email);

        // Opening the link shows whose it is and changes nothing, however often.
        for (var i = 0; i < 2; i++)
        {
            using var opened = await browser.InspectLinkAsync(token);
            var (status, _, body) = await opened.ReadAsync();
            Assert.Equal(200, status);
            Assert.Equal("reset", body.GetProperty("kind").GetString());
            Assert.Equal(account.Email, body.GetProperty("email").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("clinicNameAr").ValueKind);
        }

        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        var done = await set.ReadAsync();
        Assert.Equal(200, done.Status);
        Assert.Equal("reset", done.Body.GetProperty("kind").GetString());
        Assert.False(set.Headers.Contains("Set-Cookie"), "A reset must not open a session.");

        using var session = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);

        // The old password is now simply wrong; the new one works with both steps.
        using var old = await browser.StepOneAsync(account.Email, SignIn.Password);
        Assert.Equal("INVALID_CREDENTIALS", (await old.ReadAsync()).Code);
        using var signedIn = await world.Factory.SignInAsAsync(account.Email, Passwords.New);

        // The link worked once.
        using var again = await browser.SetWithLinkAsync(token, "Another-Pass-11!");
        Assert.Equal((410, "LINK_INVALID"), ((int)again.StatusCode, (await again.ReadAsync()).Code));
        using var reopened = await browser.InspectLinkAsync(token);
        Assert.Equal((410, "LINK_INVALID"), ((int)reopened.StatusCode, (await reopened.ReadAsync()).Code));

        // The owner is told, by a message with no link and no password in it.
        await world.Factory.RunAccountEmailJobAsync(world.Clinic);
        var notice = world.Factory.Mail.To(account.Email).Single(m => m.Subject.Contains("تم تغيير كلمة مرورك", StringComparison.Ordinal));
        Assert.DoesNotContain("#t=", notice.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Passwords.New, notice.TextBody + notice.HtmlBody, StringComparison.Ordinal);

        Assert.Equal(
            "reset-requested:activity,reset-completed:security",
            await world.ScalarAsync<string>(
                $"""
                 SELECT STRING_AGG(Kind + ':' + Category, ',') WITHIN GROUP (ORDER BY OccurredAtUtc)
                 FROM [identity].SignInEvent WHERE AccountId = '{account.Id}' AND Kind LIKE 'reset-%'
                 """));
    }

    [Fact]
    public async Task Completing_a_reset_lifts_a_lock_and_ends_every_session_of_the_account()
    {
        using var world = await WorldAsync(sql);
        var account = await world.AccountAsync();
        using var first = await world.Factory.SignInAsAsync(account.Email);
        using var second = await world.Factory.SignInAsAsync(account.Email);

        await world.ExecuteAsync(
            $"UPDATE [identity].StaffAccount SET LockedUntilUtc = DATEADD(MINUTE, 15, SYSDATETIMEOFFSET()), FailedAttempts = 3, SpentCodes = 2 WHERE Id = '{account.Id}'");

        // A locked account is still an active one: it gets its link (SH-049).
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, account.Email);
        using var anyone = world.Factory.Browser();
        using var set = await anyone.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        foreach (var browser in new[] { first, second })
        {
            using var refused = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
            Assert.Equal("SESSION_ENDED", (await refused.ReadAsync()).Code);
        }

        Assert.Equal(
            2,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{account.Id}' AND EndReason = 'password-reset'"));
        Assert.Equal(
            "0|0|",
            await world.ScalarAsync<string>(
                $"SELECT CONCAT(FailedAttempts, '|', SpentCodes, '|', LockedUntilUtc) FROM [identity].StaffAccount WHERE Id = '{account.Id}'"));

        // No waiting for the lock to run out.
        using var signedIn = await world.Factory.SignInAsAsync(account.Email, Passwords.New);
    }

    [Fact]
    public async Task The_reset_stands_when_the_notice_cannot_be_sent_and_one_failed_email_does_not_stop_another()
    {
        using var world = await WorldAsync(sql);
        var unlucky = await world.AccountAsync();
        var lucky = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        var token = await world.Factory.ResetTokenForAsync(world.Clinic, unlucky.Email);

        // From now on the mail server refuses one of the two addresses.
        world.Factory.Mail.FailingFor[unlucky.Email] = true;
        using var set = await browser.SetWithLinkAsync(token, Passwords.New);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        using (await browser.ForgotAsync(lucky.Email)) { }

        // The job reports the failure so that it is tried again, and still serves the other account.
        await Assert.ThrowsAnyAsync<Exception>(() => world.Factory.RunAccountEmailJobAsync(world.Clinic));
        Assert.Equal(1, world.Factory.Mail.LinksSentTo(lucky.Email));
        Assert.DoesNotContain(world.Factory.Mail.To(unlucky.Email), m => m.Subject.Contains("تم تغيير كلمة مرورك", StringComparison.Ordinal));

        // When the server takes the mail again, the waiting notice goes out — and the reset
        // stood all along: the new password signs in.
        world.Factory.Mail.FailingFor.Clear();
        await world.Factory.RunAccountEmailJobAsync(world.Clinic);
        Assert.Contains(world.Factory.Mail.To(unlucky.Email), m => m.Subject.Contains("تم تغيير كلمة مرورك", StringComparison.Ordinal));
        using var signedIn = await world.Factory.SignInAsAsync(unlucky.Email, Passwords.New);
    }

    [Theory]
    [InlineData(null, "REQUIRED")]
    [InlineData("", "REQUIRED")]
    [InlineData("not-an-email", "INVALID_FORMAT")]
    public async Task An_empty_or_malformed_email_is_refused_by_its_shape_alone(string? email, string code)
    {
        using var world = await WorldAsync(sql);
        using var browser = world.Factory.Browser();

        using var response = await browser.ForgotAsync(email);
        var (status, problem, body) = await response.ReadAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, problem));
        Assert.Equal(code, body.GetProperty("errors").GetProperty("email")[0].GetString());
    }
}

/// <summary>
/// Spec 004, User Story 2 (SH-043, FR-003 to FR-007): asking for a link gives one answer, in one
/// time, whatever the address — and mail goes only to an active account, within the caps.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ForgotPasswordAnswerTests(SqlServerFixture sql, ITestOutputHelper output)
{
    /// <summary>Six cases a round, and every run stays inside one minute of the request limit (120).</summary>
    private const int PerCase = 18;

    [Fact]
    public async Task Every_request_gets_one_identical_answer_and_only_an_active_account_gets_mail()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var active = await world.AccountAsync();
        var invited = await world.AccountAsync(StaffAccountStates.Invited);
        var disabled = await world.AccountAsync(StaffAccountStates.Disabled);
        var locked = await world.LockedAccountAsync();
        var failing = await world.AccountAsync();
        world.Factory.Mail.FailingFor[failing.Email] = true;
        using var browser = world.Factory.Browser();

        (string Case, string Email)[] requests =
        [
            ("unknown", $"nobody-{Guid.NewGuid():N}@clinic.example"),
            ("active", active.Email),
            ("active, asked again at once", active.Email),
            ("invited", invited.Email),
            ("disabled", disabled.Email),
            ("locked", locked.Email),
            ("mail failing", failing.Email),
        ];

        var answers = new List<string>();
        foreach (var request in requests)
        {
            using var response = await browser.ForgotAsync(request.Email);
            Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{request.Case}: {(int)response.StatusCode}");
            Assert.False(response.Headers.Contains("Set-Cookie"), $"{request.Case}: a cookie was set");

            var headers = string.Join(
                ";", response.Headers.Concat(response.Content.Headers).Select(h => h.Key).Where(k => k != "X-Trace-Id").Order());
            answers.Add($"{headers}|{await response.Content.ReadAsStringAsync()}");
        }

        Assert.Single(answers.Distinct());

        await Assert.ThrowsAnyAsync<Exception>(() => world.Factory.RunAccountEmailJobAsync(world.Clinic));

        // The active account once (asked twice within the minute), the locked one once, nobody else.
        Assert.Equal(1, world.Factory.Mail.LinksSentTo(active.Email));
        Assert.Equal(1, world.Factory.Mail.LinksSentTo(locked.Email));
        Assert.Equal(2, world.Factory.Mail.Sent.Count);

        // An unknown address leaves no trace; a known one is recorded for its clinic.
        Assert.Equal(
            2,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{active.Id}' AND Kind = 'reset-requested'"));
    }

    [Fact]
    public async Task The_answer_takes_the_same_time_whatever_the_address()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var invited = await world.AccountAsync(StaffAccountStates.Invited);
        var disabled = await world.AccountAsync(StaffAccountStates.Disabled);
        var repeated = await world.AccountAsync();
        var failing = await world.AccountAsync();
        world.Factory.Mail.FailingFor[failing.Email] = true;

        // A different active account each time, so "active" is never also "asked again".
        var actives = new List<string>();
        for (var i = 0; i < PerCase * 3; i++)
        {
            actives.Add((await world.AccountAsync()).Email);
        }

        using var browser = world.Factory.Browser();
        async Task<double> TimeAsync(string email)
        {
            var started = Stopwatch.GetTimestamp();
            using var response = await browser.ForgotAsync(email);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return elapsed;
        }

        for (var i = 0; i < 3; i++)
        {
            await TimeAsync($"warm-{Guid.NewGuid():N}@clinic.example");
            await TimeAsync(repeated.Email);
        }

        // A comparison of times is a measurement: a busy machine can disturb one run, so the
        // claim is shown by any of three. Each run stays inside one minute of the request limit.
        var worst = double.MaxValue;
        var fastest = double.MaxValue;
        var next = 0;
        for (var attempt = 1; attempt <= 3 && worst >= 0.05; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(61));

            // The six cases the criterion names (SC-002).
            var times = new Dictionary<string, List<double>>(StringComparer.Ordinal)
            {
                ["unknown"] = [],
                ["active"] = [],
                ["disabled"] = [],
                ["never activated"] = [],
                ["asked again within a minute"] = [],
                ["mail failing"] = [],
            };

            for (var i = 0; i < PerCase; i++)
            {
                // Alternate, so whatever else the machine is doing falls on all alike.
                times["unknown"].Add(await TimeAsync($"nobody-{Guid.NewGuid():N}@clinic.example"));
                times["active"].Add(await TimeAsync(actives[next++]));
                times["disabled"].Add(await TimeAsync(disabled.Email));
                times["never activated"].Add(await TimeAsync(invited.Email));
                times["asked again within a minute"].Add(await TimeAsync(repeated.Email));
                times["mail failing"].Add(await TimeAsync(failing.Email));
            }

            fastest = Math.Min(fastest, times.Values.SelectMany(v => v).Min());
            var means = times.ToDictionary(pair => pair.Key, pair => Trimmed(pair.Value), StringComparer.Ordinal);
            worst = (means.Values.Max() - means.Values.Min()) / means.Values.Max();
            output.WriteLine(
                $"Run {attempt}: {string.Join(", ", means.Select(m => $"{m.Key} {m.Value:F1} ms"))}; spread {worst:P2}.");
        }

        Assert.True(worst < 0.05, $"The answers differ in time by {worst:P2}.");
        Assert.True(fastest >= 295, $"An answer came after {fastest:F0} ms, sooner than the floor.");
    }

    private static double Trimmed(List<double> values) => values.Order().Take(values.Count * 9 / 10).Average();
}

/// <summary>Spec 004, FR-005 and FR-007: how often a reset email may go to one account.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResetEmailLimitTests(SqlServerFixture sql)
{
    [Fact]
    public async Task One_reset_email_a_minute_and_five_an_hour_per_account_and_the_last_link_keeps_working()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var account = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        async Task AskAsync()
        {
            using var asked = await browser.ForgotAsync(account.Email);
            Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
            await world.Factory.RunAccountEmailJobAsync(world.Clinic);
        }

        await AskAsync();
        var first = world.Factory.Mail.LatestLinkTokenFor(account.Email);

        // Inside the minute: the same answer, no email, and the first link is untouched.
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await AskAsync();
        Assert.Equal(1, world.Factory.Mail.LinksSentTo(account.Email));
        using (var stillLive = await browser.InspectLinkAsync(first))
        {
            Assert.Equal(HttpStatusCode.OK, stillLive.StatusCode);
        }

        // Four more, each a little over a minute after the one before: five in all.
        for (var i = 0; i < 4; i++)
        {
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            await AskAsync();
        }

        Assert.Equal(5, world.Factory.Mail.LinksSentTo(account.Email));
        var fifth = world.Factory.Mail.LatestLinkTokenFor(account.Email);

        // The sixth within the hour sends nothing, and the fifth link still works.
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await AskAsync();
        Assert.Equal(5, world.Factory.Mail.LinksSentTo(account.Email));
        using (var fifthLive = await browser.InspectLinkAsync(fifth))
        {
            Assert.Equal(HttpStatusCode.OK, fifthLive.StatusCode);
        }

        // A newer link replaced the first long ago.
        using (var replaced = await browser.InspectLinkAsync(first))
        {
            Assert.Equal("LINK_INVALID", (await replaced.ReadAsync()).Code);
        }

        Assert.Equal(
            "sent:5,skipped-limit:2",
            await world.ScalarAsync<string>(
                $"""
                 SELECT STRING_AGG(o, ',') WITHIN GROUP (ORDER BY o) FROM (
                     SELECT CONCAT(Outcome, ':', COUNT(*)) AS o FROM [identity].AccountEmailRequest
                     WHERE AccountId = '{account.Id}' GROUP BY Outcome) x
                 """));

        // Once the first of the five is more than an hour old, one may be sent again.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(56));
        await AskAsync();
        Assert.Equal(6, world.Factory.Mail.LinksSentTo(account.Email));
    }

    [Fact]
    public async Task Many_requests_at_once_produce_one_email_and_one_live_link()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var account = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => browser.ForgotAsync(account.Email)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
        Array.ForEach(responses, r => r.Dispose());

        // The job server may also run the job twice at the same moment.
        await Task.WhenAll(
            world.Factory.RunAccountEmailJobAsync(world.Clinic),
            world.Factory.RunAccountEmailJobAsync(world.Clinic));

        Assert.Equal(1, world.Factory.Mail.LinksSentTo(account.Email));
        Assert.Equal(
            1,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].PasswordLink WHERE AccountId = '{account.Id}' AND UsedAtUtc IS NULL AND ReplacedAtUtc IS NULL"));
        Assert.Equal(
            0,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].AccountEmailRequest WHERE AccountId = '{account.Id}' AND HandledAtUtc IS NULL"));
    }

    [Fact]
    public async Task Asking_shares_the_sign_in_request_limit_and_says_when_to_try_again()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var account = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        // Sign-in attempts and link requests are counted together (ten a minute per address).
        for (var i = 0; i < 4; i++)
        {
            using (await browser.StepOneAsync($"nobody-{Guid.NewGuid():N}@clinic.example", "Wrong-Password-1!")) { }
        }

        for (var i = 0; i < 6; i++)
        {
            using var allowed = await browser.ForgotAsync(account.Email);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var refused = await browser.ForgotAsync(account.Email);
        Assert.Equal((429, "RATE_LIMITED"), ((int)refused.StatusCode, (await refused.ReadAsync()).Code));
        Assert.True(int.Parse(refused.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) is > 0 and <= 60);
    }

    [Fact]
    public async Task A_link_whose_email_cannot_be_composed_is_not_stored_and_holds_up_no_other()
    {
        // Spec 094, T051: an email that can never be whole is "not sent", like one the mail
        // server refused — nothing is kept of a link nobody received, and the next account's
        // link goes out.
        using var world = await RefusalWorld.CreateAsync(sql);
        var unlucky = await world.AccountAsync();
        var lucky = await world.AccountAsync();

        var results = await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var links = services.GetRequiredService<Scs.Identity.Infrastructure.PasswordLinks>();
            var renderer = services.GetRequiredService<EmailRenderer>();

            var refused = await links.SendAsync(
                unlucky.Id, "reset", enforceResetCaps: false,
                address => renderer.Render(new EmailContent(unlucky.Email, "ar", "موضوع", "سطر", "عنوان", [new EmailButton("افتح", "http://elsewhere.example/" + address)])),
                default);
            var sent = await links.SendAsync(
                lucky.Id, "reset", enforceResetCaps: false,
                address => renderer.Render(new EmailContent(lucky.Email, "ar", "موضوع", "سطر", "عنوان", [new EmailButton("افتح", address)])),
                default);
            return (refused.ToString(), sent.ToString());
        });

        Assert.Equal(("NotSent", "Sent"), results);
        Assert.Empty(world.Factory.Mail.To(unlucky.Email));
        Assert.Single(world.Factory.Mail.To(lucky.Email));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].PasswordLink WHERE AccountId = '{unlucky.Id}'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].PasswordLink WHERE AccountId = '{lucky.Id}'"));
    }
}
