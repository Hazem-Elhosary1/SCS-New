using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 007, User Story 5 (FR-023, FR-024; SC-001): no route that changes something ships
/// without saying what it leaves in the audit trail, and no scheduled job either. This is the
/// check that fails the build for one that says nothing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RouteAuditDeclarationTests(SqlServerFixture sql)
{
    private static readonly string[] Changing = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>What every changing route of the platform declares today (data-model.md §5).</summary>
    private static readonly Dictionary<string, AuditDeclarationKind> Declared = new(StringComparer.Ordinal)
    {
        ["POST /v1/auth/sign-in"] = AuditDeclarationKind.RecordedBySignIn,
        ["POST /v1/auth/sign-in/code"] = AuditDeclarationKind.RecordedBySignIn,
        ["POST /v1/auth/sign-in/code/resend"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/auth/session/renew"] = AuditDeclarationKind.RecordedBySignIn,
        ["POST /v1/auth/sign-out"] = AuditDeclarationKind.RecordedBySignIn,
        ["POST /v1/auth/password/forgot"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/auth/password/link"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/auth/password/link/set"] = AuditDeclarationKind.RecordedBySignIn,
        ["POST /v1/auth/password/change"] = AuditDeclarationKind.RecordedBySignIn,
        ["PUT /v1/account/preferences"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/client-errors"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/dev/staff-invitations"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/notifications/{id:guid}/read"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/notifications/read-all"] = AuditDeclarationKind.RecordsNothing,

        // Spec 010: an operator's sign-in events are kept in the operators' own record.
        ["POST /v1/ops/auth/sign-in"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/code"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/code/resend"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/code/cancel"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/session/renew"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/sign-out"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/auth/sign-out-everywhere"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/password/forgot"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/password/link"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/password/set"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/profile/password"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/profile/password/confirm"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/profile/password/resend"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/ops/profile/password/cancel"] = AuditDeclarationKind.RecordsNothing,

        // Spec 011: each is recorded in the platform's own trail.
        ["POST /v1/ops/operators"] = AuditDeclarationKind.Records,
        ["PUT /v1/ops/operators/{id:guid}"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/operators/{id:guid}/suspend"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/operators/{id:guid}/reactivate"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/operators/{id:guid}/password-link"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/operators/{id:guid}/unlock"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/roles"] = AuditDeclarationKind.Records,
        ["PUT /v1/ops/roles/{id:guid}"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/roles/{id:guid}/delete"] = AuditDeclarationKind.Records,
        // Spec 012: the feature catalogue, written to the platform's own trail.
        ["POST /v1/ops/features"] = AuditDeclarationKind.Records,
        ["PUT /v1/ops/features/{key}"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/features/{key}/retire"] = AuditDeclarationKind.Records,
        ["POST /v1/ops/features/{key}/resume"] = AuditDeclarationKind.Records,
        ["PUT /v1/settings/notification-emails/{eventKey}"] = AuditDeclarationKind.Records,
        ["POST /v1/files"] = AuditDeclarationKind.RecordsNothing,
        ["DELETE /v1/files/{id:guid}"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/files/{id:guid}/access"] = AuditDeclarationKind.Records,
        ["POST /v1/dev/files/sample"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/dev/files/sample/{id:guid}/remove"] = AuditDeclarationKind.RecordsNothing,
        ["POST /v1/dev/files/sample/{id:guid}/restore"] = AuditDeclarationKind.RecordsNothing,
    };

    private static string Describe(RouteEndpoint endpoint) => $"{Method(endpoint)} {endpoint.RoutePattern.RawText}";

    private static string Method(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods ? methods[0] : "ANY";

    private static List<RouteEndpoint> ChangingRoutes(IServiceProvider services) =>
    [
        .. services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => !(e.RoutePattern.RawText ?? "").StartsWith("/probe", StringComparison.Ordinal))
            .Where(e => Changing.Contains(Method(e))),
    ];

    /// <summary>The check itself, so it can be shown to fail.</summary>
    private static List<string> Undeclared(IEnumerable<RouteEndpoint> routes) =>
    [
        .. routes
            .Where(e => e.Metadata.OfType<AuditDeclarationMetadata>().Count() != 1)
            .Select(Describe),
    ];

    [Fact]
    public async Task Every_changing_route_says_what_it_records_or_why_it_records_nothing()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var routes = ChangingRoutes(factory.Services);

        Assert.NotEmpty(routes);

        var offenders = Undeclared(routes);
        Assert.True(
            offenders.Count == 0,
            "Say what each of these leaves in the audit trail — Records(...), RecordsNothing(reason) or RecordedBySignIn(): "
            + string.Join(", ", offenders));
    }

    [Fact]
    public async Task The_routes_of_today_declare_what_the_data_model_lists()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var found = ChangingRoutes(factory.Services)
            .ToDictionary(Describe, e => e.Metadata.GetMetadata<AuditDeclarationMetadata>()!.Kind, StringComparer.Ordinal);

        Assert.Equal(Declared.OrderBy(d => d.Key, StringComparer.Ordinal), found.OrderBy(d => d.Key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Whoever_records_nothing_says_why_and_whoever_records_names_actions_of_the_catalogue()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        foreach (var route in ChangingRoutes(factory.Services))
        {
            var declaration = route.Metadata.GetMetadata<AuditDeclarationMetadata>()!;

            if (declaration.Kind == AuditDeclarationKind.RecordsNothing)
            {
                Assert.False(string.IsNullOrWhiteSpace(declaration.Reason), Describe(route));
            }

            if (declaration.Kind == AuditDeclarationKind.Records)
            {
                Assert.NotEmpty(declaration.Actions);
                Assert.All(declaration.Actions, code => Assert.True(AuditActions.Find(code) is not null, $"{Describe(route)}: {code}"));
            }
        }
    }

    [Fact]
    public async Task A_route_that_only_reads_needs_no_declaration()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var reads = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => Method(e) == "GET" && (e.RoutePattern.RawText ?? "").StartsWith("/v1/", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(reads);
        Assert.All(reads, e => Assert.Empty(e.Metadata.OfType<AuditDeclarationMetadata>()));
    }

    [Fact]
    public async Task A_changing_route_added_without_a_declaration_is_caught_by_name()
    {
        var app = WebApplication.CreateSlimBuilder().Build();
        await using (app.ConfigureAwait(true))
        {
            app.MapPost("/v1/refunds", () => "x");
            app.MapDelete("/v1/refunds/{id}", (string id) => id).Records(AuditActions.RoleDeleted);
            app.MapPut("/v1/things", () => "x").RecordsNothing("a test");
            app.MapGet("/v1/refunds", () => "x");

            var routes = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => Changing.Contains(Method(e)));

            Assert.Equal(["POST /v1/refunds"], Undeclared(routes));
        }
    }

    [Fact]
    public async Task A_route_cannot_declare_twice_nor_with_nothing_to_say()
    {
        var app = WebApplication.CreateSlimBuilder().Build();
        await using (app.ConfigureAwait(true))
        {
            app.MapPost("/twice", () => "x").RecordedBySignIn().RecordsNothing("and nothing");

            Assert.Throws<InvalidOperationException>(
                () => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList());
        }

        Assert.Throws<ArgumentException>(() => AuditDeclarationMetadata.RecordsNothing("  "));
        Assert.Throws<ArgumentException>(() => AuditDeclarationMetadata.Records());
    }

    /// <summary>Notes every job type a module schedules, whichever scheduler it uses.</summary>
    private sealed class Scheduled : IJobScheduler, IClinicJobScheduler
    {
        public HashSet<Type> Jobs { get; } = [];

        public void AddOrUpdateRecurring<TJob>(string jobName, System.Linq.Expressions.Expression<Func<TJob, Task>> run, string cron)
            where TJob : notnull => Jobs.Add(typeof(TJob));

        public string EnqueueForClinic<TJob>(Guid clinicId)
            where TJob : IClinicJob
        {
            Jobs.Add(typeof(TJob));
            return "";
        }

        public bool TryEnqueueForClinic<TJob>(Guid clinicId)
            where TJob : IClinicJob
        {
            Jobs.Add(typeof(TJob));
            return true;
        }

        public void AddOrUpdateRecurringPerClinic<TJob>(string jobName, string cron)
            where TJob : IClinicJob => Jobs.Add(typeof(TJob));
    }

    [Fact]
    public async Task Every_scheduled_job_says_what_it_records_or_why_it_records_nothing()
    {
        await sql.EnsureMigratedAsync();

        // What the modules register when the host starts, with either scheduler …
        var scheduled = new Scheduled();
        using var factory = sql.CreateApi().WithServices(services => services.AddSingleton<IClinicJobScheduler>(scheduled));
        foreach (var registration in factory.Services.GetServices<IRecurringJobRegistration>())
        {
            registration.Register(scheduled);
        }

        // … and every per-clinic job there is, scheduled on a timetable or asked for when needed.
        var jobs = Migrator.Migrator.Modules
            .Select(module => module.GetType().Assembly)
            .Append(typeof(Scs.Platform.PlatformModule).Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IClinicJob).IsAssignableFrom(type))
            .Union(scheduled.Jobs)
            .ToList();

        // The heartbeat and the purge of access records are scheduled without a clinic.
        Assert.Contains(jobs, job => job.Name == "HeartbeatJob");
        Assert.Contains(jobs, job => job.Name == "PurgeAccessRecordsJob");
        Assert.Contains(jobs, job => job.Name == "CleanupJob");
        Assert.Contains(jobs, job => job.Name == "AccountEmailJob");

        var offenders = jobs
            .Where(job =>
                (job.GetCustomAttribute<RecordsAttribute>() is null ? 0 : 1)
                + (job.GetCustomAttribute<RecordsNothingAttribute>() is null ? 0 : 1) != 1)
            .Select(job => job.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Put [Records(...)] or [RecordsNothing(reason)] on: " + string.Join(", ", offenders));

        Assert.All(jobs, job =>
        {
            if (job.GetCustomAttribute<RecordsNothingAttribute>() is { } nothing)
            {
                Assert.False(string.IsNullOrWhiteSpace(nothing.Reason), job.FullName);
            }

            if (job.GetCustomAttribute<RecordsAttribute>() is { } records)
            {
                Assert.NotEmpty(records.Actions);
                Assert.All(records.Actions, code => Assert.True(AuditActions.Find(code) is not null, $"{job.FullName}: {code}"));
            }
        });
    }
}
