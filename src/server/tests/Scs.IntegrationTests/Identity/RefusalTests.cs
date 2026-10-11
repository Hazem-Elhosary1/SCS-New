using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Identity;

/// <summary>Shared arrangement: one clinic, and an account in each state a refusal depends on.</summary>
internal sealed class RefusalWorld(SqlServerFixture sql, ApiFactory factory, Guid clinic) : IDisposable
{
    public ApiFactory Factory { get; } = factory;

    public Guid Clinic { get; } = clinic;

    public static async Task<RefusalWorld> CreateAsync(SqlServerFixture sql, Action<ApiFactory>? configure = null)
    {
        await sql.EnsureMigratedAsync();
        var factory = sql.CreateApi();
        configure?.Invoke(factory);
        return new RefusalWorld(sql, factory, await sql.Main.SeedClinicAsync());
    }

    public Task<(Guid Id, string Email)> AccountAsync(string state = StaffAccountStates.Active) =>
        Factory.CreateAccountAsync(Clinic, state);

    public async Task<(Guid Id, string Email)> AccountInAsync(string clinicState)
    {
        var clinic = await sql.Main.SeedClinicAsync(clinicState);
        return await Factory.CreateAccountAsync(clinic);
    }

    public async Task<(Guid Id, string Email)> LockedAccountAsync()
    {
        var account = await AccountAsync();
        await sql.ExecuteAsync(
            sql.Main.Crossing,
            $"UPDATE [identity].StaffAccount SET LockedUntilUtc = DATEADD(MINUTE, 15, SYSDATETIMEOFFSET()) WHERE Id = '{account.Id}'");
        return account;
    }

    public Task<T?> ScalarAsync<T>(string query) => TestDatabase.ScalarAsync<T>(sql.Main.Crossing, query);

    public Task ExecuteAsync(string statement) => sql.ExecuteAsync(sql.Main.Crossing, statement);

    public void Dispose() => Factory.Dispose();
}

