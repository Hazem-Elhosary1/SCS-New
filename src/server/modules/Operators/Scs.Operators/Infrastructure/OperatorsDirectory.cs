using Microsoft.EntityFrameworkCore;
using Scs.Audit.Contracts;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Wording;

namespace Scs.Operators.Infrastructure;

/// <summary>One row of the team's list. The name and the email are personal data.</summary>
internal sealed record OperatorRow(
    Guid Id,
    [property: PersonalData] string FullName,
    [property: PersonalData] string Email,
    RoleRef Role,
    DateTimeOffset? LastSignInAtUtc,
    string State,
    bool TwoStep)
{
    public override string ToString() => $"{nameof(OperatorRow)}#{Id}";
}

/// <summary>The four cards over the list: counted over the whole team, whatever is searched for.</summary>
internal sealed record OperatorCards(int Total, int Active, int ActivatedThisMonth, int Invited, int TwoStepOn);

/// <summary>What the filter panel may offer: only the values some operator has.</summary>
internal sealed record OperatorFilters(IReadOnlyList<string> States, IReadOnlyList<RoleRef> Roles);

internal sealed record OperatorsPage(
    IReadOnlyList<OperatorRow> Items, int Total, int Page, int PageSize, OperatorCards Cards, OperatorFilters Filters);

internal sealed record InvitedByView(Guid Id, [property: PersonalData] string Name)
{
    public override string ToString() => $"{nameof(InvitedByView)}#{Id}";
}

/// <summary>One number of the operator's page; later specs add theirs to the list.</summary>
internal sealed record PerformanceCard(string Key, int Value);

/// <summary>
/// One line of an operator's recent activity. The console writes the sentence: this carries
/// its kind and the names it needs, never a link, an address or an email.
/// </summary>
/// <param name="Kind"><c>signed-in</c>, or the code of a platform action.</param>
/// <param name="Name">Whom or what it was done to, as it was called then.</param>
/// <param name="Role">A custom role's name, where the sentence names a role.</param>
/// <param name="RoleKey">The same for one of the platform's own roles: its key.</param>
/// <param name="Device">For a sign-in: the browser and system in words.</param>
internal sealed record ActivityLine(
    string Kind, [property: PersonalData] string? Name, string? Role, string? RoleKey, string? Device, DateTimeOffset AtUtc)
{
    public override string ToString() => nameof(ActivityLine);
}

/// <summary>What the caller may do to the account they are looking at.</summary>
internal sealed record OperatorActions(bool Edit, bool Suspend, bool Reactivate, bool SendLink, bool Unlock)
{
    public static OperatorActions None { get; } = new(false, false, false, false, false);
}

/// <param name="FirstOperator">Made by the deployment tool before anyone else existed: «النظام (أول مشغل)».</param>
internal sealed record OperatorDetail(
    Guid Id,
    [property: PersonalData] string FullName,
    [property: PersonalData] string Email,
    RoleRef Role,
    DateTimeOffset? LastSignInAtUtc,
    string State,
    bool TwoStep,
    [property: PersonalData] string? Mobile,
    string? Department,
    string? JobTitle,
    string Language,
    DateTimeOffset CreatedAtUtc,
    InvitedByView? InvitedBy,
    bool FirstOperator,
    bool Locked,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PerformanceCard> Performance,
    IReadOnlyList<ActivityLine> Activity,
    string Version,
    OperatorActions Can)
{
    public override string ToString() => $"{nameof(OperatorDetail)}#{Id}";
}

