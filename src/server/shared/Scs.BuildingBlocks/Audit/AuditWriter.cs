using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tracing;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;

namespace Scs.BuildingBlocks.Audit;

/// <summary>The audit trail's table, owned by the Audit module and written from here (spec 007).</summary>
public static class AuditSql
{
    public const string Schema = "audit";
    public const string Table = "Entry";
    public const string AppendOnlyTrigger = "TR_Entry_AppendOnly";

    public const int ActorNameMaxLength = 254;
    public const int TargetNameMaxLength = 200;
    public const int IpAddressMaxLength = 45;
    public const int UserAgentMaxLength = 256;
    public const int FactTextMaxLength = 200;

    public const string ActorStaff = "staff";
    public const string ActorOperator = "operator";
    public const string ActorSystem = "system";

    internal const string Insert =
        $"""
         INSERT INTO [{Schema}].[{Table}]
             (Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category,
              TargetKind, TargetId, TargetName, TargetReadyKey, Facts, Changes, IpAddress, UserAgent, TraceId)
         VALUES
             (@id, @clinic, @at, @actorKind, @actor, @actorName, @action, @category,
              @targetKind, @targetId, @targetName, @targetReadyKey, @facts, @changes, @ip, @userAgent, @trace);
         """;

    /// <summary>
    /// Nothing changes an entry and nothing removes one: not the application's login, not the
    /// operator connection, not the migrator (FR-020, FR-021). When a retention period is ever
    /// set (SH-095), the spec that builds it replaces this trigger in a reviewed migration.
    /// </summary>
    public const string CreateAppendOnlyTrigger =
        $"""
         CREATE TRIGGER [{Schema}].[{AppendOnlyTrigger}]
         ON [{Schema}].[{Table}]
         AFTER UPDATE, DELETE
         AS
         BEGIN
             SET NOCOUNT ON;

             IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                 RETURN;

             THROW 51007, N'Audit entries cannot be changed or removed.', 1;
         END
         """;

    public const string DropAppendOnlyTrigger = $"DROP TRIGGER [{Schema}].[{AppendOnlyTrigger}];";
}

/// <summary>Who is acting and from where, on this piece of work. Taken by the trail, never by the caller.</summary>
public sealed record AuditActor(
    string Kind, Guid? AccountId, [property: PersonalData] string? Name, AuditOrigin Origin);

/// <summary>Gives an account's display name at this moment: the email address until accounts have names (spec 026).</summary>
public interface IAuditActorNames
{
    Task<string?> NameOfAsync(Guid accountId, CancellationToken cancellation);
}

/// <summary>
/// Lets a piece of work that is not a request — a job, a test — say who it acts for. Empty by
/// default: with no signed-in person and nothing set here, the platform itself is the actor
/// (FR-015). It is not read at all while a request is being answered: there the actor is the
/// signed-in person or nobody, and no code can name another (FR-014). No module may use it; an
/// architecture test says so.
/// </summary>
public sealed class AuditActorScope
{
    public Guid? AccountId { get; set; }

    public AuditOrigin Origin { get; set; } = AuditOrigin.None;
}

/// <summary>The entry could not be written because of a defect in whoever recorded it.</summary>
public sealed class AuditRefusedException(string message) : InvalidOperationException(message);

public static class AuditExtensions
{
    public static IServiceCollection AddScsAudit(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IAuditCatalogue, AuditCatalogue>();
        services.TryAddScoped<AuditActorScope>();
        return services;
    }
}

