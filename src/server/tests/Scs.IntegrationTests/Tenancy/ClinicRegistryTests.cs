using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;

namespace Scs.IntegrationTests.Tenancy;

/// <summary>
/// Spec 002, User Stories 4 and 5: registering and renaming clinics through the operator
/// operations, and the record each use leaves.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicRegistryTests(SqlServerFixture sql)
{
    private const string Operator = "test:registry";

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private async Task<T> WithRegistryAsync<T>(Func<IClinicRegistry, Task<T>> work)
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IClinicRegistry>());
    }

    private async Task WithRegistryAsync(Func<IClinicRegistry, Task> work)
    {
        await WithRegistryAsync<object?>(async registry =>
        {
            await work(registry);
            return null;
        });
    }

    private static async Task<string> RefusedCodeAsync(Func<Task> work, string field = "shortName")
    {
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(work);
        Assert.Equal(ErrorCodes.ValidationFailed, refusal.Code);
        return refusal.Errors![field].Single();
    }

    [Fact]
    public async Task Registering_creates_the_clinic_and_its_current_name()
    {
        var name = Unique("reg");

        var id = await WithRegistryAsync(r => r.RegisterAsync($"  {name.ToUpperInvariant()} ", " عيادة ", " Clinic ", Operator, default));

        Assert.Equal("Clinic", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, $"SELECT NameEn FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal("active", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, $"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal(id, await TestDatabase.ScalarAsync<Guid>(sql.Main.App, $"SELECT ClinicId FROM tenancy.ClinicDirectory WHERE ShortName = '{name}' AND IsCurrent = 1"));
    }

    [Theory]
    [InlineData("ab", "TOO_SHORT")]
    [InlineData("1clinic", "INVALID_FORMAT")]
    [InlineData("عيادة", "INVALID_FORMAT")]
    [InlineData("api", "RESERVED")]
    [InlineData("Support", "RESERVED")]
    [InlineData("", "REQUIRED")]
    public async Task A_name_that_breaks_a_rule_is_refused_and_nothing_is_created(string name, string code)
    {
        var before = await WithRegistryAsync(async _ => await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM tenancy.Clinic"));

        Assert.Equal(code, await RefusedCodeAsync(() => WithRegistryAsync(r => r.RegisterAsync(name, "عيادة", "Clinic", Operator, default))));

        Assert.Equal(before, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM tenancy.Clinic"));
    }

    [Fact]
    public async Task Every_problem_is_reported_at_once()
    {
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => WithRegistryAsync(r => r.RegisterAsync("x", " ", new string('a', 201), Operator, default)));

        Assert.Equal(["TOO_SHORT"], refusal.Errors!["shortName"]);
        Assert.Equal(["REQUIRED"], refusal.Errors["nameAr"]);
        Assert.Equal(["TOO_LONG"], refusal.Errors["nameEn"]);
    }

    [Fact]
    public async Task A_name_another_clinic_has_is_taken()
    {
        var name = Unique("taken");
        await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة", "First", Operator, default));

        Assert.Equal("TAKEN", await RefusedCodeAsync(
            () => WithRegistryAsync(r => r.RegisterAsync(name.ToUpperInvariant(), "عيادة", "Second", Operator, default))));
    }

    [Fact]
    public async Task After_a_rename_the_old_name_still_leads_to_the_clinic_and_the_new_one_is_current()
    {
        var first = Unique("old");
        var second = Unique("new");
        var third = Unique("newer");
        var id = await WithRegistryAsync(r => r.RegisterAsync(first, "عيادة", "Clinic", Operator, default));

        await WithRegistryAsync(r => r.RenameAsync(id, second, Operator, default));
        await WithRegistryAsync(r => r.RenameAsync(id, third, Operator, default));

        foreach (var name in new[] { first, second, third })
        {
            Assert.Equal(id, await TestDatabase.ScalarAsync<Guid>(sql.Main.App, $"SELECT ClinicId FROM tenancy.ClinicDirectory WHERE ShortName = '{name}'"));
        }

        Assert.Equal(third, await TestDatabase.ScalarAsync<string>(sql.Main.App, $"SELECT ShortName FROM tenancy.ClinicDirectory WHERE ClinicId = '{id}' AND IsCurrent = 1"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.App, $"SELECT COUNT(*) FROM tenancy.ClinicDirectory WHERE ClinicId = '{id}' AND IsCurrent = 1"));
    }

    [Fact]
    public async Task An_earlier_name_can_never_be_given_to_another_clinic()
    {
        var first = Unique("mine");
        var id = await WithRegistryAsync(r => r.RegisterAsync(first, "عيادة", "Clinic", Operator, default));
        await WithRegistryAsync(r => r.RenameAsync(id, Unique("moved"), Operator, default));
        var other = await WithRegistryAsync(r => r.RegisterAsync(Unique("other"), "عيادة", "Other", Operator, default));

        Assert.Equal("TAKEN", await RefusedCodeAsync(() => WithRegistryAsync(r => r.RegisterAsync(first, "عيادة", "Third", Operator, default))));
        Assert.Equal("TAKEN", await RefusedCodeAsync(() => WithRegistryAsync(r => r.RenameAsync(other, first, Operator, default))));
    }

    [Fact]
    public async Task A_clinic_may_take_back_one_of_its_own_earlier_names()
    {
        var first = Unique("back");
        var id = await WithRegistryAsync(r => r.RegisterAsync(first, "عيادة", "Clinic", Operator, default));
        await WithRegistryAsync(r => r.RenameAsync(id, Unique("away"), Operator, default));

        await WithRegistryAsync(r => r.RenameAsync(id, first, Operator, default));

        Assert.Equal(first, await TestDatabase.ScalarAsync<string>(sql.Main.App, $"SELECT ShortName FROM tenancy.ClinicDirectory WHERE ClinicId = '{id}' AND IsCurrent = 1"));
        Assert.Equal(2, await TestDatabase.ScalarAsync<int>(sql.Main.App, $"SELECT COUNT(*) FROM tenancy.ClinicDirectory WHERE ClinicId = '{id}'"));
    }

    [Fact]
    public async Task Renaming_to_the_current_name_changes_nothing_and_an_unknown_clinic_is_not_found()
    {
        var name = Unique("same");
        var id = await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة", "Clinic", Operator, default));

        await WithRegistryAsync(r => r.RenameAsync(id, name, Operator, default));
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => WithRegistryAsync(r => r.RenameAsync(Guid.CreateVersion7(), Unique("ghost"), Operator, default)));

        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.App, $"SELECT COUNT(*) FROM tenancy.ClinicDirectory WHERE ClinicId = '{id}'"));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
    }

    [Fact]
    public async Task Listing_returns_every_clinic_with_its_current_name()
    {
        var name = Unique("list");
        var id = await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة القائمة", "Listed", Operator, default));
        var renamed = Unique("listed");
        await WithRegistryAsync(r => r.RenameAsync(id, renamed, Operator, default));

        var all = await WithRegistryAsync(r => r.ListAsync(Operator, default));

        var mine = Assert.Single(all, c => c.Id == id);
        Assert.Equal(renamed, mine.ShortName);
        Assert.Equal("Listed", mine.NameEn);
        Assert.True(all.Count >= 1);
    }

    [Fact]
    public async Task Each_operation_by_a_person_leaves_exactly_one_access_record_with_identifiers_only()
    {
        var who = $"test:{Guid.NewGuid():N}";
        var name = Unique("rec");

        var id = await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة السجل", "Recorded Clinic", who, default));
        await WithRegistryAsync(r => r.RenameAsync(id, Unique("rec2"), who, default));
        await WithRegistryAsync(r => r.ListAsync(who, default));
        await WithRegistryAsync(r => r.ListClinicIdsForJobsAsync(default));

        string Count(string operation) => $"SELECT COUNT(*) FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}' AND Operation = '{operation}'";
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, Count("register-clinic")));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, Count("rename-clinic")));
        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, Count("list-clinics")));
        Assert.Equal(3, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}'"));

        Assert.Equal(id, await TestDatabase.ScalarAsync<Guid>(sql.Main.Crossing, $"SELECT ClinicId FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}' AND Operation = 'register-clinic'"));
        Assert.Null(await TestDatabase.ScalarAsync<object>(sql.Main.Crossing, $"SELECT ClinicId FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}' AND Operation = 'list-clinics'"));

        // The record has no column that could hold a name, and none of its text does.
        var columns = await TestDatabase.ScalarAsync<string>(sql.Main.Admin,
            "SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY column_id) FROM sys.columns WHERE object_id = OBJECT_ID('crossing.CrossClinicAccess')");
        Assert.Equal("Id,Operation,ClinicId,OperatorId,TraceId,OccurredAtUtc", columns);
    }

    [Fact]
    public async Task A_refused_operation_leaves_no_access_record()
    {
        var who = $"test:{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<ScsProblemException>(() => WithRegistryAsync(r => r.RegisterAsync("api", "عيادة", "Clinic", who, default)));

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}'"));
    }

    [Fact]
    public async Task The_directory_is_readable_without_a_clinic_and_not_writable_by_the_application()
    {
        var name = Unique("dir");
        var id = await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة", "Clinic", Operator, default));

        Assert.Equal(id, await TestDatabase.ScalarAsync<Guid>(sql.Main.App, $"SELECT ClinicId FROM tenancy.ClinicDirectory WHERE ShortName = '{name}'"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, $"UPDATE tenancy.ClinicDirectory SET ClinicId = NEWID() WHERE ShortName = '{name}'", id));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, $"DELETE FROM tenancy.ClinicDirectory WHERE ShortName = '{name}'", id));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, $"INSERT INTO tenancy.ClinicDirectory (ShortName, ClinicId, IsCurrent, AssignedAtUtc) VALUES ('{Unique("x")}', '{id}', 0, SYSDATETIMEOFFSET())", id));
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_current_name_and_a_reused_name()
    {
        var name = Unique("db");
        var id = await WithRegistryAsync(r => r.RegisterAsync(name, "عيادة", "Clinic", Operator, default));
        var other = await sql.Main.SeedClinicAsync();

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Crossing,
            $"INSERT INTO tenancy.ClinicDirectory (ShortName, ClinicId, IsCurrent, AssignedAtUtc) VALUES ('{Unique("two")}', '{id}', 1, SYSDATETIMEOFFSET())"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Crossing,
            $"INSERT INTO tenancy.ClinicDirectory (ShortName, ClinicId, IsCurrent, AssignedAtUtc) VALUES ('{name}', '{other}', 1, SYSDATETIMEOFFSET())"));
    }

    [Fact]
    public async Task An_access_record_cannot_be_altered_by_any_login()
    {
        var who = $"test:{Guid.NewGuid():N}";
        await WithRegistryAsync(r => r.ListAsync(who, default));
        var change = $"UPDATE crossing.CrossClinicAccess SET OperatorId = 'someone-else' WHERE OperatorId = '{who}'";

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Crossing, change));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Migration, change));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, change));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Report, change));

        Assert.Equal(1, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM crossing.CrossClinicAccess WHERE OperatorId = '{who}'"));
    }

    [Fact]
    public async Task The_application_login_cannot_read_or_write_the_access_records()
    {
        await sql.EnsureMigratedAsync();

        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "SELECT COUNT(*) FROM crossing.CrossClinicAccess"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.Report, "SELECT COUNT(*) FROM crossing.CrossClinicAccess"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App, "DELETE FROM crossing.CrossClinicAccess"));
        Assert.Equal(-1, await TestDatabase.TryExecuteAsync(sql.Main.App,
            "INSERT INTO crossing.CrossClinicAccess (Operation, OperatorId, OccurredAtUtc) VALUES ('x', 'x', SYSDATETIMEOFFSET())"));
    }
}