/// <summary>
/// Spec 003, User Story 2 (SH-043, FR-010, FR-011): before a correct password, whatever is wrong
/// gets one answer. Nobody can learn from the sign-in form whether an email is registered.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SingleAnswerTests(SqlServerFixture sql)
{
    private const string Wrong = "Wrong-Password-1!";

    [Fact]
    public async Task Everything_wrong_before_a_correct_password_gets_one_identical_answer()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var active = await world.AccountAsync();
        var invited = await world.AccountAsync(StaffAccountStates.Invited);
        var disabled = await world.AccountAsync(StaffAccountStates.Disabled);
        var locked = await world.LockedAccountAsync();
        var suspended = await world.AccountInAsync("suspended");
        var cancelled = await world.AccountInAsync("cancelled");

        (string Case, string Email, string Password)[] attempts =
        [
            ("unknown email", $"nobody-{Guid.NewGuid():N}@clinic.example", SignIn.Password),
            ("wrong password", active.Email, Wrong),
            ("invited, any password", invited.Email, SignIn.Password),
            ("disabled, wrong password", disabled.Email, Wrong),
            ("locked, wrong password", locked.Email, Wrong),
            ("suspended clinic, wrong password", suspended.Email, Wrong),
            ("cancelled clinic, wrong password", cancelled.Email, Wrong),
        ];

        var answers = new List<string>();
        foreach (var attempt in attempts)
        {
            using var browser = world.Factory.Browser();
            using var response = await browser.StepOneAsync(attempt.Email, attempt.Password);
            var text = await response.Content.ReadAsStringAsync();
            using var body = JsonDocument.Parse(text);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{attempt.Case}: {(int)response.StatusCode}");
            Assert.Equal("INVALID_CREDENTIALS", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("البريد الإلكتروني أو كلمة المرور غير صحيحة.", body.RootElement.GetProperty("title").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"), $"{attempt.Case}: a cookie was set");

            var headers = string.Join(
                ";", response.Headers.Concat(response.Content.Headers).Select(h => h.Key).Where(k => k != "X-Trace-Id").Order());
            answers.Add($"{headers}|{text.Replace(body.RootElement.GetProperty("traceId").GetString()!, "", StringComparison.Ordinal)}");
        }

        Assert.Single(answers.Distinct());
        Assert.Empty(world.Factory.Mail.Sent);
    }

    [Fact]
    public async Task A_wrong_password_is_counted_and_recorded_and_an_unknown_email_is_recorded_without_the_email()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var active = await world.AccountAsync();
        var invited = await world.AccountAsync(StaffAccountStates.Invited);
        var unknown = $"nobody-{Guid.NewGuid():N}@clinic.example";
        var before = await world.ScalarAsync<int>("SELECT COUNT(*) FROM security.AnonymousSignInAttempt");
        using var browser = world.Factory.Browser();

        using var first = await browser.StepOneAsync(active.Email, Wrong);
        using var second = await browser.StepOneAsync(invited.Email, Wrong);
        using var third = await browser.StepOneAsync(unknown, Wrong);

        Assert.Equal((byte)1, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{active.Id}'"));
        Assert.Equal(
            1,
            await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{active.Id}' AND Kind = 'wrong-password' AND Category = 'security'"));

        // An invited account has no password: nothing to count, nothing to lock.
        Assert.Equal((byte)0, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{invited.Id}'"));

        Assert.Equal(before + 1, await world.ScalarAsync<int>("SELECT COUNT(*) FROM security.AnonymousSignInAttempt"));
        var columns = await TestDatabase.ScalarAsync<string>(
            sql.Main.Admin,
            "SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY column_id) FROM sys.columns WHERE object_id = OBJECT_ID('security.AnonymousSignInAttempt')");
        Assert.Equal("Id,EmailKey,IpAddress,UserAgent,TraceId,OccurredAtUtc", columns);
    }

    [Fact]
    public async Task The_application_can_add_an_anonymous_attempt_and_nothing_else()
    {
        await sql.EnsureMigratedAsync();

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "SELECT COUNT(*) FROM security.AnonymousSignInAttempt"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "DELETE FROM security.AnonymousSignInAttempt"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "UPDATE security.AnonymousSignInAttempt SET TraceId = TraceId"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Report, "SELECT COUNT(*) FROM security.AnonymousSignInAttempt"));
        Assert.True(await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM security.AnonymousSignInAttempt") >= 0);
    }
}

/// <summary>
/// Spec 003, SC-002: the single answer takes the same time whatever was wrong. Follows the
/// pattern of the isolation timing test of spec 002.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SingleAnswerTimingTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Requests = 150;

    [Fact]
    public async Task An_unknown_email_an_invited_account_and_a_wrong_password_cannot_be_told_apart_by_time()
    {
        // The request limit is not what is being measured.
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var invited = await world.AccountAsync(StaffAccountStates.Invited);

        // One account per attempt for "wrong password", so no lock ever comes into it.
        var actives = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            actives.Add((await world.AccountAsync()).Email);
        }

        using var browser = world.Factory.Browser();
        async Task<double> TimeAsync(string email)
        {
            var started = Stopwatch.GetTimestamp();
            using var response = await browser.StepOneAsync(email, "Wrong-Password-1!");
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            return elapsed;
        }

        for (var i = 0; i < 6; i++)
        {
            await TimeAsync($"warm-{Guid.NewGuid():N}@clinic.example");
            await TimeAsync(invited.Email);
        }

        // A comparison of times is a measurement: a busy machine can disturb one run, so the
        // claim is shown by any of three. Each run stays inside one minute of the request limit.
        var worst = double.MaxValue;
        for (var attempt = 1; attempt <= 3 && worst >= 0.05; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(61));
            }

            var unknown = new List<double>();
            var notActivated = new List<double>();
            var wrong = new List<double>();
            for (var i = 0; i < Requests / 5; i++)
            {
                // Alternate, so whatever else the machine is doing falls on all alike.
                unknown.Add(await TimeAsync($"nobody-{Guid.NewGuid():N}@clinic.example"));
                notActivated.Add(await TimeAsync(invited.Email));
                wrong.Add(await TimeAsync(actives[i % actives.Count]));
            }

            var means = new[] { Trimmed(unknown), Trimmed(notActivated), Trimmed(wrong) };
            worst = (means.Max() - means.Min()) / means.Max();
            output.WriteLine(
                $"Run {attempt}: unknown {means[0]:F1} ms, invited {means[1]:F1} ms, wrong password {means[2]:F1} ms; spread {worst:P2}.");
        }

        Assert.True(worst < 0.05, $"The answers differ in time by {worst:P2}.");
    }

    private static double Trimmed(List<double> values) => values.Order().Take(values.Count * 9 / 10).Average();
}

