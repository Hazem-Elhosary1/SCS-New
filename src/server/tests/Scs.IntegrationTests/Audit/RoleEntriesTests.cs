using Scs.BuildingBlocks.Errors;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 1 (FR-013, FR-025 to FR-027; constitution IV): every change to who may
/// do what leaves one entry, with the value before and after. The role pages arrive with spec
/// 027, so these changes are made through the module's own operations.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RoleEntriesTests(SqlServerFixture sql)
{
    private static string[] Texts(System.Text.Json.JsonElement list) =>
        [.. list.EnumerateArray().Select(item => item.GetString()!)];

    [Fact]
    public async Task Creating_a_role_records_its_name_and_the_permissions_it_holds()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, email) = await world.AccountAsync();

        var id = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.CreateAsync(
            "الاستقبال", "مكتب المواعيد", [Permissions.PatientsView, Permissions.BookingsView, Permissions.BookingsManage], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
        Assert.Equal((AuditActions.RoleCreated, "users"), (entry.ActionCode, entry.Category));
        Assert.Equal(("role", id, "الاستقبال", (string?)null), (entry.TargetKind, entry.TargetId, entry.TargetName, entry.TargetReadyKey));
        Assert.Equal(("staff", actor, email), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Null(entry.Facts);

        // In the catalogue's order, whatever order they were given in.
        Assert.Equal(
            [Permissions.BookingsView, Permissions.BookingsManage, Permissions.PatientsView],
            Texts(entry.Change("permissions").GetProperty("added")));
        Assert.Equal("الاستقبال", entry.Change("name").GetProperty("after").GetString());
        Assert.Equal("مكتب المواعيد", entry.Change("description").GetProperty("after").GetString());
    }

    [Fact]
    public async Task Editing_a_role_is_one_entry_with_everything_that_changed()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var id = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.CreateAsync(
            "الاستقبال", "مكتب المواعيد", [Permissions.BookingsView, Permissions.BookingsManage], default));

        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.UpdateAsync(
            id, "مكتب الاستقبال", "المواعيد والمرضى", [Permissions.BookingsView, Permissions.PatientsView], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleUpdated));
        Assert.Equal(("role", id, "مكتب الاستقبال"), (entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Equal(("الاستقبال", "مكتب الاستقبال"), Pair(entry.Change("name")));
        Assert.Equal(("مكتب المواعيد", "المواعيد والمرضى"), Pair(entry.Change("description")));
        Assert.Equal([Permissions.PatientsView], Texts(entry.Change("permissions").GetProperty("added")));
        Assert.Equal([Permissions.BookingsManage], Texts(entry.Change("permissions").GetProperty("removed")));
    }

    [Fact]
    public async Task Editing_one_thing_records_that_thing_alone()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var id = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.CreateAsync(
            "الاستقبال", "مكتب المواعيد", [Permissions.BookingsView], default));

        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.UpdateAsync(
            id, "الاستقبال", "مكتب المواعيد", [Permissions.BookingsView, Permissions.BookingsManage], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleUpdated));
        Assert.False(entry.HasChange("name"));
        Assert.False(entry.HasChange("description"));
        Assert.Equal([Permissions.BookingsManage], Texts(entry.Change("permissions").GetProperty("added")));
    }

    [Fact]
    public async Task Saving_a_role_exactly_as_it_is_records_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var id = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.CreateAsync(
            "الاستقبال", "مكتب المواعيد", [Permissions.BookingsView], default));

        var change = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.UpdateAsync(
            id, "الاستقبال", "مكتب المواعيد", [Permissions.BookingsView], default));

        Assert.Empty(change.Added);
        Assert.Empty(change.Removed);
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleUpdated));
    }

    [Fact]
    public async Task Deleting_a_role_records_its_name_and_what_it_held_readable_after_it_is_gone()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var id = await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.CreateAsync(
            "مؤقت", null, [Permissions.BookingsView, Permissions.PatientsView], default));

        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.RemoveAsync(id, default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleDeleted));
        Assert.Equal(("role", id, "مؤقت"), (entry.TargetKind, entry.TargetId, entry.TargetName));
        Assert.Equal("مؤقت", entry.Change("name").GetProperty("before").GetString());
        Assert.Equal(
            [Permissions.BookingsView, Permissions.PatientsView], Texts(entry.Change("permissions").GetProperty("removed")));

        var read = (await world.Factory.ReadTrailAsync(world.Clinic)).Entries.Single(e => e.Action.Code == AuditActions.RoleDeleted);
        Assert.Equal("حُذف الدور «مؤقت»", read.Sentence);
    }

    [Fact]
    public async Task Giving_a_person_another_role_records_the_role_before_and_after()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var (person, _) = await world.AccountAsync();
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        var custom = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);
        var customName = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == custom).Name;

        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.AssignAsync(person, doctor, default));
        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.AssignAsync(person, custom, default));

        var entries = await sql.Main.EntriesAsync(world.Clinic, AuditActions.AccountRoleChanged);
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry =>
        {
            // The account is named by reference alone (FR-018).
            Assert.Equal(("account", person, (string?)null), (entry.TargetKind, entry.TargetId, entry.TargetName));
            Assert.Equal(actor, entry.ActorAccountId);
        });

        // A ready role under its own name keeps its key, so it reads in the reader's language.
        var first = entries[0].Change("role");
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, Optional(first, "before").ValueKind);
        Assert.Equal(("طبيب", "doctor"), (first.GetProperty("after").GetString(), first.GetProperty("afterKey").GetString()));

        var second = entries[1].Change("role");
        Assert.Equal(("طبيب", "doctor"), (second.GetProperty("before").GetString(), second.GetProperty("beforeKey").GetString()));
        Assert.Equal(customName, second.GetProperty("after").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, Optional(second, "afterKey").ValueKind);
    }

    [Fact]
    public async Task Giving_a_person_the_role_they_already_hold_records_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var (person, _) = await world.AccountAsync();
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");

        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.AssignAsync(person, doctor, default));
        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.AssignAsync(person, doctor, default));

        Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.AccountRoleChanged));
    }

    [Fact]
    public async Task A_refused_change_records_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, _) = await world.AccountAsync();
        var (onlyOwner, _) = await world.AccountAsync();
        var owner = await world.Factory.ReadyRoleAsync(world.Clinic, "owner");
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        var inUse = await world.Factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(onlyOwner, owner, default));
        await world.Factory.RolesAsync(world.Clinic, r => r.AssignAsync(actor, inUse, default));
        var before = (await sql.Main.EntriesAsync(world.Clinic)).Count;

        Task<ScsProblemException> RefusedAsync(Func<Scs.Identity.Contracts.IClinicRoles, Task> work) =>
            Assert.ThrowsAsync<ScsProblemException>(() => world.Factory.RolesAsAsync(world.Clinic, actor, work));

        Assert.Equal("ROLE_FIXED", (await RefusedAsync(r => r.UpdateAsync(owner, "x", null, [Permissions.BookingsView], default))).Code);
        Assert.Equal("ROLE_FIXED", (await RefusedAsync(r => r.RemoveAsync(owner, default))).Code);
        Assert.Equal("ROLE_IN_USE", (await RefusedAsync(r => r.RemoveAsync(inUse, default))).Code);
        Assert.Equal("LAST_OWNER", (await RefusedAsync(r => r.AssignAsync(onlyOwner, doctor, default))).Code);
        Assert.Equal("NOT_FOUND", (await RefusedAsync(r => r.AssignAsync(Guid.NewGuid(), doctor, default))).Code);
        Assert.Equal(
            "VALIDATION_FAILED",
            (await RefusedAsync(r => r.CreateAsync("بلا عرض", null, [Permissions.AdminUsersManage], default))).Code);
        Assert.Equal(
            "VALIDATION_FAILED",
            (await RefusedAsync(r => r.UpdateAsync(inUse, "", null, [Permissions.BookingsView], default))).Code);

        // A name another role of the clinic already has.
        var taken = (await world.Factory.RolesAsync(world.Clinic, r => r.ListAsync(default))).Single(r => r.Id == inUse).Name;
        Assert.Equal("VALIDATION_FAILED", (await RefusedAsync(r => r.CreateAsync(taken, null, [Permissions.BookingsView], default))).Code);

        Assert.Equal(before, (await sql.Main.EntriesAsync(world.Clinic)).Count);
    }

    [Fact]
    public async Task Changing_ones_own_role_is_refused_and_records_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        var before = (await sql.Main.EntriesAsync(world.Clinic)).Count;

        using var refused = await owner.Browser.PostAsync(new Uri($"/probe/assign/{owner.AccountId}/{doctor}", UriKind.Relative), null);
        var (status, code, _) = await refused.ReadAsync();

        Assert.Equal((409, "OWN_ROLE"), (status, code));
        Assert.Equal(before, (await sql.Main.EntriesAsync(world.Clinic)).Count);
    }

    [Fact]
    public async Task The_roles_a_clinic_starts_with_are_recorded_as_the_platforms_doing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.ProvisionRolesAsync(world.Clinic);
        await world.Factory.ProvisionRolesAsync(world.Clinic);

        var entries = await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated);
        Assert.Equal(["doctor", "manager", "owner"], entries.Select(e => e.TargetReadyKey).Order(StringComparer.Ordinal));
        Assert.All(entries, entry => Assert.Equal(("system", (Guid?)null, (string?)null), (entry.ActorKind, entry.ActorAccountId, entry.ActorName)));

        // The owner role stores no rows; its entry still says it holds the whole catalogue.
        var owner = entries.Single(e => e.TargetReadyKey == "owner");
        Assert.Equal(PermissionCatalogue.Count, owner.Change("permissions").GetProperty("added").GetArrayLength());
        Assert.Equal(
            ReadySets.Doctor.Count,
            entries.Single(e => e.TargetReadyKey == "doctor").Change("permissions").GetProperty("added").GetArrayLength());

        // What the platform did is not part of what the clinic reads (FR-030).
        Assert.Empty((await world.Factory.ReadTrailAsync(world.Clinic)).Entries);
    }

    [Fact]
    public async Task The_roles_of_a_seeded_clinic_are_recorded_the_same_written_today_or_carried_over()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.True(await Migrator.Migrator.RunAsync(db.Migration, output, error, default) == 0, error.ToString());

        // A clinic seeded today: its roles — the three ready ones and the seeded custom one —
        // are recorded as they are created, as the platform's doing.
        var today = await db.SeedClinicAsync();
        await Scs.Identity.IdentityModule.SeedAsync(
            db.App, new Dictionary<string, Guid> { ["noor"] = today }, SignIn.Password, default);

        // A clinic holding the same roles from before the trail existed: no entries yet.
        var before = await db.SeedClinicAsync();
        await TestDatabase.ExecuteAsync(
            db.Crossing,
            $"""
             INSERT INTO [identity].Role (Id, ClinicId, Name, Description, Category, ReadyKey, IsRenamed, HoldsEverything, CreatedAtUtc)
             SELECT NEWID(), '{before}', Name, Description, Category, ReadyKey, IsRenamed, HoldsEverything, CreatedAtUtc
             FROM [identity].Role WHERE ClinicId = '{today}';

             INSERT INTO [identity].RolePermission (RoleId, Code, ClinicId)
             SELECT copy.Id, held.Code, '{before}'
             FROM [identity].RolePermission held
             JOIN [identity].Role original ON original.Id = held.RoleId AND original.ClinicId = '{today}'
             JOIN [identity].Role copy ON copy.ClinicId = '{before}' AND copy.Name = original.Name;
             """);
        Assert.Empty(await db.EntriesAsync(before));

        // What the migration of this spec does for it, run twice.
        await TestDatabase.ExecuteAsync(db.Migration, Scs.Identity.Infrastructure.Migrations.TrailBackfillSql.ExistingRoles);
        await TestDatabase.ExecuteAsync(db.Migration, Scs.Identity.Infrastructure.Migrations.TrailBackfillSql.ExistingRoles);

        static string Shape(StoredEntry entry) => string.Join(
            " | ",
            entry.ActionCode,
            entry.Category,
            entry.ActorKind,
            entry.ActorAccountId?.ToString() ?? "-",
            entry.TargetKind,
            entry.TargetName,
            entry.TargetReadyKey ?? "-",
            entry.Change("name").GetProperty("after").GetString(),
            Optional(entry.Change("name"), "afterKey").ValueKind == System.Text.Json.JsonValueKind.String
                ? entry.Change("name").GetProperty("afterKey").GetString()
                : "-",
            entry.HasChange("description") ? entry.Change("description").GetProperty("after").GetString() : "-",
            string.Join(',', Texts(entry.Change("permissions").GetProperty("added")).Order(StringComparer.Ordinal)));

        var written = (await db.EntriesAsync(today)).Select(Shape).Order(StringComparer.Ordinal).ToList();
        var carried = (await db.EntriesAsync(before)).Select(Shape).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(4, written.Count);
        Assert.Equal(written, carried);
        Assert.Contains(written, shape => shape.Contains(Scs.Identity.IdentityModule.SeedCustomRole, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_change_of_role_also_ends_the_persons_sessions_and_says_who_did_it()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);

        using var response = await owner.Browser.PostAsync(new Uri($"/probe/assign/{person.AccountId}/{doctor}", UriKind.Relative), null);
        Assert.True(response.IsSuccessStatusCode, $"Answered {(int)response.StatusCode}.");

        var changed = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.AccountRoleChanged), e =>
            e.TargetId == person.AccountId && e.ActorAccountId == owner.AccountId);
        var ended = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.SessionsEnded));

        Assert.Equal(("account", person.AccountId, owner.AccountId, owner.Email), (ended.TargetKind, ended.TargetId, ended.ActorAccountId, ended.ActorName));
        Assert.Equal(("role-changed", "all"), (ended.FactsJson.GetProperty("reason").GetString(), ended.FactsJson.GetProperty("scope").GetString()));
        Assert.Equal(changed.TraceId, ended.TraceId);

        // Two entries of one act share its moment.
        Assert.Equal(changed.OccurredAtUtc, ended.OccurredAtUtc);
    }

    private static (string? Before, string? After) Pair(System.Text.Json.JsonElement change) =>
        (change.GetProperty("before").GetString(), change.GetProperty("after").GetString());

    private static System.Text.Json.JsonElement Optional(System.Text.Json.JsonElement change, string name) =>
        change.TryGetProperty(name, out var found) ? found : default;
}
