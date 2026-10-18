using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.ClinicData.Contracts;
using Scs.Identity.Contracts;
using Scs.Operators.Contracts;
using Scs.Plans.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Clinics;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Subscriptions.Infrastructure;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Api;

internal sealed record OpsNewClinicRequest(
    string? NameAr, string? NameEn, string? OwnerName, string? Email, string? Mobile, string? Address, Guid? PlanId,
    string? TaxNumber, string? Specialty);

internal sealed record OpsEditClinicRequest(
    string? Version, string? NameAr, string? NameEn, string? OwnerName, string? Email, string? Mobile, string? Address,
    string? TaxNumber, bool? TaxNumberSent, string? Specialty, string? DefaultLanguage, string? Note);

internal sealed record OpsAccountManagerRequest(string? Version, Guid? OperatorId);

internal sealed record ClinicCreated(Guid Id, string Slug);

internal sealed record FormPlan(Guid Id, string NameAr, string NameEn, decimal MonthlyEgp);

internal sealed record ClinicFormOptions(IReadOnlyList<FormPlan> Plans, IReadOnlyList<string> Specialties);

internal sealed record OwnerView(
    string? Name, string Email, string? Mobile, string State, DateTimeOffset? LastSignInAt, OwnerInvitation? Invitation);

internal sealed record DetailActions(bool Edit);

internal sealed record ClinicDetail(
    Guid Id,
    string? Slug,
    string NameAr,
    string NameEn,
    DateTimeOffset CreatedAt,
    OperatorRef? CreatedBy,
    string Version,
    ClinicStatusView Status,
    NamedRef? Plan,
    string? BillingCycle,
    DateTimeOffset? NextRenewal,
    string? Address,
    string? TaxNumber,
    string? Specialty,
    string DefaultLanguage,
    string SignInAddress,
    OwnerView? Owner,
    OperatorRef? AccountManager,
    IReadOnlyList<OperatorRef> Managers,
    int Doctors,
    int Branches,
    DetailActions Can);

internal sealed record AccountManagerSaved(OperatorRef? AccountManager, string Version);

internal sealed record InvitationSent(DateTimeOffset ExpiresAt);

internal sealed record ClinicSettingsView(string DefaultLanguage, string TimeZone, string StaffAddress, DetailActions Can);

/// <summary>
/// The clinics of the operators console (spec 015; contracts/ops-clinics-api.md). Whoever may view
/// clinics reads them; whoever may edit clinic data adds, edits, assigns the account manager and
/// invites the owner again — each asked of the server on every request (SH-056). A route about one
/// clinic works inside that clinic's own isolation (research R4).
/// </summary>
internal static class OpsClinicsEndpoints
{
    /// <summary>The one staff address every clinic signs in at (constitution; the owner's answer).</summary>
    public const string StaffAddress = "https://app.scs-heta.com";

    public const string TimeZone = "Africa/Cairo";

    public static IEndpointRouteBuilder MapOpsClinics(this IEndpointRouteBuilder v1)
    {
        var ops = v1.MapGroup("/ops");

        ops.MapGet("/clinics", ListAsync).RequiresOperator(OperatorPermissions.ClinicsView).WithName("OpsListClinics");
        ops.MapGet("/clinics/form", FormAsync).RequiresOperator(OperatorPermissions.ClinicsEdit).WithName("OpsClinicForm");
        ops.MapPost("/clinics", AddAsync)
            .RequiresOperator(OperatorPermissions.ClinicsEdit)
            .Records(AuditActions.ClinicCreated, AuditActions.SubscriptionPlanSet)
            .WithName("OpsAddClinic");

        ops.MapGet("/clinics/{id:guid}", ReadAsync).RequiresOperator(OperatorPermissions.ClinicsView).WithName("OpsGetClinic");
        ops.MapPut("/clinics/{id:guid}", EditAsync)
            .RequiresOperator(OperatorPermissions.ClinicsEdit)
            .Records(AuditActions.ClinicUpdated, AuditActions.ClinicOwnerUpdated)
            .WithName("OpsEditClinic");
        ops.MapPut("/clinics/{id:guid}/account-manager", AssignAsync)
            .RequiresOperator(OperatorPermissions.ClinicsEdit)
            .Records(AuditActions.ClinicAccountManagerAssigned)
            .WithName("OpsAssignAccountManager");
        ops.MapPost("/clinics/{id:guid}/invitation", InviteAsync)
            .RequiresOperator(OperatorPermissions.ClinicsEdit)
            .Records(AuditActions.ClinicInvitationResent)
            .WithName("OpsResendOwnerInvitation");
        ops.MapGet("/clinics/{id:guid}/settings", SettingsAsync)
            .RequiresOperator(OperatorPermissions.ClinicsView)
            .WithName("OpsClinicSettings");

        return v1;
    }

