using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Audit;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.Notifications.Contracts;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 4 (FR-008, FR-010 to FR-012, FR-016 to FR-019; SC-009): an entry holds
/// only what its action allows, and never a secret.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditFactsTests(SqlServerFixture sql)
{
    /// <summary>An action only this test knows: what a later spec adds to the catalogue.</summary>
    private sealed class WithOneMoreAction : IAuditCatalogue
    {
        public const string Code = "test.refund-issued";

        private static readonly AuditAction Refund = new(
            Code, AuditCategories.Records, "ردّ مبلغ", "Refund issued", "رُدّ مبلغ", "An amount was refunded", ["amount", "method"], false);

        public AuditAction? Find(string code) => code == Code ? Refund : AuditActions.Find(code);
    }

    private static Dictionary<string, object?> Facts(string key, object? value) => new(StringComparer.Ordinal) { [key] = value };

    /// <summary>Adds a role and records what is given beside it; returns what saving threw, if anything.</summary>
    private static Task<Exception?> SaveRoleWithAsync(RefusalWorld world, string roleName, AuditRecord record) =>
        world.Factory.ForClinicAsync<Exception?>(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            var role = Role.Custom(roleName, null, null, DateTimeOffset.UtcNow);
            role.Hold([Permissions.BookingsView]);
            db.Roles.Add(role);
            db.Record(record);
            return await Record.ExceptionAsync(() => db.SaveChangesAsync());
        });

    private static Task<int> RolesNamedAsync(RefusalWorld world, string name) =>
        world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{world.Clinic}' AND Name = N'{name}'");

    [Fact]
    public async Task A_fact_the_action_does_not_allow_is_refused_and_the_change_with_it()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var account = AuditTarget.ForAccount(Guid.NewGuid());

        var refusal = await SaveRoleWithAsync(
            world, "حقيقة زائدة", new AuditRecord(AuditActions.SessionsEnded, account, Facts("device", "a phone")));

        Assert.IsType<AuditRefusedException>(refusal);
        Assert.Contains("device", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, await RolesNamedAsync(world, "حقيقة زائدة"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("code")]
    [InlineData("token")]
    [InlineData("email")]
    public async Task A_fact_that_could_hold_a_secret_is_allowed_by_no_action(string key)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var account = AuditTarget.ForAccount(Guid.NewGuid());

        foreach (var action in AuditActions.All.Where(a => !a.NeedsBeforeAfter))
        {
            var refusal = await SaveRoleWithAsync(
                world, $"r-{Guid.NewGuid():N}", new AuditRecord(action.Code, account, Facts(key, "x")));
            Assert.IsType<AuditRefusedException>(refusal);
        }

        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task A_code_that_is_not_in_the_catalogue_is_refused()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        var refusal = await SaveRoleWithAsync(world, "رمز غريب", new AuditRecord("refund.issued", null));

        Assert.IsType<AuditRefusedException>(refusal);
        Assert.Equal(0, await RolesNamedAsync(world, "رمز غريب"));
    }

    [Fact]
    public async Task An_action_that_needs_before_and_after_is_refused_without_them()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var role = new AuditTarget(AuditTarget.Role, Guid.NewGuid(), "دور");

        Assert.IsType<AuditRefusedException>(await SaveRoleWithAsync(world, "بلا قبل وبعد", new AuditRecord(AuditActions.RoleUpdated, role)));
        Assert.IsType<AuditRefusedException>(
            await SaveRoleWithAsync(world, "بلا قبل وبعد ٢", new AuditRecord(AuditActions.RoleUpdated, role, Changes: [])));

        Assert.Equal(0, await RolesNamedAsync(world, "بلا قبل وبعد"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task A_change_that_changed_nothing_writes_no_entry_and_still_saves()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var role = new AuditTarget(AuditTarget.Role, Guid.NewGuid(), "دور");

        var failure = await SaveRoleWithAsync(
            world,
            "لم يتغير شيء",
            new AuditRecord(
                AuditActions.RoleUpdated,
                role,
                Changes: [AuditChange.Of("name", "دور", "دور"), AuditChange.OfSet("permissions", [], [])]));

        Assert.Null(failure);
        Assert.Equal(1, await RolesNamedAsync(world, "لم يتغير شيء"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task Only_what_changed_is_kept_on_the_entry()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var role = new AuditTarget(AuditTarget.Role, Guid.NewGuid(), "دور");

        await SaveRoleWithAsync(
            world,
            "تغير الاسم فقط",
            new AuditRecord(
                AuditActions.RoleUpdated,
                role,
                Changes:
                [
                    AuditChange.Of("name", "قديم", "دور"),
                    AuditChange.Of("description", "هو هو", "هو هو"),
                    AuditChange.OfSet("permissions", [], []),
                ]));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal("name", Assert.Single(entry.ChangesJson.EnumerateArray()).GetProperty("field").GetString());
        Assert.Equal("قديم", entry.Change("name").GetProperty("before").GetString());
        Assert.Equal("دور", entry.Change("name").GetProperty("after").GetString());
    }

    [Fact]
    public async Task A_fact_is_a_short_text_a_number_a_yes_or_no_or_a_list_of_codes()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var account = AuditTarget.ForAccount(Guid.NewGuid());

        var tooLong = await SaveRoleWithAsync(
            world, "طويل", new AuditRecord(AuditActions.SessionsEnded, account, Facts("reason", new string('x', AuditSql.FactTextMaxLength + 1))));
        var anObject = await SaveRoleWithAsync(
            world, "كائن", new AuditRecord(AuditActions.SessionsEnded, account, Facts("reason", new { nested = "x" })));
        var fits = await SaveRoleWithAsync(
            world, "يصلح", new AuditRecord(AuditActions.SessionsEnded, account, Facts("reason", new string('x', AuditSql.FactTextMaxLength))));

        Assert.IsType<AuditRefusedException>(tooLong);
        Assert.IsType<AuditRefusedException>(anObject);
        Assert.Null(fits);
        // The limit was 200 until spec 011, where a reason or a note on an operator or a role is "up to 500".
        Assert.Equal(500, AuditSql.FactTextMaxLength);
        Assert.Equal(
            AuditSql.FactTextMaxLength,
            Assert.Single(await sql.Main.EntriesAsync(world.Clinic)).FactsJson.GetProperty("reason").GetString()!.Length);
    }

    [Fact]
    public async Task An_action_a_later_spec_adds_records_with_no_change_to_the_trail()
    {
        using var world = await RefusalWorld.CreateAsync(
            sql, factory => factory.WithServices(services => services.AddSingleton<IAuditCatalogue, WithOneMoreAction>()));
        var (actor, _) = await world.AccountAsync();
        var invoice = Guid.NewGuid();

        await world.Factory.AsStaffAsync(world.Clinic, actor, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            db.Record(new AuditRecord(
                WithOneMoreAction.Code,
                new AuditTarget("invoice", invoice, "INV-0007"),
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["amount"] = 150.50m, ["method"] = "cash" }));
            await db.SaveChangesAsync();
        });

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((WithOneMoreAction.Code, "records", "invoice", invoice, "INV-0007"),
            (entry.ActionCode, entry.Category, entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Equal(150.50m, entry.FactsJson.GetProperty("amount").GetDecimal());
        Assert.Equal("cash", entry.FactsJson.GetProperty("method").GetString());
        Assert.Equal(actor, entry.ActorAccountId);

        // And it is read with the others, under its own category.
        var read = await world.Factory.ReadTrailAsync(world.Clinic, Trail.Everything() with { Category = "records" });
        Assert.Equal((1, 1), (read.Total, read.Counts.Records));
    }

    [Fact]
    public async Task Saving_display_preferences_writes_no_entry()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var before = (await sql.Main.EntriesAsync(world.Clinic)).Count;

        using var saved = await owner.Browser.PutAsJsonAsync("/v1/account/preferences", new { language = "en", sidebarCollapsed = true });

        Assert.True(saved.IsSuccessStatusCode, $"Answered {(int)saved.StatusCode}.");
        Assert.Equal(before, (await sql.Main.EntriesAsync(world.Clinic)).Count);
    }

    [Fact]
    public async Task Nothing_secret_that_passed_through_sign_in_is_anywhere_in_the_trail()
    {
        const string password = "Zzq-Secret-Pass-9!";
        const string wrong = "Zzq-Wrong-Pass-7!";

        using var world = await RefusalWorld.CreateAsync(sql, f => f.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));
        var localPart = $"zzq.mariam.{Guid.NewGuid():N}";
        var email = $"{localPart}@zzqclinic.example";
        var (account, _) = await world.Factory.CreateAccountAsync(world.Clinic, email: email, password: password);
        var secrets = new List<string> { password, wrong, Passwords.New };

        using var browser = world.Factory.Browser();

        // A failed attempt, a sign-in, a sign-out.
        using (await browser.StepOneAsync(email, wrong)) { }
        using (var first = await browser.StepOneAsync(email, password))
        {
            secrets.AddRange(CookieValues(first));
        }

        var code = world.Factory.Mail.LatestCodeFor(email);
        secrets.Add(code);
        using (var second = await browser.StepTwoAsync(code))
        {
            Assert.True(second.IsSuccessStatusCode);
            secrets.AddRange(CookieValues(second));
        }

        using (await browser.SignOutAsync()) { }

        // A reset by link, and access handed over again by link and by a generated password.
        var token = await world.Factory.ResetTokenForAsync(world.Clinic, email);
        secrets.Add(token);
        using (var elsewhere = world.Factory.Browser())
        using (var set = await elsewhere.SetWithLinkAsync(token, Passwords.New))
        {
            Assert.True(set.IsSuccessStatusCode, $"Answered {(int)set.StatusCode}.");
        }

        await world.Factory.HandOverAsync(world.Clinic, handover => handover.ReissueAsync(account, HandoverMethods.Link, null, default));
        secrets.Add(world.Factory.Mail.LatestLinkTokenFor(email));
        var generated = await world.Factory.HandOverAsync(
            world.Clinic, handover => handover.ReissueAsync(account, HandoverMethods.Generated, null, default));
        secrets.Add(generated.GeneratedPassword!);

        // An invitation accepted by its link.
        var invitedEmail = $"zzq.invited.{Guid.NewGuid():N}@zzqclinic.example";
        await world.Factory.HandOverAsync(world.Clinic, handover => handover.CreateWithLinkAsync(invitedEmail, default));
        var invitation = world.Factory.Mail.LatestLinkTokenFor(invitedEmail);
        secrets.Add(invitation);
        using (var elsewhere = world.Factory.Browser())
        using (var accepted = await elsewhere.SetWithLinkAsync(invitation, Passwords.New))
        {
            Assert.True(accepted.IsSuccessStatusCode, $"Answered {(int)accepted.StatusCode}.");
        }

        // A temporary password typed by someone else, replaced by its owner after signing in.
        const string temporary = "Zzq-Temp-Pass-5!";
        var tempEmail = $"zzq.temp.{Guid.NewGuid():N}@zzqclinic.example";
        var temp = await world.Factory.HandOverAsync(
            world.Clinic, handover => handover.CreateWithTypedPasswordAsync(tempEmail, temporary, default));
        secrets.Add(temporary);
        using var tempBrowser = world.Factory.Browser();
        using (var first = await tempBrowser.StepOneAsync(tempEmail, temporary))
        {
            secrets.AddRange(CookieValues(first));
        }

        var tempCode = world.Factory.Mail.LatestCodeFor(tempEmail);
        secrets.Add(tempCode);
        using (var second = await tempBrowser.StepTwoAsync(tempCode))
        {
            Assert.True(second.IsSuccessStatusCode);
            secrets.AddRange(CookieValues(second));
        }

        using (var replaced = await tempBrowser.ChangePasswordAsync(Passwords.New))
        {
            Assert.True(replaced.IsSuccessStatusCode, $"Answered {(int)replaced.StatusCode}.");
        }

        // A colleague ends that person's sessions, and changes roles and who holds them.
        var (manager, _) = await world.Factory.CreateAccountAsync(world.Clinic, email: $"zzq.manager.{Guid.NewGuid():N}@zzqclinic.example");
        await world.Factory.AsStaffAsync(world.Clinic, manager, services =>
            services.GetRequiredService<IStaffSessions>().EndForAccountAsync(temp.AccountId, SessionEndings.EndedForAccount, default));
        var role = await world.Factory.RolesAsAsync(
            world.Clinic, manager, roles => roles.CreateAsync("دور", null, [Permissions.BookingsView], default));
        await world.Factory.RolesAsAsync(
            world.Clinic, manager, roles => roles.UpdateAsync(role, "دور آخر", null, [Permissions.BookingsView, Permissions.PatientsView], default));
        await world.Factory.RolesAsAsync(world.Clinic, manager, roles => roles.AssignAsync(temp.AccountId, role, default));
        var spare = await world.Factory.RolesAsAsync(
            world.Clinic, manager, roles => roles.CreateAsync("زائد", null, [Permissions.BookingsView], default));
        await world.Factory.RolesAsAsync(world.Clinic, manager, roles => roles.RemoveAsync(spare, default));

        // And turns the email of a notification off (spec 008).
        await world.Factory.AsStaffAsync(world.Clinic, manager, services =>
            services.GetRequiredService<IEmailSwitches>().SetAsync(NotificationEvents.Switchable[0].Key, false, "ar", default));

        var entries = await sql.Main.EntriesAsync(world.Clinic);

        // Every action of the catalogue was written while all of that was in reach — but those
        // of files, which pass nothing of sign-in and have their own secrets test (spec 009), and
        // those of the operators' team, which are written to the platform's own trail and never
        // to a clinic's (spec 011: PlatformEntryTests, NobodyGainsTests), and those of the feature
        // catalogue, which are the platform's as well (spec 012: FeaturesAddTests and its kin) …
        Assert.Equal(
            AuditActions.All.Select(a => a.Code)
                .Where(code => !code.StartsWith("file.", StringComparison.Ordinal)
                               && !code.StartsWith("operator.", StringComparison.Ordinal)
                               && !code.StartsWith("opsrole.", StringComparison.Ordinal)
                               && !code.StartsWith("feature.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
            entries.Select(e => e.ActionCode).Distinct().Order(StringComparer.Ordinal));

        // … and none of what was typed or issued is in any column of it.
        var stored = string.Join('\n', entries.Select(e => string.Join('|',
            e.ActorName, e.ActionCode, e.Category, e.TargetKind, e.TargetName, e.TargetReadyKey, e.Facts, e.Changes,
            e.IpAddress, e.UserAgent, e.TraceId)));

        Assert.All(secrets.Where(s => s.Length > 0), secret => Assert.DoesNotContain(secret, stored, StringComparison.Ordinal));

        // An account is named by reference: its address is the actor's name of the moment and nothing else.
        var besidesTheActor = string.Join('\n', entries.Select(e => string.Join('|', e.TargetName, e.Facts, e.Changes, e.UserAgent)));
        Assert.DoesNotContain(localPart, besidesTheActor, StringComparison.Ordinal);
        Assert.DoesNotContain("zzqclinic", besidesTheActor, StringComparison.Ordinal);
        Assert.DoesNotContain("zzq.", besidesTheActor, StringComparison.Ordinal);
        Assert.All(entries.Where(e => e.TargetKind == "account"), e => Assert.Null(e.TargetName));
    }

    private static IEnumerable<string> CookieValues(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(cookie => cookie.Split(';')[0].Split('=', 2)[1]).Where(value => value.Length > 8)
            : [];
}
