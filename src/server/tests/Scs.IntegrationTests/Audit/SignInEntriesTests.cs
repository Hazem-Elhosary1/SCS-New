using Microsoft.Extensions.DependencyInjection;
using Scs.Audit;
using Scs.Identity;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.Tenancy;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 3 (FR-014a, FR-028, FR-029; research R2): what happens at sign-in and to
/// an account's password is part of the same trail, written with its sign-in event.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SignInEntriesTests(SqlServerFixture sql)
{
    private const string Wrong = "Wrong-Password-1!";
    private const string Agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private Task<RefusalWorld> WorldAsync() =>
        RefusalWorld.CreateAsync(sql, factory => factory.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));

    private static HttpClient BrowserOf(RefusalWorld world)
    {
        var browser = world.Factory.Browser();
        browser.DefaultRequestHeaders.UserAgent.ParseAdd(Agent);
        return browser;
    }

    private static async Task WrongPasswordsAsync(HttpClient browser, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            using var refused = await browser.StepOneAsync(email, Wrong);
            Assert.Equal(401, (int)refused.StatusCode);
        }
    }

    private static async Task SignInAsync(RefusalWorld world, HttpClient browser, string email, string password = SignIn.Password)
    {
        using var first = await browser.StepOneAsync(email, password);
        Assert.True(first.IsSuccessStatusCode, $"Step one answered {(int)first.StatusCode}.");
        using var second = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email));
        Assert.True(second.IsSuccessStatusCode, $"Step two answered {(int)second.StatusCode}.");
    }

    [Fact]
    public async Task A_sign_in_is_recorded_as_the_accounts_own_doing_though_no_one_was_signed_in()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        using (var first = await browser.StepOneAsync(email, SignIn.Password))
        {
            // A correct password alone is not a sign-in.
            Assert.True(first.IsSuccessStatusCode);
            Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
        }

        using (var second = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email)))
        {
            Assert.True(second.IsSuccessStatusCode);
        }

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((AuditActions.SignInSucceeded, "security"), (entry.ActionCode, entry.Category));
        Assert.Equal(("staff", account, email), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(("account", account, (string?)null), (entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Equal(Agent, entry.UserAgent);
        Assert.Matches("^[0-9a-f]{32}$", entry.TraceId);

        // The entry and its sign-in event are of one request.
        Assert.Equal(
            1,
            await world.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'signed-in' AND TraceId = '{entry.TraceId}'"));

        var read = Assert.Single((await world.Factory.ReadTrailAsync(world.Clinic)).Entries);
        Assert.Equal($"سُجِّل الدخول إلى حساب {email}", read.Sentence);
        Assert.Equal("متصفح Chrome على Windows", read.Device);
        Assert.Null(read.Attempts);
    }

    [Fact]
    public async Task A_run_of_failed_attempts_is_one_entry_written_by_the_first()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        await WrongPasswordsAsync(browser, email, 1);

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((AuditActions.SignInFailedRun, "security"), (entry.ActionCode, entry.Category));

        // About a clinic's account, by nobody anyone knows.
        Assert.Equal(("staff", (Guid?)null, (string?)null), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(("account", account, (string?)null), (entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Equal(Agent, entry.UserAgent);

        await WrongPasswordsAsync(browser, email, 4);

        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic)));
        Assert.Equal(5, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'wrong-password'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'account-locked'"));
    }

    [Theory]
    [InlineData(1, false, "محاولة واحدة على حساب {0}", "1 attempt on {0}’s account")]
    [InlineData(2, false, "محاولتان على حساب {0}", "2 attempts on {0}’s account")]
    [InlineData(4, false, "٤ محاولات متتالية على حساب {0}", "4 attempts in a row on {0}’s account")]
    [InlineData(5, true, "٥ محاولات متتالية على حساب {0}، فقُفل ١٥ دقيقة", "5 attempts in a row on {0}’s account; locked for 15 minutes")]
    public async Task A_run_is_read_with_its_attempts_and_its_lock(int attempts, bool locked, string arabic, string english)
    {
        using var world = await WorldAsync();
        var (_, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        await WrongPasswordsAsync(browser, email, attempts);

        var ar = Assert.Single((await world.Factory.ReadTrailAsync(world.Clinic)).Entries);
        var en = Assert.Single((await world.Factory.ReadTrailAsync(world.Clinic, Trail.Everything("en"))).Entries);

        Assert.Equal((attempts, locked), (ar.Attempts, ar.Locked));
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, arabic, email), ar.Sentence);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, english, email), en.Sentence);
        Assert.Equal(("محاولة دخول فاشلة", "Failed sign-in"), (ar.Action.Name, en.Action.Name));
        Assert.Equal((false, (Guid?)null, "غير معروف"), (ar.Actor.Known, ar.Actor.AccountId, ar.Actor.Name));
        Assert.Equal("Unknown", en.Actor.Name);
    }

    [Fact]
    public async Task Attempts_made_while_the_account_is_locked_are_not_of_the_run()
    {
        using var world = await WorldAsync();
        var (_, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        await WrongPasswordsAsync(browser, email, 5);
        await WrongPasswordsAsync(browser, email, 3);

        var read = Assert.Single((await world.Factory.ReadTrailAsync(world.Clinic)).Entries);
        Assert.Equal((5, true), (read.Attempts, read.Locked));
    }

    [Fact]
    public async Task A_wrong_password_after_a_sign_in_starts_a_new_run()
    {
        using var world = await WorldAsync();
        var (_, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        await WrongPasswordsAsync(browser, email, 2);
        await SignInAsync(world, browser, email);
        await WrongPasswordsAsync(browser, email, 1);

        var runs = (await world.Factory.ReadTrailAsync(world.Clinic)).Entries
            .Where(e => e.Action.Code == AuditActions.SignInFailedRun)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        Assert.Equal([2, 1], runs.Select(r => r.Attempts!.Value));
        Assert.All(runs, run => Assert.False(run.Locked));
    }

    [Fact]
    public async Task A_run_that_is_left_alone_long_enough_is_over()
    {
        using var world = await WorldAsync();
        var (_, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        await WrongPasswordsAsync(browser, email, 2);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(16));
        await WrongPasswordsAsync(browser, email, 3);

        var runs = (await world.Factory.ReadTrailAsync(world.Clinic)).Entries.OrderBy(e => e.OccurredAt).ToList();
        Assert.Equal([2, 3], runs.Select(r => r.Attempts!.Value));
    }

    [Fact]
    public async Task Signing_out_is_recorded_as_the_accounts_own_doing()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);
        await SignInAsync(world, browser, email);

        using (var out_ = await browser.SignOutAsync())
        {
            Assert.True(out_.IsSuccessStatusCode);
        }

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionSignedOut));
        Assert.Equal(("staff", account, email, account), (entry.ActorKind, entry.ActorAccountId, entry.ActorName, entry.TargetId));
        Assert.Null(entry.Facts);
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));

        // Signing out again ends nothing and records nothing.
        using (await browser.SignOutAsync()) { }
        Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionSignedOut));
    }

    [Fact]
    public async Task Sessions_ended_by_someone_else_say_who_why_and_how_many()
    {
        using var world = await WorldAsync();
        var (manager, managerEmail) = await world.AccountAsync();
        var (person, email) = await world.AccountAsync();
        using var one = BrowserOf(world);
        using var two = BrowserOf(world);
        await SignInAsync(world, one, email);
        await SignInAsync(world, two, email);

        var ended = await world.Factory.AsStaffAsync(world.Clinic, manager, services =>
            services.GetRequiredService<IStaffSessions>().EndForAccountAsync(person, SessionEndings.EndedForAccount, default));

        Assert.Equal(2, ended);

        // Two sessions, one entry: one thing was done to one account (FR-013).
        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));
        Assert.Equal(("staff", manager, managerEmail), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(("account", person), (entry.TargetKind, entry.TargetId));
        Assert.Equal(("ended-for-account", "all"), (entry.FactsJson.GetProperty("reason").GetString(), entry.FactsJson.GetProperty("scope").GetString()));
        Assert.Equal(2, entry.FactsJson.EnumerateObject().Count());
    }

    [Fact]
    public async Task Ending_sessions_that_do_not_exist_records_nothing()
    {
        using var world = await WorldAsync();
        var (manager, _) = await world.AccountAsync();
        var (person, _) = await world.AccountAsync();

        await world.Factory.AsStaffAsync(world.Clinic, manager, services =>
            services.GetRequiredService<IStaffSessions>().EndForAccountAsync(person, SessionEndings.EndedForAccount, default));

        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task A_lock_that_ends_sessions_is_the_platforms_doing()
    {
        using var world = await WorldAsync();
        var (person, email) = await world.AccountAsync();
        using var signedIn = BrowserOf(world);
        await SignInAsync(world, signedIn, email);

        using var stranger = BrowserOf(world);
        await WrongPasswordsAsync(stranger, email, 5);

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));
        Assert.Equal(("system", (Guid?)null, person), (entry.ActorKind, entry.ActorAccountId, entry.TargetId));
        Assert.Equal("locked", entry.FactsJson.GetProperty("reason").GetString());

        // The clinic reads the sign-in and the run; what the platform did is not its to read.
        var read = await world.Factory.ReadTrailAsync(world.Clinic);
        Assert.Equal(
            [AuditActions.SignInFailedRun, AuditActions.SignInSucceeded],
            read.Entries.Select(e => e.Action.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_completed_reset_is_recorded_with_the_sessions_it_ended()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var signedIn = BrowserOf(world);
        await SignInAsync(world, signedIn, email);

        var token = await world.Factory.ResetTokenForAsync(world.Clinic, email);

        // Asking for the link changed nothing yet, and recorded nothing.
        Assert.Single(await sql.Main.EntriesAsync(world.Clinic));

        using (var elsewhere = BrowserOf(world))
        using (var set = await elsewhere.SetWithLinkAsync(token, Passwords.New))
        {
            Assert.True(set.IsSuccessStatusCode, $"Answered {(int)set.StatusCode}.");
        }

        var reset = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.PasswordResetCompleted));
        Assert.Equal(("staff", account, email, account), (reset.ActorKind, reset.ActorAccountId, reset.ActorName, reset.TargetId));
        Assert.Null(reset.Facts);

        var ended = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));
        Assert.Equal((account, account, "password-reset"), (ended.ActorAccountId, ended.TargetId, ended.FactsJson.GetProperty("reason").GetString()));
        Assert.Equal(reset.TraceId, ended.TraceId);
        Assert.Equal(reset.OccurredAtUtc, ended.OccurredAtUtc);
    }

    [Fact]
    public async Task Replacing_a_temporary_password_is_recorded()
    {
        using var world = await WorldAsync();
        var email = $"temp-{Guid.NewGuid():N}@clinic.example";
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithTypedPasswordAsync(email, SignIn.Password, default));
        using var browser = BrowserOf(world);
        await SignInAsync(world, browser, email);

        using (var changed = await browser.ChangePasswordAsync(Passwords.New))
        {
            Assert.True(changed.IsSuccessStatusCode, $"Answered {(int)changed.StatusCode}.");
        }

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.PasswordTemporaryReplaced));
        Assert.Equal(("staff", created.AccountId, email, created.AccountId), (entry.ActorKind, entry.ActorAccountId, entry.ActorName, entry.TargetId));

        // Their own session goes on: nothing ended, so nothing says it did.
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));
    }

    [Fact]
    public async Task An_accepted_invitation_is_recorded_under_users()
    {
        using var world = await WorldAsync();
        var email = $"invited-{Guid.NewGuid():N}@clinic.example";
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithLinkAsync(email, default));

        using (var browser = BrowserOf(world))
        using (var set = await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), Passwords.New))
        {
            Assert.True(set.IsSuccessStatusCode, $"Answered {(int)set.StatusCode}.");
        }

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((AuditActions.InvitationAccepted, "users"), (entry.ActionCode, entry.Category));
        Assert.Equal(("staff", created.AccountId, email, created.AccountId), (entry.ActorKind, entry.ActorAccountId, entry.ActorName, entry.TargetId));
    }

    [Theory]
    [InlineData(HandoverMethods.Link, "link")]
    [InlineData(HandoverMethods.Generated, "temporary-password")]
    [InlineData(HandoverMethods.Typed, "temporary-password")]
    public async Task Access_handed_over_again_says_which_way_and_by_whom(string method, string way)
    {
        using var world = await WorldAsync();
        var (manager, managerEmail) = await world.AccountAsync();
        var (person, _) = await world.AccountAsync();

        await world.Factory.AsStaffAsync(world.Clinic, manager, services => services.GetRequiredService<IAccountHandover>()
            .ReissueAsync(person, method, method == HandoverMethods.Typed ? Passwords.New : null, default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((AuditActions.AccountAccessReissued, "users"), (entry.ActionCode, entry.Category));
        Assert.Equal(("staff", manager, managerEmail), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(("account", person), (entry.TargetKind, entry.TargetId));

        // The way, and nothing else: never the password, never the link.
        Assert.Equal(way, Assert.Single(entry.FactsJson.EnumerateObject()).Value.GetString());
    }

    [Fact]
    public async Task What_did_not_take_effect_leaves_no_entry()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        // A link asked for, a code typed wrong until it is spent.
        await world.Factory.ResetTokenForAsync(world.Clinic, email);
        using (var first = await browser.StepOneAsync(email, SignIn.Password))
        {
            Assert.True(first.IsSuccessStatusCode);
        }

        var code = world.Factory.Mail.LatestCodeFor(email);
        var wrongCode = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
        {
            using var refused = await browser.StepTwoAsync(wrongCode);
            Assert.Equal(401, (int)refused.StatusCode);
        }

        Assert.Equal(5, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'wrong-code'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'code-spent'"));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'reset-requested'"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));

        // The same, said of the mapping itself.
        foreach (var kind in new[] { SignInEventKinds.ResetRequested, SignInEventKinds.WrongCode, SignInEventKinds.CodeSpent, SignInEventKinds.RenewalReused })
        {
            Assert.Null(TrailEntries.PasswordSet(kind, account, null, RequestOrigin.None));
        }
    }

    [Fact]
    public async Task An_attempt_on_an_address_that_matches_no_account_writes_no_entry_in_any_clinic()
    {
        using var world = await WorldAsync();
        await world.AccountAsync();
        var before = await sql.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM [audit].Entry");
        using var browser = BrowserOf(world);

        using (var refused = await browser.StepOneAsync($"nobody-{Guid.NewGuid():N}@clinic.example", Wrong))
        {
            Assert.Equal(401, (int)refused.StatusCode);
        }

        Assert.Equal(before, await sql.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM [audit].Entry"));
    }

    [Fact]
    public async Task When_the_entry_cannot_be_written_the_sign_in_does_not_happen()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory
            .With("Scs:AUTH:REQUESTS_PER_MINUTE", "120")
            .WithServices(services => services.AddSingleton<IAuditCatalogue, WithoutSignIn>()));
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world);

        using (var first = await browser.StepOneAsync(email, SignIn.Password))
        {
            Assert.True(first.IsSuccessStatusCode);
        }

        using (var second = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email)))
        {
            Assert.Equal(500, (int)second.StatusCode);
        }

        // Neither the session, nor its event, nor an entry: one unit of work.
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{account}'"));
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].SignInEvent WHERE AccountId = '{account}' AND Kind = 'signed-in'"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    /// <summary>A catalogue that has lost one action: the writer then refuses that entry.</summary>
    private sealed class WithoutSignIn : IAuditCatalogue
    {
        public AuditAction? Find(string code) => code == AuditActions.SignInSucceeded ? null : AuditActions.Find(code);
    }

    // ------------------------------------------------------------------ what was recorded before this spec

    private const string BeforeThisSpec = "StaffPreferences";

    private static Task<int> EventAsync(
        TestDatabase db, Guid clinic, Guid account, string kind, int minutesAgo, string? detail = null, Guid? actor = null, string? trace = null) =>
        TestDatabase.ExecuteAsync(
            db.Crossing,
            $"""
             INSERT INTO [identity].SignInEvent (Id, ClinicId, AccountId, Kind, Category, Detail, ActorAccountId, IpAddress, UserAgent, TraceId, OccurredAtUtc)
             VALUES (NEWID(), '{clinic}', '{account}', '{kind}', '{SignInEventKinds.CategoryOf(kind)}',
                     {(detail is null ? "NULL" : $"'{detail}'")}, {(actor is null ? "NULL" : $"'{actor}'")},
                     '203.0.113.7', N'{Agent}', '{trace ?? Guid.NewGuid().ToString("N")}',
                     DATEADD(MINUTE, -{minutesAgo}, CAST('2026-10-01T12:00:00+00:00' AS datetimeoffset)))
             """);

    private static async Task<Guid> AccountRowAsync(TestDatabase db, Guid clinic)
    {
        var id = Guid.CreateVersion7();
        await TestDatabase.ExecuteAsync(
            db.Crossing,
            $"""
             INSERT INTO [identity].StaffAccount (Id, ClinicId, Email, PasswordHash, State, CreatedAtUtc)
             VALUES ('{id}', '{clinic}', '{id:N}@before.example', 'v1.1.c2FsdA==.aGFzaA==', 'active', SYSDATETIMEOFFSET())
             """);
        return id;
    }

    [Fact]
    public async Task What_was_recorded_before_the_trail_existed_is_carried_into_it_once()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        var identity = new IdentityModule();
        await new TenancyModule().MigrateAsync(db.Migration, null, default);
        await new AuditModule().MigrateAsync(db.Migration, null, default);
        await identity.MigrateAsync(db.Migration, BeforeThisSpec, default);

        // A clinic as spec 006 left it: accounts, their sign-in events, and its roles.
        var clinic = await db.SeedClinicAsync();
        var person = await AccountRowAsync(db, clinic);
        var manager = await AccountRowAsync(db, clinic);
        var invited = await AccountRowAsync(db, clinic);

        await EventAsync(db, clinic, person, "wrong-password", 100);
        await EventAsync(db, clinic, person, "wrong-password", 99);
        await EventAsync(db, clinic, person, "signed-in", 98);
        for (var i = 0; i < 4; i++)
        {
            await EventAsync(db, clinic, person, "wrong-password", 90 - i);
        }

        await EventAsync(db, clinic, person, "wrong-password", 80, trace: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        await EventAsync(db, clinic, person, "account-locked", 80, trace: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        await EventAsync(db, clinic, person, "session-ended", 80, "locked", trace: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        await EventAsync(db, clinic, person, "signed-in", 70);
        await EventAsync(db, clinic, person, "session-ended", 60, "signed-out");
        await EventAsync(db, clinic, person, "reset-requested", 55);
        await EventAsync(db, clinic, person, "reset-completed", 50, trace: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        await EventAsync(db, clinic, person, "session-ended", 50, "password-reset", trace: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        await EventAsync(db, clinic, person, "session-ended", 50, "password-reset", trace: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        await EventAsync(db, clinic, person, "access-reissued", 40, "link", actor: manager);
        await EventAsync(db, clinic, person, "access-reissued", 30, "generated");
        await EventAsync(db, clinic, person, "temp-password-changed", 20);
        await EventAsync(db, clinic, person, "wrong-code", 15);
        await EventAsync(db, clinic, person, "code-spent", 15);
        await EventAsync(db, clinic, person, "renewal-reused", 14);
        await EventAsync(db, clinic, invited, "invitation-accepted", 10);

        var custom = Guid.CreateVersion7();
        var owner = Guid.CreateVersion7();
        await TestDatabase.ExecuteAsync(
            db.Crossing,
            $"""
             INSERT INTO [identity].Role (Id, ClinicId, Name, Description, Category, ReadyKey, IsRenamed, HoldsEverything, CreatedAtUtc)
             VALUES ('{owner}', '{clinic}', N'مالك العيادة', N'كل صلاحيات العيادة', 'ready', 'owner', 0, 1, '2026-09-01T08:00:00+00:00'),
                    ('{custom}', '{clinic}', N'فريق "العيادة"', NULL, 'custom', NULL, 1, 0, '2026-09-02T08:00:00+00:00');
             INSERT INTO [identity].RolePermission (RoleId, Code, ClinicId)
             VALUES ('{custom}', 'bookings.view', '{clinic}'), ('{custom}', 'patients.view', '{clinic}');
             """);

        await identity.MigrateAsync(db.Migration, null, default);
        var carried = await db.EntriesAsync(clinic);

        // Taken back and applied again: nothing is removed, and nothing is added twice.
        await identity.MigrateAsync(db.Migration, BeforeThisSpec, default);
        await identity.MigrateAsync(db.Migration, null, default);
        Assert.Equal(carried, await db.EntriesAsync(clinic));

        Assert.Equal(
            new Dictionary<string, int>
            {
                [AuditActions.SignInFailedRun] = 2,
                [AuditActions.SignInSucceeded] = 2,
                [AuditActions.SessionSignedOut] = 1,
                [AuditActions.SessionsEnded] = 2,
                [AuditActions.PasswordResetCompleted] = 1,
                [AuditActions.AccountAccessReissued] = 2,
                [AuditActions.PasswordTemporaryReplaced] = 1,
                [AuditActions.InvitationAccepted] = 1,
                [AuditActions.RoleCreated] = 2,
            },
            carried.GroupBy(e => e.ActionCode).ToDictionary(g => g.Key, g => g.Count()));

        // Each entry is what the writer would have written at the time.
        Assert.All(carried, entry =>
        {
            Assert.Equal(AuditActions.Find(entry.ActionCode)!.Category, entry.Category);
            Assert.Equal(clinic, entry.ClinicId);
        });

        var runs = carried.Where(e => e.ActionCode == AuditActions.SignInFailedRun).ToList();
        Assert.All(runs, run => Assert.Equal(("staff", (Guid?)null, person, Agent), (run.ActorKind, run.ActorAccountId, run.TargetId, run.UserAgent)));
        Assert.Equal(
            [new DateTimeOffset(2026, 10, 1, 10, 20, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 10, 30, 0, TimeSpan.Zero)],
            runs.Select(r => r.OccurredAtUtc).Order());

        var signedIn = carried.First(e => e.ActionCode == AuditActions.SignInSucceeded);
        Assert.Equal(("staff", person, $"{person:N}@before.example", "203.0.113.7"), (signedIn.ActorKind, signedIn.ActorAccountId, signedIn.ActorName, signedIn.IpAddress));

        var ended = carried.Where(e => e.ActionCode == AuditActions.SessionsEnded).ToList();
        var byLock = ended.Single(e => e.FactsJson.GetProperty("reason").GetString() == "locked");
        var byReset = ended.Single(e => e.FactsJson.GetProperty("reason").GetString() == "password-reset");
        Assert.Equal(("system", (Guid?)null), (byLock.ActorKind, byLock.ActorAccountId));
        Assert.Equal(("staff", person), (byReset.ActorKind, byReset.ActorAccountId));

        var reissued = carried.Where(e => e.ActionCode == AuditActions.AccountAccessReissued).ToList();
        Assert.Contains(reissued, e => e.ActorAccountId == manager && e.FactsJson.GetProperty("way").GetString() == "link");
        Assert.Contains(reissued, e => e.ActorKind == "system" && e.FactsJson.GetProperty("way").GetString() == "temporary-password");

        Assert.Equal(invited, carried.Single(e => e.ActionCode == AuditActions.InvitationAccepted).ActorAccountId);

        var roles = carried.Where(e => e.ActionCode == AuditActions.RoleCreated).ToList();
        Assert.All(roles, role => Assert.Equal(("system", "role"), (role.ActorKind, role.TargetKind)));
        var ownerEntry = roles.Single(r => r.TargetId == owner);
        Assert.Equal(("مالك العيادة", "owner"), (ownerEntry.TargetName, ownerEntry.TargetReadyKey));
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), ownerEntry.OccurredAtUtc);
        Assert.Equal(PermissionCatalogue.Count, ownerEntry.Change("permissions").GetProperty("added").GetArrayLength());
        Assert.Equal("owner", ownerEntry.Change("name").GetProperty("afterKey").GetString());

        var customEntry = roles.Single(r => r.TargetId == custom);
        Assert.Equal(("فريق \"العيادة\"", (string?)null), (customEntry.TargetName, customEntry.TargetReadyKey));
        Assert.Equal("فريق \"العيادة\"", customEntry.Change("name").GetProperty("after").GetString());
        Assert.False(customEntry.HasChange("description"));
        Assert.Equal(
            ["bookings.view", "patients.view"],
            customEntry.Change("permissions").GetProperty("added").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task What_is_carried_reads_like_what_is_written_today()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        var identity = new IdentityModule();
        await new TenancyModule().MigrateAsync(db.Migration, null, default);
        await new AuditModule().MigrateAsync(db.Migration, null, default);
        await identity.MigrateAsync(db.Migration, BeforeThisSpec, default);

        var clinic = await db.SeedClinicAsync();
        var person = await AccountRowAsync(db, clinic);
        for (var i = 0; i < 5; i++)
        {
            await EventAsync(db, clinic, person, "wrong-password", 50 - i, trace: $"{i:D32}");
        }

        await EventAsync(db, clinic, person, "account-locked", 46, trace: $"{4:D32}");
        await identity.MigrateAsync(db.Migration, null, default);

        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", db.App)
            .With("Scs:DB:REPORT_CONNECTION", db.Report)
            .With("Scs:DB:CROSSING_CONNECTION", db.Crossing);

        var page = await factory.ReadTrailAsync(
            clinic, Trail.Everything() with { From = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), To = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero) });

        var run = Assert.Single(page.Entries);
        Assert.Equal((5, true), (run.Attempts, run.Locked));
        Assert.Equal($"٥ محاولات متتالية على حساب {person:N}@before.example، فقُفل ١٥ دقيقة", run.Sentence);
    }
}
