using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.Audit.Contracts;
using Scs.BuildingBlocks.Audit;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 011, FR-048 (contracts/operator-access.md §5; constitution IV): what an operator does to
/// the platform itself is recorded in the platform's own trail — with the change or not at all,
/// naming the operator of the request, never changed afterwards, and never in a clinic's trail.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlatformEntryTests(SqlServerFixture sql)
{
    // Refused by the trigger, or — for a sign-in that may not write there at all — by the database.
    private static readonly int[] Refusals = [51007, 229];

    private Task<List<string>> EntriesAboutAsync(Guid target) => TestDatabase.ReadColumnAsync(
        sql.Main.Admin,
        $"""
         SELECT CONCAT(ActorKind, '|', ActorAccountId, '|', ActorName, '|', ActionCode, '|', Category, '|',
                       TargetKind, '|', TargetName, '|', Facts, '|', LEN(TraceId))
         FROM [audit].[PlatformEntry] WHERE TargetId = '{target}' ORDER BY OccurredAtUtc
         """);

    private static AuditRecord Suspended(Guid target) => new(
        AuditActions.OperatorSuspended,
        new AuditTarget(AuditTarget.Operator, target, "سارة أحمد"),
        new Dictionary<string, object?> { ["reason"] = "probe" });

    [Fact]
    public async Task What_an_operator_does_is_recorded_with_who_did_it_and_in_no_clinic_s_trail()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        var target = Guid.NewGuid();
        var name = await world.Factory.OpsAsync(db => db.Operators.Where(o => o.Id == world.OperatorId).Select(o => o.FullName).SingleAsync());

        using var done = await browser.OpsGetAsync($"/probe/ops/record/{target}");
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var entry = Assert.Single(await EntriesAboutAsync(target));
        Assert.Equal(
            $"operator|{world.OperatorId}|{name}|operator.suspended|users|operator|سارة أحمد|{{\"reason\":\"probe\"}}|32",
            entry,
            ignoreCase: true);

        // The clinics' trail holds nothing of it.
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(
            sql.Main.Admin, $"SELECT COUNT(*) FROM [audit].[Entry] WHERE TargetId = '{target}' OR ActionCode LIKE 'operator.%' OR ActionCode LIKE 'opsrole.%'"));

        // And the operator's own activity reads it back.
        using var scope = world.Factory.Services.CreateScope();
        var activity = await scope.ServiceProvider.GetRequiredService<IPlatformAuditReading>()
            .LastByActorAsync(world.OperatorId, 10, default);
        var last = activity[0];
        Assert.Equal(
            (AuditActions.OperatorSuspended, (Guid?)target, "سارة أحمد", "probe"),
            (last.Action, last.TargetId, last.TargetName, last.Facts["reason"]));
    }

    [Fact]
    public async Task When_the_change_does_not_go_through_neither_does_its_entry()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        var target = Guid.NewGuid();

        using var failed = await browser.OpsGetAsync($"/probe/ops/record/{target}?abandon=true");

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(await EntriesAboutAsync(target));
    }

    [Fact]
    public async Task An_entry_is_written_only_inside_the_transaction_of_its_change_and_only_for_a_known_action()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var target = Guid.NewGuid();
        using var scope = world.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();

        await Assert.ThrowsAsync<AuditRefusedException>(() => db.RecordForPlatformAsync(Suspended(target), default));

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<AuditRefusedException>(() => db.RecordForPlatformAsync(
                new AuditRecord("operator.no-such-action", new AuditTarget(AuditTarget.Operator, target, "x")), default));

            // A fact the catalogue does not list for the action is a defect in the caller.
            await Assert.ThrowsAsync<AuditRefusedException>(() => db.RecordForPlatformAsync(
                new AuditRecord(
                    AuditActions.OperatorSuspended,
                    new AuditTarget(AuditTarget.Operator, target, "x"),
                    new Dictionary<string, object?> { ["password"] = "x" }),
                default));
            await transaction.CommitAsync();
        }

        Assert.Empty(await EntriesAboutAsync(target));
    }

    [Fact]
    public async Task Where_there_is_no_request_the_platform_itself_is_the_actor()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var target = Guid.NewGuid();
        using var scope = world.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.RecordForPlatformAsync(Suspended(target), default);
            await transaction.CommitAsync();
        }

        var entry = Assert.Single(await EntriesAboutAsync(target));
        Assert.StartsWith("system|||operator.suspended|users|", entry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nobody_changes_or_removes_an_entry()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var browser = await world.SignInAsync();
        var target = Guid.NewGuid();
        using var done = await browser.OpsGetAsync($"/probe/ops/record/{target}");
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var before = await EntriesAboutAsync(target);

        foreach (var connection in new[] { sql.Main.App, sql.Main.Crossing, sql.Main.Admin })
        {
            var changed = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
                connection, $"UPDATE [audit].[PlatformEntry] SET TargetName = N'غيره' WHERE TargetId = '{target}'"));
            var removed = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
                connection, $"DELETE FROM [audit].[PlatformEntry] WHERE TargetId = '{target}'"));
            Assert.All([changed, removed], refusal => Assert.Contains(refusal.Number, Refusals));
        }

        Assert.Equal(before, await EntriesAboutAsync(target));
    }
}