    private static async Task<IResult> ListAsync(
        HttpRequest http, ClinicsReading reading, ICurrentOperator current, IOperatorAccess access, CancellationToken cancellation)
    {
        var query = http.Query;
        var can = new ClinicActions(
            await access.HasAsync(OperatorPermissions.ClinicsEdit, cancellation),
            await access.HasAsync(OperatorPermissions.ClinicsEdit, cancellation));
        var page = await reading.ListAsync(
            new ClinicsQuery(
                query["search"],
                [.. query["status"].Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)],
                Guid.TryParse(query["plan"], out var plan) ? plan : null,
                string.IsNullOrWhiteSpace(query["length"]) ? null : query["length"].ToString(),
                int.TryParse(query["endsWithin"], out var within) && within is 7 or 14 or 30 ? within : null,
                query["sort"],
                int.TryParse(query["page"], out var number) ? number : null,
                int.TryParse(query["size"], out var size) ? size : null),
            current.OperatorId.ToString(),
            can,
            cancellation);
        return TypedResults.Ok(page);
    }

    private static async Task<IResult> FormAsync(IPlanBook plans, CancellationToken cancellation)
    {
        var offered = await plans.AssignableAsync(cancellation);
        return TypedResults.Ok(new ClinicFormOptions(
            [.. offered.Select(p => new FormPlan(p.Id, p.NameAr, p.NameEn, p.MonthlyEgp))], Specialties.All));
    }

    private static async Task<IResult> AddAsync(
        OpsNewClinicRequest? request, ClinicOnboarding onboarding, ICurrentOperator current, CancellationToken cancellation)
    {
        var body = request ?? new OpsNewClinicRequest(null, null, null, null, null, null, null, null, null);
        var added = await onboarding.AddAsync(
            new NewClinic(body.NameAr, body.NameEn, body.OwnerName, body.Email, body.Mobile, body.Address, body.PlanId, body.TaxNumber, body.Specialty),
            current.OperatorId.ToString(),
            cancellation);
        return TypedResults.Created($"/v1/ops/clinics/{added.Id}", new ClinicCreated(added.Id, added.Slug));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        IClinicContext clinic,
        IClinicProfile profiles,
        IClinicPlans clinicPlans,
        IPlanBook plans,
        IClinicOwners owners,
        IBranches branches,
        IOperatorDirectory operators,
        IOperatorAccess access,
        TimeProvider clock,
        CancellationToken cancellation)
    {
        var profile = await EnterAsync(id, clinic, profiles, cancellation);
        var link = await clinicPlans.StateAsync(cancellation);
        var plan = link?.PlanId is { } planId ? await plans.FindAsync(planId, cancellation) : null;
        var owner = await owners.FindAsync(cancellation);
        var now = clock.GetUtcNow();
        var status = ClinicStatus.Of(profile.State, link?.TrialEndsAtUtc, now);
        var canEdit = await access.HasAsync(OperatorPermissions.ClinicsEdit, cancellation);

        return TypedResults.Ok(new ClinicDetail(
            profile.Id,
            profile.ShortName,
            profile.NameAr,
            profile.NameEn,
            profile.CreatedAtUtc,
            await OperatorAsync(operators, Guid.TryParse(profile.CreatedBy, out var by) ? by : null, cancellation),
            Convert.ToBase64String(profile.Version ?? []),
            status,
            plan is null ? null : new NamedRef(plan.Id, plan.NameAr, plan.NameEn),
            BillingCycle: null,
            NextRenewal: status.Code == ClinicStatus.Trial ? link?.TrialEndsAtUtc : null,
            profile.Address,
            profile.TaxNumber,
            profile.Specialty,
            profile.DefaultLanguage,
            StaffAddress,
            owner is null ? null : new OwnerView(owner.FullName, owner.Email, owner.Mobile, owner.State, owner.LastSignInAtUtc, owner.Invitation),
            await OperatorAsync(operators, profile.AccountManagerId, cancellation),
            canEdit ? [.. (await operators.ActiveAsync(cancellation)).Select(o => new OperatorRef(o.Id, o.FullName))] : [],
            Doctors: 0,
            await branches.CountAsync(cancellation),
            new DetailActions(canEdit)));
    }

    private static async Task<IResult> EditAsync(
        Guid id,
        OpsEditClinicRequest? request,
        IClinicContext clinic,
        IClinicProfile profiles,
        IClinicRegistry registry,
        IClinicOwners owners,
        ICurrentOperator current,
        CancellationToken cancellation)
    {
        var profile = await EnterAsync(id, clinic, profiles, cancellation);
        var body = request ?? throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["version"] = [ClinicFields.Required] });
        var version = Version(body.Version);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if ((body.Note?.Trim().Length ?? 0) > ClinicFields.NoteMaxLength)
        {
            errors["note"] = [ClinicFields.TooLong];
        }

        var taxSent = body.TaxNumberSent ?? body.TaxNumber is not null;
        var change = new ClinicProfileChange(
            body.NameAr, body.NameEn, body.Address, taxSent, body.TaxNumber, string.IsNullOrWhiteSpace(body.Specialty) ? null : body.Specialty,
            body.DefaultLanguage, body.Note, current.OperatorId.ToString());

        // Both parts are checked before either is written: a stale form or any refused field
        // changes nothing, the owner included, and every refusal comes back at once (FR-021).
        var owner = await owners.FindAsync(cancellation);
        var hasOwner = owner is not null;
        await registry.CheckProfileAsync(id, version, change, errors, cancellation);
        if (owner is not null)
        {
            owners.CheckDetails(body.OwnerName, body.Email, body.Mobile, errors);
            if (!errors.ContainsKey("email")
                && !string.Equals(ClinicFields.CanonicalEmail(body.Email), owner.Email, StringComparison.OrdinalIgnoreCase)
                && !await owners.EmailIsFreeAsync(body.Email!, cancellation))
            {
                errors["email"] = [ClinicFields.Taken];
            }
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        // The owner's part, in Identity's transaction, then the clinic's, in Tenancy's (research R5).
        // A notice to the owner's old address speaks the clinic's language as saved.
        var ownerChanged = hasOwner && await owners.UpdateAsync(
            id, profile.NameAr, body.OwnerName, body.Email, body.Mobile, body.Note, body.DefaultLanguage!, cancellation);
        await registry.UpdateProfileAsync(id, version, change, ownerChanged, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> AssignAsync(
        Guid id,
        OpsAccountManagerRequest? request,
        IClinicContext clinic,
        IClinicProfile profiles,
        IClinicRegistry registry,
        IOperatorDirectory operators,
        ICurrentOperator current,
        CancellationToken cancellation)
    {
        var profile = await EnterAsync(id, clinic, profiles, cancellation);
        var version = Version(request?.Version);

        OperatorRef? after = null;
        if (request?.OperatorId is { } chosen)
        {
            var found = await operators.FindAsync(chosen, cancellation);
            if (found is null || found.State != "active")
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OperatorNotActive, "The chosen operator is not active.");
            }

            after = new OperatorRef(found.Id, found.FullName);
        }

        var before = await OperatorAsync(operators, profile.AccountManagerId, cancellation);
        var saved = await registry.AssignAccountManagerAsync(
            id, version, after?.Id, before?.Name, after?.Name, current.OperatorId.ToString(), cancellation);
        return TypedResults.Ok(new AccountManagerSaved(after, Convert.ToBase64String(saved)));
    }

    private static async Task<IResult> InviteAsync(
        Guid id, IClinicContext clinic, IClinicProfile profiles, IClinicOwners owners, CancellationToken cancellation)
    {
        var profile = await EnterAsync(id, clinic, profiles, cancellation);
        var expiresAt = await owners.ResendInvitationAsync(id, profile.NameAr, cancellation);
        return TypedResults.Ok(new InvitationSent(expiresAt));
    }

    private static async Task<IResult> SettingsAsync(
        Guid id, IClinicContext clinic, IClinicProfile profiles, IOperatorAccess access, CancellationToken cancellation)
    {
        var profile = await EnterAsync(id, clinic, profiles, cancellation);
        return TypedResults.Ok(new ClinicSettingsView(
            profile.DefaultLanguage, TimeZone, StaffAddress, new DetailActions(await access.HasAsync(OperatorPermissions.ClinicsEdit, cancellation))));
    }

    /// <summary>
    /// The request works inside the clinic's own isolation from here on (research R4). A clinic that
    /// is not there, or that nobody may see yet, answers as one that does not exist (FR-019).
    /// </summary>
    private static async Task<ClinicProfile> EnterAsync(Guid id, IClinicContext clinic, IClinicProfile profiles, CancellationToken cancellation)
    {
        clinic.Establish(id, ClinicSource.Operator);
        try
        {
            var profile = await profiles.GetOwnAsync(cancellation);
            if (ClinicStates.Invisible.Contains(profile.State))
            {
                throw NotFound();
            }

            return profile;
        }
        catch (ScsProblemException refused) when (refused.Code == ErrorCodes.ClinicNotAvailable)
        {
            throw NotFound();
        }
    }

    private static async Task<OperatorRef?> OperatorAsync(IOperatorDirectory operators, Guid? id, CancellationToken cancellation) =>
        id is { } operatorId && await operators.FindAsync(operatorId, cancellation) is { } found
            ? new OperatorRef(found.Id, found.FullName)
            : null;

    private static byte[] Version(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["version"] = [ClinicFields.Required] });
        }

        try
        {
            return Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["version"] = [ClinicFields.Invalid] });
        }
    }

    private static ScsProblemException NotFound() =>
        new(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such clinic.");
}
