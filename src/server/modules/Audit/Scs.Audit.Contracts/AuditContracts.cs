namespace Scs.Audit.Contracts;

/// <summary>What a reader asks of the trail (spec 007, FR-032). Everything but the language is optional.</summary>
/// <param name="From">Start of the period; ninety days before <paramref name="To"/> when not given.</param>
/// <param name="To">End of the period; now when not given.</param>
/// <param name="Search">Part of the actor's name, or of the action's name in the reader's language.</param>
/// <param name="Language"><c>ar</c> or <c>en</c>: the language the entries are read in.</param>
public sealed record AuditQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Category,
    Guid? Actor,
    string? Action,
    string? Search,
    int Page,
    int Size,
    string Language)
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;
    public const int DefaultDays = 90;
    public const int MaxDays = 366;
    public const int SearchMaxLength = 100;
}

/// <summary>Entries in the period that match every facet except the category.</summary>
public sealed record AuditCounts(int All, int Security, int Changes, int Records, int Users);

public sealed record AuditActionView(string Code, string Name);

/// <param name="Known">False for a failed sign-in: nobody is known to have acted.</param>
public sealed record AuditActorView(bool Known, Guid? AccountId, string? Name);

/// <param name="Exists">False when an account that was acted on no longer exists.</param>
public sealed record AuditTargetView(string Kind, Guid? Id, string? Name, bool Exists);

/// <summary>One entry, as words in the reader's language (FR-036).</summary>
/// <param name="Attempts">For a run of failed sign-in attempts: how many there were.</param>
/// <param name="Locked">For a run of failed sign-in attempts: whether it ended in a lock.</param>
public sealed record AuditEntryView(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Category,
    AuditActionView Action,
    string Sentence,
    AuditActorView Actor,
    AuditTargetView? Target,
    string? IpAddress,
    string? Device,
    int? Attempts,
    bool? Locked);

public sealed record AuditPage(
    DateTimeOffset From,
    DateTimeOffset To,
    int Total,
    AuditCounts Counts,
    int Page,
    int Size,
    IReadOnlyList<AuditEntryView> Entries);

/// <summary>
/// A clinic's reading of its own trail (spec 007, FR-030): entries of the established clinic
/// whose actor is one of its people, and the sign-in entries on its accounts. Never an entry of
/// another clinic, and never what an operator or the platform itself did. Refused with
/// <c>VALIDATION_FAILED</c> for a period that ends before it starts or is longer than a year,
/// and for a category or action that does not exist.
/// </summary>
public interface IClinicAuditReading
{
    Task<AuditPage> ReadAsync(AuditQuery query, CancellationToken cancellation);
}

/// <summary>One thing an operator did to the platform itself, as stored (spec 011).</summary>
/// <param name="Facts">The entry's facts as short texts: a role's name, a kind of link.</param>
/// <param name="After">For a change of one value: what it became (a role's name).</param>
/// <param name="AfterKey">The same when it became one of the platform's own roles: its key.</param>
public sealed record PlatformActivity(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string? TargetKind,
    Guid? TargetId,
    string? TargetName,
    IReadOnlyDictionary<string, string> Facts,
    string? After,
    string? AfterKey = null)
{
    public override string ToString() => nameof(PlatformActivity);
}

/// <summary>
/// The platform's own trail, read from the operators' side only (spec 011, FR-026): what one
/// operator did, newest first. No clinic's reading reaches it.
/// </summary>
public interface IPlatformAuditReading
{
    Task<IReadOnlyList<PlatformActivity>> LastByActorAsync(Guid operatorId, int take, CancellationToken cancellation);
}

/// <summary>
/// The operators' reading (FR-031): entries whose actor is an operator, across clinics or for
/// one. It has no caller until the operators' console exists (specs 023 and 025).
/// </summary>
public interface IOperatorAuditReading
{
    Task<AuditPage> ReadAsync(AuditQuery query, Guid? clinicId, CancellationToken cancellation);
}