/// <summary>
/// Reading the team (spec 011, User Story 3; FR-022 to FR-026): the list with its cards and
/// filters in one answer, so they never disagree, and one operator's page. Nothing here returns
/// a password, a link, a token, or how an account came to be locked.
/// </summary>
internal sealed class OperatorsDirectory(
    OperatorsDbContext db, IPlatformAuditReading trail, IOperatorAccess access, TimeProvider clock)
{
    public const int PageSize = 8;
    public const int ActivityLines = 10;
    public const int SearchMaxLength = 100;

    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public async Task<OperatorsPage> ListAsync(
        string? search, string? state, Guid? roleId, int page, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var all = db.Operators.AsNoTracking();

        // The cards and the filter's values are about the whole team.
        var byState = await all.GroupBy(o => o.State).Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellation).ConfigureAwait(false);
        var monthStart = StartOfMonthInCairo(now);
        var activatedThisMonth = await all.CountAsync(o => o.ActivatedAtUtc >= monthStart, cancellation).ConfigureAwait(false);
        var twoStepOn = await all.CountAsync(o => o.TwoStepRequired, cancellation).ConfigureAwait(false);

        var heldRoles = await (
                from role in db.Roles.AsNoTracking()
                where all.Any(o => o.RoleId == role.Id)
                orderby role.SystemKey == null, role.CreatedAtUtc
                select new RoleRef(role.Id, role.Name, role.SystemKey))
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        int Count(string of) => byState.Where(s => s.State == of).Sum(s => s.Count);
        var cards = new OperatorCards(
            byState.Sum(s => s.Count), Count(OperatorStates.Active), activatedThisMonth, Count(OperatorStates.Invited), twoStepOn);
        string[] order = [OperatorStates.Active, OperatorStates.Invited, OperatorStates.Suspended];
        var filters = new OperatorFilters([.. order.Where(s => Count(s) > 0)], heldRoles);

        var matching = all;
        var words = (search ?? "").Trim();
        if (words.Length > SearchMaxLength)
        {
            words = words[..SearchMaxLength];
        }

        if (words.Length > 0)
        {
            // "Contains", whatever the case: the columns' collation compares without it, and the
            // wildcards a person may type are taken as letters.
            var pattern = $"%{words.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
            matching = matching.Where(o => EF.Functions.Like(o.FullName, pattern) || EF.Functions.Like(o.Email, pattern));
        }

        if (state is OperatorStates.Active or OperatorStates.Invited or OperatorStates.Suspended)
        {
            matching = matching.Where(o => o.State == state);
        }

        if (roleId is { } held)
        {
            matching = matching.Where(o => o.RoleId == held);
        }

        var total = await matching.CountAsync(cancellation).ConfigureAwait(false);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var shown = Math.Clamp(page, 1, pages);

        var rows = await (
                from account in matching
                join role in db.Roles.AsNoTracking() on account.RoleId equals role.Id
                orderby account.CreatedAtUtc descending, account.Id descending
                select new OperatorRow(
                    account.Id, account.FullName, account.Email, new RoleRef(role.Id, role.Name, role.SystemKey),
                    account.LastSignInAtUtc, account.State, account.TwoStepRequired))
            .Skip((shown - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return new OperatorsPage(rows, total, shown, PageSize, cards, filters);
    }

    /// <summary>One operator's page, for whoever is looking. Null when there is no such account.</summary>
    public async Task<OperatorDetail?> ReadAsync(Guid id, Guid callerId, string language, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var found = await (
                from o in db.Operators.AsNoTracking()
                join role in db.Roles.AsNoTracking() on o.RoleId equals role.Id
                where o.Id == id
                select new { Account = o, Role = new RoleRef(role.Id, role.Name, role.SystemKey) })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (found is null)
        {
            return null;
        }

        var account = found.Account;
        var codes = await db.RolePermissions.AsNoTracking()
            .Where(p => p.RoleId == account.RoleId)
            .Select(p => p.Code)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var activeSessions = await db.Sessions.AsNoTracking()
            .CountAsync(s => s.OperatorId == id && s.EndedAtUtc == null && s.ExpiresAtUtc > now, cancellation)
            .ConfigureAwait(false);

        // The first operator: made by the deployment tool, and nobody was made before them.
        var first = account.InvitedById is null
                    && !await db.Operators.AsNoTracking()
                        .AnyAsync(o => o.InvitedById == null && o.CreatedAtUtc < account.CreatedAtUtc, cancellation)
                        .ConfigureAwait(false);

        var locked = account.IsLocked(now);
        var manages = await access.HasAsync(OperatorPermissions.OperatorsManage, cancellation).ConfigureAwait(false);
        var can = !manages
            ? OperatorActions.None
            : new OperatorActions(
                Edit: true,
                Suspend: account.State == OperatorStates.Active && id != callerId,
                Reactivate: account.State == OperatorStates.Suspended,
                SendLink: account.State is OperatorStates.Active or OperatorStates.Invited,
                Unlock: account.State == OperatorStates.Active && locked);

        return new OperatorDetail(
            account.Id, account.FullName, account.Email, found.Role, account.LastSignInAtUtc, account.State,
            account.TwoStepRequired, account.Mobile, account.Department, account.JobTitle, account.Language,
            account.CreatedAtUtc,
            account.InvitedById is { } inviter ? new InvitedByView(inviter, account.InvitedByName ?? "") : null,
            first,
            locked,
            OperatorPermissions.InOrder(codes.Where(OperatorPermissions.IsKnown)),
            [new PerformanceCard("activeSessions", activeSessions)],
            await ActivityAsync(id, language, cancellation).ConfigureAwait(false),
            Convert.ToBase64String(account.RowVersion),
            can);
    }

    /// <summary>
    /// The newest ten of two things, by time: the account's successful sign-ins, and what it
    /// did to the platform (research R12).
    /// </summary>
    private async Task<IReadOnlyList<ActivityLine>> ActivityAsync(Guid id, string language, CancellationToken cancellation)
    {
        var signIns = await db.Records.AsNoTracking()
            .Where(r => r.OperatorId == id && r.Kind == SignInRecordKinds.LoginSucceeded)
            .OrderByDescending(r => r.OccurredAtUtc)
            .Take(ActivityLines)
            .Select(r => new { r.OccurredAtUtc, r.UserAgent })
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var actions = await trail.LastByActorAsync(id, ActivityLines, cancellation).ConfigureAwait(false);
        var words = language == Words.English ? Words.English : Words.Arabic;

        return
        [
            .. signIns
                .Select(s => new ActivityLine("signed-in", null, null, null, DeviceText.Describe(s.UserAgent, words), s.OccurredAtUtc))
                .Concat(actions.Select(a => new ActivityLine(
                    a.Action,
                    a.TargetName,
                    a.Facts.GetValueOrDefault("role") ?? a.After,
                    a.Facts.GetValueOrDefault("systemRole") ?? a.AfterKey,
                    null,
                    a.OccurredAt)))
                .OrderByDescending(line => line.AtUtc)
                .Take(ActivityLines),
        ];
    }

    private static DateTimeOffset StartOfMonthInCairo(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Cairo);
        var first = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(first, Cairo.GetUtcOffset(first)).ToUniversalTime();
    }
}
