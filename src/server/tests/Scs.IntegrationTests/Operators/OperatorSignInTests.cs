using System.Diagnostics;
using System.Net;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 010, User Stories 1 to 4: an operator signs in with email, password and a code by
/// email; nothing is revealed before a correct password; wrong tries lock the account; the
/// code is short-lived, limited and can be sent again.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorSignInTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_correct_password_opens_nothing_and_sends_a_code_and_the_code_opens_the_session()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        using var first = await browser.OpsStepOneAsync($"  {world.Email.ToUpperInvariant()} ");
        var one = await first.ReadAsync();
        Assert.Equal(200, one.Status);
        Assert.Equal(world.Email, one.Body.GetProperty("email").GetString());
        Assert.Empty(await world.SessionsAsync());

        // No session yet: the console's own address still says "sign in".
        using var early = await browser.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, early.StatusCode);

        var message = Assert.Single(world.Factory.Mail.To(world.Email));
        Assert.Contains("Smart Clinic OS", message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);

        using var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(world.Email));
        var two = await second.ReadAsync();
        Assert.Equal(200, two.Status);
        Assert.Equal("سارة أحمد", two.Body.GetProperty("operator").GetProperty("fullName").GetString());
        Assert.Equal("مسؤول عام", two.Body.GetProperty("operator").GetProperty("roleName").GetString());
        Assert.Equal(30, two.Body.GetProperty("idleMinutes").GetInt32());

        var session = Assert.Single(await world.SessionsAsync());
        Assert.Null(session.EndedAtUtc);
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.LoginSucceeded));

        using var now = await browser.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, now.StatusCode);
    }

    [Fact]
    public async Task What_is_wrong_before_a_correct_password_gets_one_answer_whatever_it_is()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, invited) = await world.Factory.CreateOperatorAsync(OperatorStates.Invited);
        var (_, suspended) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended);
        using var browser = world.Factory.Browser();

        var answers = new List<(int Status, string? Code, string Body)>();
        foreach (var (email, password) in new[]
                 {
                     ("nobody@heta.example", OpsWorld.Password),
                     (world.Email, "Wrong-Horse-9!"),
                     (invited, OpsWorld.Password),
                     (suspended, "Wrong-Horse-9!"),
                     (world.Email, new string('x', 201)),
                 })
        {
            using var response = await browser.OpsStepOneAsync(email, password);
            var read = await response.ReadAsync();
            answers.Add((read.Status, read.Code, read.Body.GetProperty("title").GetString()!));
        }

        Assert.All(answers, a => Assert.Equal((401, "INVALID_CREDENTIALS"), (a.Status, a.Code)));
        Assert.Single(answers.Select(a => a.Body).Distinct());
        Assert.Empty(world.Factory.Mail.Sent);

        // A wrong password is recorded; an unknown email leaves a line with no account and no address.
        Assert.NotEmpty(await world.RecordsAsync(SignInRecordKinds.LoginFailed));
        Assert.DoesNotContain("nobody@heta.example", world.Factory.Logs.All, StringComparison.Ordinal);

        // Only the correct password of a suspended account is told why.
        using var told = await browser.OpsStepOneAsync(suspended);
        Assert.Equal((403, "ACCOUNT_DISABLED"), ((await told.ReadAsync()) is var t ? (t.Status, t.Code) : default));
        Assert.Empty(world.Factory.Mail.Sent);
    }

    [Fact]
    public async Task An_unknown_email_takes_as_long_as_a_wrong_password()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        async Task<double> MedianAsync(string email)
        {
            var times = new List<double>();
            for (var i = 0; i < 4; i++)
            {
                var watch = Stopwatch.StartNew();
                using var response = await browser.OpsStepOneAsync(email, "Wrong-Horse-9!");
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            return times.Order().ElementAt(times.Count / 2);
        }

        var (_, other) = await world.Factory.CreateOperatorAsync();
        var unknown = await MedianAsync("nobody@heta.example");
        var wrong = await MedianAsync(other);

        // Both wait until the same moment: the floor, and little beyond it.
        Assert.InRange(unknown, 290, 1500);
        Assert.InRange(wrong, 290, 1500);
        Assert.InRange(Math.Abs(unknown - wrong), 0, 150);
    }

    [Fact]
    public async Task The_fifth_wrong_password_locks_for_fifteen_minutes_and_only_the_right_one_is_told()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
            Assert.Equal("INVALID_CREDENTIALS", (await wrong.ReadAsync()).Code);
        }

        Assert.Single(await world.RecordsAsync(SignInRecordKinds.AccountLocked));

        using var right = await browser.OpsStepOneAsync(world.Email);
        var locked = await right.ReadAsync();
        Assert.Equal((403, "ACCOUNT_LOCKED"), (locked.Status, locked.Code));
        var until = locked.Body.GetProperty("lockedUntil").GetDateTimeOffset();
        Assert.InRange((until - world.Factory.Clock.GetUtcNow()).TotalMinutes, 14, 15.1);
        Assert.Empty(world.Factory.Mail.Sent);

        // More wrong tries do not move the lock.
        using var more = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
        using var again = await browser.OpsStepOneAsync(world.Email);
        Assert.Equal(until, (await again.ReadAsync()).Body.GetProperty("lockedUntil").GetDateTimeOffset());

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(15.1));
        using var signedIn = await world.SignInAsync();
        var failed = await world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Single(o => o.Id == world.OperatorId).FailedAttempts));
        Assert.Equal(0, failed);
    }

    [Fact]
    public async Task Wrong_tries_older_than_the_window_are_forgotten()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        for (var i = 0; i < 4; i++)
        {
            using var wrong = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
        }

        world.Factory.Clock.Advance(TimeSpan.FromMinutes(16));
        using var fifth = await browser.OpsStepOneAsync(world.Email, "Wrong-Horse-9!");
        Assert.Empty(await world.RecordsAsync(SignInRecordKinds.AccountLocked));
    }

    [Fact]
    public async Task A_code_is_refused_when_wrong_expired_used_replaced_or_from_another_browser()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();
        using var first = await browser.OpsStepOneAsync(world.Email);
        var code = world.Factory.Mail.LatestCodeFor(world.Email);
        var wrongCode = code == "000000" ? "000001" : "000000";

        using var wrong = await browser.OpsStepTwoAsync(wrongCode);
        var read = await wrong.ReadAsync();
        Assert.Equal((401, "CODE_INVALID"), (read.Status, read.Code));
        Assert.Equal(4, read.Body.GetProperty("attemptsLeft").GetInt32());
        Assert.Single(await world.RecordsAsync(SignInRecordKinds.OtpFailed));

        // The right code from a browser that did not ask for it opens nothing.
        using var stranger = world.Factory.Browser();
        using var elsewhere = await stranger.OpsStepTwoAsync(code);
        Assert.Equal("STEP_EXPIRED", (await elsewhere.ReadAsync()).Code);

        // A resend before its minute is refused; after it, the earlier code stops working.
        using var early = await browser.OpsPostAsync("/v1/ops/auth/code/resend");
        var tooEarly = await early.ReadAsync();
        Assert.Equal((409, "RESEND_TOO_EARLY"), (tooEarly.Status, tooEarly.Code));

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var resent = await browser.OpsPostAsync("/v1/ops/auth/code/resend");
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var newer = world.Factory.Mail.LatestCodeFor(world.Email);

        using var old = await browser.OpsStepTwoAsync(code);
        if (code != newer)
        {
            Assert.NotEqual(HttpStatusCode.OK, old.StatusCode);
        }

        // Ten minutes after the password the sign-in is over, whatever was resent.
        using var another = world.Factory.Browser();
        using var again = await another.OpsStepOneAsync(world.Email);
        var third = world.Factory.Mail.LatestCodeFor(world.Email);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(10.1));
        using var late = await another.OpsStepTwoAsync(third);
        Assert.Equal("STEP_EXPIRED", (await late.ReadAsync()).Code);

        // A code works once.
        using var fresh = world.Factory.Browser();
        using var start = await fresh.OpsStepOneAsync(world.Email);
        var good = world.Factory.Mail.LatestCodeFor(world.Email);
        using var ok = await fresh.OpsStepTwoAsync(good);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var twice = await fresh.OpsStepTwoAsync(good);
        Assert.Equal("STEP_EXPIRED", (await twice.ReadAsync()).Code);
    }

    [Fact]
    public async Task Five_wrong_codes_end_the_sign_in_and_three_spent_codes_lock_the_account()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        for (var spent = 0; spent < 3; spent++)
        {
            using var browser = world.Factory.Browser();
            using var first = await browser.OpsStepOneAsync(world.Email);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var code = world.Factory.Mail.LatestCodeFor(world.Email);
            var wrongCode = code == "000000" ? "000001" : "000000";

            for (var i = 0; i < 4; i++)
            {
                using var wrong = await browser.OpsStepTwoAsync(wrongCode);
                Assert.Equal("CODE_INVALID", (await wrong.ReadAsync()).Code);
            }

            using var fifth = await browser.OpsStepTwoAsync(wrongCode);
            Assert.Equal("STEP_EXPIRED", (await fifth.ReadAsync()).Code);

            // The right code after the tries are spent opens nothing.
            using var late = await browser.OpsStepTwoAsync(code);
            Assert.Equal("STEP_EXPIRED", (await late.ReadAsync()).Code);
        }

        Assert.Single(await world.RecordsAsync(SignInRecordKinds.AccountLocked));
        using var locked = await world.Factory.Browser().OpsStepOneAsync(world.Email);
        Assert.Equal("ACCOUNT_LOCKED", (await locked.ReadAsync()).Code);
    }

    [Fact]
    public async Task A_fourth_new_code_is_refused_a_code_that_cannot_be_sent_opens_nothing_and_cancel_ends_the_step()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        // The password is right and the mail server is down: nothing is open.
        world.Factory.Mail.Failing = true;
        using var notSent = await browser.OpsStepOneAsync(world.Email);
        var refused = await notSent.ReadAsync();
        Assert.Equal((503, "SIGN_IN_CODE_NOT_SENT"), (refused.Status, refused.Code));
        Assert.Equal(0, await world.Factory.OpsAsync(db => Task.FromResult(db.Challenges.Count(c => c.OperatorId == world.OperatorId))));
        world.Factory.Mail.Failing = false;

        using var first = await browser.OpsStepOneAsync(world.Email);
        for (var i = 0; i < 3; i++)
        {
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            using var resent = await browser.OpsPostAsync("/v1/ops/auth/code/resend");
            Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        }

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var fourth = await browser.OpsPostAsync("/v1/ops/auth/code/resend");
        Assert.Equal("RESEND_LIMIT_REACHED", (await fourth.ReadAsync()).Code);

        // A resend that cannot be sent leaves the earlier code good.
        using var other = world.Factory.Browser();
        using var start = await other.OpsStepOneAsync(world.Email);
        var code = world.Factory.Mail.LatestCodeFor(world.Email);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        world.Factory.Mail.Failing = true;
        using var failed = await other.OpsPostAsync("/v1/ops/auth/code/resend");
        Assert.Equal("SIGN_IN_CODE_NOT_RESENT", (await failed.ReadAsync()).Code);
        world.Factory.Mail.Failing = false;

        // Leaving the step makes its code useless.
        using var cancelled = await other.OpsPostAsync("/v1/ops/auth/code/cancel");
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        using var after = await other.OpsStepTwoAsync(code);
        Assert.Equal("STEP_EXPIRED", (await after.ReadAsync()).Code);
    }

    [Fact]
    public async Task An_account_suspended_between_the_password_and_the_code_is_refused_at_the_code()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();
        using var first = await browser.OpsStepOneAsync(world.Email);
        await world.Factory.OpsSqlAsync($"UPDATE [ops].[Operator] SET [State] = 'suspended' WHERE [Id] = '{world.OperatorId}'");

        using var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(world.Email));
        Assert.Equal((403, "ACCOUNT_DISABLED"), ((await second.ReadAsync()) is var r ? (r.Status, r.Code) : default));
        Assert.Empty(await world.SessionsAsync());
    }

    [Fact]
    public async Task The_form_is_checked_before_anything_is_looked_up()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = world.Factory.Browser();

        using var empty = await browser.OpsPostAsync("/v1/ops/auth/sign-in", new { email = "", password = "" });
        var read = await empty.ReadAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (read.Status, read.Code));

        using var malformed = await browser.OpsPostAsync("/v1/ops/auth/sign-in", new { email = "not-an-address", password = "x" });
        Assert.Equal(400, (await malformed.ReadAsync()).Status);
    }
}