/// <summary>
/// Spec 003, User Story 3 (SH-041, FR-015 to FR-019): the lock, and the refusals a person is
/// told only after typing the correct password.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LockoutTests(SqlServerFixture sql)
{
    private const string Wrong = "Wrong-Password-1!";

    private static async Task<(HttpStatusCode Status, string Code, JsonElement Body)> AnswerAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body.RootElement.GetProperty("code").GetString()!, body.RootElement.Clone());
    }

    [Fact]
    public async Task The_fifth_wrong_password_locks_the_account_and_ends_its_sessions()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var signedIn = await world.Factory.SignInAsAsync(email);
        using var attacker = world.Factory.Browser();

        for (var i = 1; i <= 5; i++)
        {
            using var response = await attacker.StepOneAsync(email, Wrong);
            var (status, code, _) = await AnswerAsync(response);

            // The fifth looks exactly like the first four.
            Assert.Equal((HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"), (status, code));
        }

        Assert.NotNull(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal((byte)0, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'account-locked'"));
        Assert.Equal(5, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'wrong-password'"));

        // The session opened before the lock is over, at its very next request.
        using var after = await signedIn.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.Equal((HttpStatusCode.Unauthorized, "SESSION_ENDED"), ((await AnswerAsync(after)).Status, (await AnswerAsync(after)).Code));
        Assert.Equal("locked", await world.ScalarAsync<string>($"SELECT EndReason FROM [identity].Session WHERE AccountId = '{id}'"));
    }

    [Fact]
    public async Task While_locked_the_correct_password_is_told_so_and_a_wrong_one_is_not()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.StepOneAsync(email, Wrong);
        }

        var lockedUntil = await world.ScalarAsync<DateTimeOffset>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'");
        var mailBefore = world.Factory.Mail.Sent.Count;

        using var right = await browser.StepOneAsync(email, SignIn.Password);
        var (status, code, body) = await AnswerAsync(right);

        Assert.Equal((HttpStatusCode.Forbidden, "ACCOUNT_LOCKED"), (status, code));
        Assert.Equal(lockedUntil.UtcDateTime, body.GetProperty("lockedUntil").GetDateTimeOffset().UtcDateTime, TimeSpan.FromSeconds(1));
        Assert.InRange((lockedUntil - world.Factory.Clock.GetUtcNow()).TotalMinutes, 14, 15.1);
        Assert.Equal(mailBefore, world.Factory.Mail.Sent.Count);

        using var wrongAgain = await browser.StepOneAsync(email, Wrong);
        Assert.Equal("INVALID_CREDENTIALS", (await AnswerAsync(wrongAgain)).Code);

        // A wrong password while locked moves nothing.
        Assert.Equal(lockedUntil, await world.ScalarAsync<DateTimeOffset>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Equal((byte)0, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task The_lock_opens_after_fifteen_minutes()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "60"));
        var (_, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.StepOneAsync(email, Wrong);
        }

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(14));
        using var early = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal("ACCOUNT_LOCKED", (await AnswerAsync(early)).Code);

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(1.1));
        using var onTime = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, onTime.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlocking_or_clearing_the_lock_opens_it_at_once(bool unlock)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (id, email) = await world.LockedAccountAsync();
        using var browser = world.Factory.Browser();

        await world.Factory.ForClinicAsync(world.Clinic, services =>
        {
            var accounts = services.GetRequiredService<IStaffAccounts>();
            return unlock ? accounts.UnlockAsync(id, default) : accounts.ClearLockAsync(id, default);
        });

        using var response = await browser.StepOneAsync(email, SignIn.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_attempts_more_than_fifteen_minutes_apart_do_not_add_up()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "60"));
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        for (var i = 0; i < 4; i++)
        {
            using var wrong = await browser.StepOneAsync(email, Wrong);
        }

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(15.1));
        using var later = await browser.StepOneAsync(email, Wrong);

        // That one counts as the first; the account is not locked.
        Assert.Equal((byte)1, await world.ScalarAsync<byte>($"SELECT FailedAttempts FROM [identity].StaffAccount WHERE Id = '{id}'"));
        Assert.Null(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task Wrong_attempts_less_than_fifteen_minutes_apart_do_add_up()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "60"));
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.StepOneAsync(email, Wrong);
            world.Factory.Clock.Advance(TimeSpan.FromMinutes(14));
        }

        Assert.NotNull(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task Twenty_wrong_passwords_at_the_same_moment_lock_once_and_lose_no_count()
    {
        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var (id, email) = await world.AccountAsync();
        using var browser = world.Factory.Browser();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => browser.StepOneAsync(email, Wrong)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'account-locked'"));
        Assert.Equal(20, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{id}' AND Kind = 'wrong-password'"));
        Assert.NotNull(await world.ScalarAsync<object>($"SELECT LockedUntilUtc FROM [identity].StaffAccount WHERE Id = '{id}'"));
    }
}

/// <summary>Spec 003, FR-009: the order of checks after a correct password.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefusalOrderTests(SqlServerFixture sql)
{
    private static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return $"{(int)response.StatusCode} {body.RootElement.GetProperty("code").GetString()}";
    }

    [Fact]
    public async Task A_disabled_account_is_told_so_only_with_the_correct_password()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync(StaffAccountStates.Disabled);
        using var browser = world.Factory.Browser();

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal("403 ACCOUNT_DISABLED", await CodeAsync(response));
        Assert.Empty(world.Factory.Mail.Sent);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("cancelled")]
    public async Task A_clinic_that_may_not_operate_is_told_so_only_with_the_correct_password(string state)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountInAsync(state);
        using var browser = world.Factory.Browser();

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal("403 CLINIC_SUSPENDED", await CodeAsync(response));
        Assert.Empty(world.Factory.Mail.Sent);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("trial")]
    [InlineData("read-only")]
    public async Task A_clinic_in_any_other_state_lets_its_team_in(string state)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountInAsync(state);
        using var browser = world.Factory.Browser();

        using var response = await browser.StepOneAsync(email, SignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_lock_is_told_before_the_account_state_and_the_account_before_the_clinic()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        var lockedAndDisabled = await world.LockedAccountAsync();
        await world.ExecuteAsync($"UPDATE [identity].StaffAccount SET State = 'disabled' WHERE Id = '{lockedAndDisabled.Id}'");
        using var first = await browser.StepOneAsync(lockedAndDisabled.Email, SignIn.Password);
        Assert.Equal("403 ACCOUNT_LOCKED", await CodeAsync(first));

        var disabledInSuspended = await world.AccountInAsync("suspended");
        await world.ExecuteAsync($"UPDATE [identity].StaffAccount SET State = 'disabled' WHERE Id = '{disabledInSuspended.Id}'");
        using var second = await browser.StepOneAsync(disabledInSuspended.Email, SignIn.Password);
        Assert.Equal("403 ACCOUNT_DISABLED", await CodeAsync(second));
    }
}