/// <summary>
/// Writes audit entries on the connection and inside the transaction of the change they belong
/// to, so the two take effect together or not at all (spec 007, FR-009; constitution IV).
/// </summary>
internal static class AuditWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The platform's clock, as the context that is saving knows it (FR-004).</summary>
    public static DateTimeOffset Now(DbContext db) =>
        (Services(db)?.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

    private static IServiceProvider? Services(DbContext db) =>
        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;

    /// <param name="now">The moment of the unit of work: every entry of one transaction carries it.</param>
    public static async Task WriteAsync(
        DbContext db, Guid clinicId, IReadOnlyList<AuditRecord> records, DateTimeOffset now, CancellationToken cancellation)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction()
                          ?? throw new AuditRefusedException("An audit entry is written only inside the transaction of its change.");

        var services = Services(db);
        var catalogue = services?.GetService<IAuditCatalogue>() ?? new AuditCatalogue();
        AuditActor? ambient = null;

        foreach (var record in records)
        {
            var action = Check(record, catalogue);
            var changes = record.Changes?.Where(change => !change.IsEmpty).ToList();

            // An action that leaves everything as it was records nothing (FR-012).
            if (record.Changes is { Count: > 0 } && changes!.Count == 0)
            {
                continue;
            }

            var actor = record.BySystem
                ? new AuditActor(AuditSql.ActorSystem, null, null, AuditOrigin.None)
                : record.SignIn is { } signIn
                ? new AuditActor(
                    AuditSql.ActorStaff,
                    signIn.AccountId,
                    await NameOfAsync(services, signIn.AccountId, cancellation).ConfigureAwait(false),
                    signIn.Origin)
                : ambient ??= await AmbientAsync(services, cancellation).ConfigureAwait(false);

            var command = db.Database.GetDbConnection().CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = transaction;
                command.CommandText = AuditSql.Insert;
                Add(command, "@id", SqlDbType.UniqueIdentifier, Guid.CreateVersion7(now));
                Add(command, "@clinic", SqlDbType.UniqueIdentifier, clinicId);
                Add(command, "@at", SqlDbType.DateTimeOffset, now);
                Add(command, "@actorKind", SqlDbType.VarChar, actor.Kind);
                Add(command, "@actor", SqlDbType.UniqueIdentifier, actor.AccountId);
                Add(command, "@actorName", SqlDbType.NVarChar, Cut(actor.Name, AuditSql.ActorNameMaxLength));
                Add(command, "@action", SqlDbType.VarChar, action.Code);
                Add(command, "@category", SqlDbType.VarChar, action.Category);
                Add(command, "@targetKind", SqlDbType.VarChar, record.Target?.Kind);
                Add(command, "@targetId", SqlDbType.UniqueIdentifier, record.Target?.Id);
                Add(command, "@targetName", SqlDbType.NVarChar, Cut(record.Target?.Name, AuditSql.TargetNameMaxLength));
                Add(command, "@targetReadyKey", SqlDbType.VarChar, record.Target?.ReadyKey);
                Add(command, "@facts", SqlDbType.NVarChar, record.Facts is { Count: > 0 } ? JsonSerializer.Serialize(record.Facts, Json) : null);
                Add(command, "@changes", SqlDbType.NVarChar, changes is { Count: > 0 } ? JsonSerializer.Serialize(changes, Json) : null);
                Add(command, "@ip", SqlDbType.VarChar, Cut(actor.Origin.IpAddress, AuditSql.IpAddressMaxLength));
                Add(command, "@userAgent", SqlDbType.NVarChar, Cut(actor.Origin.UserAgent, AuditSql.UserAgentMaxLength));
                Add(command, "@trace", SqlDbType.Char, actor.Origin.TraceId);
                await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The checks of FR-008, FR-010 and FR-011. A failure is a defect in the caller.</summary>
    private static AuditAction Check(AuditRecord record, IAuditCatalogue catalogue)
    {
        var action = catalogue.Find(record.Action)
                     ?? throw new AuditRefusedException($"'{record.Action}' is not an action of the audit catalogue.");

        foreach (var (key, value) in record.Facts ?? new Dictionary<string, object?>())
        {
            if (!action.Facts.Contains(key, StringComparer.Ordinal))
            {
                throw new AuditRefusedException($"The action '{action.Code}' does not allow the fact '{key}'.");
            }

            var allowed = value is null or bool or int or long or decimal
                          || value is string { Length: <= AuditSql.FactTextMaxLength }
                          || value is IEnumerable<string> list && list.All(item => item.Length <= AuditSql.FactTextMaxLength);
            if (!allowed)
            {
                throw new AuditRefusedException($"The fact '{key}' of '{action.Code}' is not a short text, a number, a yes/no or a list of codes.");
            }
        }

        if (action.NeedsBeforeAfter && record.Changes is not { Count: > 0 })
        {
            throw new AuditRefusedException($"The action '{action.Code}' needs the value before and after.");
        }

        return action;
    }

    /// <summary>The signed-in person of this request, or of this piece of work; otherwise the platform itself.</summary>
    private static async Task<AuditActor> AmbientAsync(IServiceProvider? services, CancellationToken cancellation)
    {
        var http = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var scope = services?.GetService<AuditActorScope>();

        var origin = http is null
            ? scope?.Origin ?? AuditOrigin.None
            : new AuditOrigin(
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null,
                TraceIdMiddleware.Get(http));

        // On a request the actor is whoever is signed in, or nobody. What a piece of work says
        // about itself counts only where there is no request.
        Guid? accountId = http is not null
            ? Guid.TryParse(http.User.FindFirstValue(StaffClaims.AccountId), out var signedIn) ? signedIn : null
            : scope?.AccountId;

        if (accountId is not { } actor)
        {
            return new AuditActor(AuditSql.ActorSystem, null, null, origin);
        }

        var name = await NameOfAsync(services, actor, cancellation).ConfigureAwait(false);
        return new AuditActor(AuditSql.ActorStaff, actor, name, origin);
    }

    private static async Task<string?> NameOfAsync(IServiceProvider? services, Guid? accountId, CancellationToken cancellation) =>
        accountId is { } id && services?.GetService<IAuditActorNames>() is { } names
            ? await names.NameOfAsync(id, cancellation).ConfigureAwait(false)
            : null;

    private static string? Cut(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];

    private static void Add(DbCommand command, string name, SqlDbType type, object? value) =>
        command.Parameters.Add(new SqlParameter(name, type) { Value = value ?? DBNull.Value });
}