/// <summary>
/// Spec 010, SC-003: over a hundred tries each, an unknown email, a wrong password, and a wrong
/// password for a locked, a suspended and an invited account cannot be told apart by their time.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorAnswerTimingTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Tries = 100;

    [Fact]
    public async Task The_five_answers_take_the_same_time_over_a_hundred_tries_each()
    {
        // One platform for each answer, so that each keeps under the request limit of an
        // address (120 a minute here) while the five are measured side by side.
        var cases = new[] { "unknown", "wrong", "locked", "suspended", "invited" };
        var measured = await Task.WhenAll(cases.Select(MeasureAsync));

        var medians = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (name, times) in measured)
        {
            var sorted = times.Order().ToList();
            medians[name] = sorted[Tries / 2];
            output.WriteLine(
                $"{name,-10} n={sorted.Count} min={sorted[0]:F0} p5={sorted[5]:F0} median={sorted[Tries / 2]:F0} p95={sorted[94]:F0} max={sorted[^1]:F0} ms");

            // None answers before the floor, whatever it found.
            Assert.All(sorted, time => Assert.True(time >= 290, $"{name} answered in {time:F0} ms"));
        }

        // The medians lie together, and each lies inside the middle nine tenths of every other.
        Assert.InRange(medians.Values.Max() - medians.Values.Min(), 0, 60);
        foreach (var (name, times) in measured)
        {
            var sorted = times.Order().ToList();
            Assert.All(medians, median => Assert.InRange(median.Value, sorted[5] - 5, sorted[94] + 5));
            _ = name;
        }
    }

    private async Task<(string Name, List<double> Times)> MeasureAsync(string name)
    {
        using var world = await OpsWorld.CreateAsync(
            sql, name switch { "suspended" => OperatorStates.Suspended, "invited" => OperatorStates.Invited, _ => OperatorStates.Active });
        using var browser = world.Factory.Browser();
        var email = name == "unknown" ? $"nobody-{Guid.NewGuid():N}@heta.example" : world.Email;

        if (name == "locked")
        {
            await world.Factory.OpsSqlAsync(
                $"UPDATE [ops].[Operator] SET [LockedUntilUtc] = DATEADD(day, 1, SYSUTCDATETIME()) WHERE [Id] = '{world.OperatorId}'");
        }

        var times = new List<double>(Tries);
        for (var i = 0; i < Tries; i++)
        {
            // The "wrong" account is kept unlocked — its fifth wrong try would lock it. The same
            // statement is run before every try of every case, so that the measuring itself
            // treats the five alike.
            await world.Factory.OpsSqlAsync(
                name == "wrong"
                    ? $"UPDATE [ops].[Operator] SET [FailedAttempts] = 0, [LockedUntilUtc] = NULL WHERE [Id] = '{world.OperatorId}'"
                    : $"UPDATE [ops].[Operator] SET [FailedAttempts] = [FailedAttempts] WHERE [Id] = '{world.OperatorId}'");

            var started = Stopwatch.GetTimestamp();
            using var response = await browser.OpsStepOneAsync(email, "Wrong-Horse-9!");
            times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // Under two a second for one address.
            var rest = 520 - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (rest > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(rest));
            }
        }

        return (name, times);
    }
}
