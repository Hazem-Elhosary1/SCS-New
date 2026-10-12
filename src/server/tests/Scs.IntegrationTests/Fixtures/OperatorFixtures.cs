using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure;
using Scs.Operators.Infrastructure.Persistence;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>A row of <c>ops.Session</c>, as tests read it.</summary>
public sealed record SessionRow(Guid Id, DateTimeOffset? EndedAtUtc, string? EndedFor, bool Remember);

/// <summary>A row of <c>ops.SignInRecord</c>, as tests read it.</summary>
public sealed record RecordRow(string Kind, Guid? OperatorId, string? Facts);

/// <summary>
/// One test's own application and one operator in it (spec 010). The request limit is raised so
/// that a test of something else is not stopped by it; the test of the limit lowers it again.
/// </summary>
public sealed partial class OpsWorld : IDisposable
{
    public const string Password = "Operator-Horse-9!";

    private OpsWorld(ApiFactory factory, Guid operatorId, string email)
    {
        Factory = factory;
        OperatorId = operatorId;
        Email = email;
    }

    public ApiFactory Factory { get; }

    public Guid OperatorId { get; }

    public string Email { get; }

    public static async Task<OpsWorld> CreateAsync(
        SqlServerFixture sql, string state = OperatorStates.Active, Func<ApiFactory, ApiFactory>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        await sql.EnsureMigratedAsync();

        var factory = sql.CreateApi().With("Scs:AUTH:REQUESTS_PER_MINUTE", "120");
        factory = configure?.Invoke(factory) ?? factory;
        var (id, email) = await factory.CreateOperatorAsync(state);
        return new OpsWorld(factory, id, email);
    }

    public void Dispose() => Factory.Dispose();

    [GeneratedRegex(@"#token=([A-Za-z0-9_\-]+)")]
    public static partial Regex TokenPattern();
}

public static class OpsHelp
{
    /// <summary>Creates an operator in a state, through the module's own entities.</summary>
    public static async Task<(Guid Id, string Email)> CreateOperatorAsync(
        this ApiFactory factory, string state = OperatorStates.Active, string? email = null, string language = "ar")
    {
        ArgumentNullException.ThrowIfNull(factory);
        email ??= $"operator-{Guid.NewGuid():N}@heta.example";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
        var account = Operator.Invite("سارة أحمد", email, factory.Clock.GetUtcNow());
        if (state != OperatorStates.Invited)
        {
            account.Activate(PasswordHasher.Hash(OpsWorld.Password), factory.Clock.GetUtcNow());
        }

        db.Operators.Add(account);
        await db.SaveChangesAsync();

        if (state == OperatorStates.Suspended || language != "ar")
        {
            await factory.OpsSqlAsync(
                $"UPDATE [ops].[Operator] SET [State] = '{state}', [Language] = '{language}' WHERE [Id] = '{account.Id}'");
        }

        return (account.Id, email);
    }

    public static async Task OpsSqlAsync(this ApiFactory factory, string sql)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database, sql);
    }

    public static async Task<T> OpsAsync<T>(this ApiFactory factory, Func<OperatorsDbContext, Task<T>> read)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(read);
        using var scope = factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<OperatorsDbContext>());
    }

    public static Task<List<SessionRow>> SessionsAsync(this OpsWorld world) =>
        world.Factory.OpsAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Sessions.Where(s => s.OperatorId == world.OperatorId).OrderBy(s => s.StartedAtUtc)
                .Select(s => new SessionRow(s.Id, s.EndedAtUtc, s.EndedFor, s.Remember))));

    public static Task<List<RecordRow>> RecordsAsync(this OpsWorld world, string? kind = null) =>
        world.Factory.OpsAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Records.Where(r => r.OperatorId == world.OperatorId && (kind == null || r.Kind == kind))
                .OrderBy(r => r.OccurredAtUtc).Select(r => new RecordRow(r.Kind, r.OperatorId, r.Facts))));

    public static Task<HttpResponseMessage> OpsStepOneAsync(
        this HttpClient browser, string email, string password = OpsWorld.Password, bool remember = false) =>
        browser.PostAsJsonAsync("/v1/ops/auth/sign-in", new { email, password, remember });

    public static Task<HttpResponseMessage> OpsStepTwoAsync(this HttpClient browser, string code) =>
        browser.PostAsJsonAsync("/v1/ops/auth/code", new { code });

    public static Task<HttpResponseMessage> OpsPostAsync(this HttpClient browser, string path, object? body = null) =>
        body is null
            ? browser.PostAsync(new Uri(path, UriKind.Relative), content: null)
            : browser.PostAsJsonAsync(path, body);

    public static Task<HttpResponseMessage> OpsGetAsync(this HttpClient browser, string path) =>
        browser.GetAsync(new Uri(path, UriKind.Relative));

    /// <summary>Completes both steps and returns a browser that carries the operator's session.</summary>
    public static async Task<HttpClient> SignInAsync(this OpsWorld world, bool remember = false, HttpClient? browser = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        browser ??= world.Factory.Browser();
        using var first = await browser.OpsStepOneAsync(world.Email, remember: remember);
        Assert.True(first.IsSuccessStatusCode, $"Step one answered {(int)first.StatusCode}.");
        using var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(world.Email));
        Assert.True(second.IsSuccessStatusCode, $"Step two answered {(int)second.StatusCode}.");
        return browser;
    }

    /// <summary>The token of the newest link emailed to an address.</summary>
    public static string LatestLinkToken(this ApiFactory factory, string email)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var message = factory.Mail.To(email).Last(m => OpsWorld.TokenPattern().IsMatch(m.TextBody));
        return OpsWorld.TokenPattern().Match(message.TextBody).Groups[1].Value;
    }

    /// <summary>Runs the job that sends invitations, as the worker does every minute.</summary>
    public static async Task<int> SendInvitationsAsync(this ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OperatorInviteJob>().RunAsync(CancellationToken.None);
    }

    public static async Task<Scs.Operators.Infrastructure.InviteResult> InviteAsync(this ApiFactory factory, string name, string email)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OperatorInvite>().InviteAsync(name, email, "test", CancellationToken.None);
    }

    public static async Task<Scs.Operators.Infrastructure.InviteResult> WithdrawAsync(this ApiFactory factory, string email)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OperatorInvite>().WithdrawAsync(email, "test", CancellationToken.None);
    }
}
