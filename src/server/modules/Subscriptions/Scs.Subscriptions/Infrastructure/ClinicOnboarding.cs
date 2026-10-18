using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Tenancy;
using Scs.ClinicData.Contracts;
using Scs.Identity.Contracts;
using Scs.Plans.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Clinics;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>What an operator sends to add a clinic (spec 015, FR-002).</summary>
internal sealed record NewClinic(
    string? NameAr,
    string? NameEn,
    string? OwnerName,
    string? Email,
    string? Mobile,
    string? Address,
    Guid? PlanId,
    string? TaxNumber,
    string? Specialty);

/// <summary>
/// One add of a clinic, all or nothing as anyone sees it (spec 015, FR-004; research R2):
/// <list type="number">
/// <item>under the platform's one add turn, every check that can refuse runs before anything is written;</item>
/// <item>the clinic is registered in <c>setting-up</c>, and in its context the owner, the main
/// branch and the plan with its trial are written, each by its own module;</item>
/// <item>the clinic is made visible with <c>clinic.created</c>;</item>
/// <item>the owner's invitation is emailed — a failure there undoes nothing.</item>
/// </list>
/// A fault after the checks is undone as far as it can be; nothing of it is ever visible.
/// </summary>
internal sealed partial class ClinicOnboarding(
    IClinicRegistry registry,
    IClinicContext clinic,
    IClinicOwners owners,
    IAccountHandover handover,
    IBranches branches,
    IClinicPlans clinicPlans,
    IPlanBook plans,
    IFeatureCatalogue catalogue,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<ClinicOnboarding> logger)
{
    public async Task<RegisteredClinic> AddAsync(NewClinic request, string operatorId, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        RegisteredClinic added;
        Guid? ownerId = null;
        var turn = await registry.TakeAddTurnAsync(cancellation).ConfigureAwait(false);
        await using (turn.ConfigureAwait(false))
        {
            var (plan, mobile) = await CheckAsync(request, cancellation).ConfigureAwait(false);
            var trialEndsAt = clock.GetUtcNow().AddDays(options.Value.Subscriptions.TrialDays);

            added = await registry.RegisterAsync(
                new ClinicRegistration(request.NameAr, request.NameEn, request.Specialty, request.Address, request.TaxNumber, operatorId),
                cancellation).ConfigureAwait(false);

            Guid? branchId = null;
            var planStarted = false;
            var stage = "register";
            try
            {
                clinic.Establish(added.Id, ClinicSource.Operator);
                var profile = new { NameAr = request.NameAr!.Trim(), NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr!.Trim() : request.NameEn.Trim() };

                // The owner first: the one write another writer can still refuse (an account made with
                // this email meanwhile), before anything of the add is recorded (research R2, R6).
                stage = "owner";
                ownerId = await owners.CreateAsync(request.Email!, request.OwnerName!, mobile, "ar", cancellation).ConfigureAwait(false);

                stage = "branch";
                branchId = await branches.CreateMainAsync(profile.NameAr, profile.NameEn, request.Address!, cancellation).ConfigureAwait(false);

                stage = "plan";
                await clinicPlans.StartAsync(plan.Id, trialEndsAt, cancellation).ConfigureAwait(false);
                planStarted = true;

                stage = "activate";
                await registry.ActivateAsync(
                    added.Id,
                    new ClinicCreation(plan.Code, trialEndsAt, request.OwnerName!.Trim(), ClinicFields.CanonicalEmail(request.Email), mobile, operatorId),
                    cancellation).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                // A request given up part-way (a dropped connection) is undone like any fault: what
                // it wrote would otherwise hold the owner's email for good (research R2).
                await UndoAsync(added.Id, ownerId, branchId, planStarted, operatorId, stage, failure).ConfigureAwait(false);
                throw;
            }

        }

        // The invitation leaves after the clinic is whole and the turn is let go, so a slow mail
        // server holds up no other add (SC-002): a failure is shown on the detail, never undone.
        // Any fault there leaves the clinic made: the detail shows no live invitation and offers
        // «إعادة إرسال الدعوة» (FR-004, US1 scenario 6).
        try
        {
            var sent = await handover.ResendInvitationAsync(ownerId!.Value, cancellation).ConfigureAwait(false);
            if (!sent.EmailSent)
            {
                LogInvitationNotSent(logger, added.Id);
            }
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            LogInvitationFailed(logger, added.Id, fault.GetType().Name);
        }

        return added;
    }

    /// <summary>
    /// Every refusal of the add, before anything is written (FR-003, SC-003): the fields together,
    /// then the email, the plan and its users limit.
    /// </summary>
    private async Task<(PlanEntitlements Plan, string Mobile)> CheckAsync(NewClinic request, CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Text(request.NameAr, "nameAr", ClinicFields.NameMaxLength, required: true, errors);
        Text(request.NameEn, "nameEn", ClinicFields.NameMaxLength, required: false, errors);
        Text(request.OwnerName, "ownerName", ClinicFields.NameMaxLength, required: true, errors);
        Text(request.Address, "address", ClinicFields.AddressMaxLength, required: true, errors);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = [ClinicFields.Required];
        }
        else if (!ClinicFields.IsEmail(request.Email))
        {
            errors["email"] = [ClinicFields.InvalidFormat];
        }

        var mobile = "";
        if (string.IsNullOrWhiteSpace(request.Mobile))
        {
            errors["mobile"] = [ClinicFields.Required];
        }
        else if (!ClinicFields.TryMobile(request.Mobile, out mobile))
        {
            errors["mobile"] = [ClinicFields.MobileCode];
        }

        if (!ClinicFields.TryTaxNumber(request.TaxNumber, out _))
        {
            errors["taxNumber"] = [ClinicFields.TaxNumberCode];
        }

        if (string.IsNullOrWhiteSpace(request.Specialty))
        {
            errors["specialty"] = [ClinicFields.Required];
        }
        else if (!Specialties.IsKnown(request.Specialty))
        {
            errors["specialty"] = [ClinicFields.Invalid];
        }

        if (request.PlanId is null)
        {
            errors["planId"] = [ClinicFields.Required];
        }

        if (!errors.ContainsKey("email")
            && !await owners.EmailIsFreeAsync(request.Email!, cancellation).ConfigureAwait(false))
        {
            errors["email"] = [ClinicFields.Taken];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var offered = await plans.AssignableAsync(cancellation).ConfigureAwait(false);
        var plan = offered.Any(p => p.Id == request.PlanId)
            ? await plans.FindAsync(request.PlanId!.Value, cancellation).ConfigureAwait(false)
            : null;
        if (plan is null)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PlanDisabled, "The plan is not offered to clinics.");
        }

        await EnsureASeatAsync(plan, cancellation).ConfigureAwait(false);
        return (plan, mobile);
    }

    /// <summary>
    /// The owner takes a seat of «المستخدمون» (spec 014): a plan whose users limit is 0 — only
    /// possible through the catalogue's baseline — refuses the add before anything is written.
    /// </summary>
    private async Task EnsureASeatAsync(PlanEntitlements plan, CancellationToken cancellation)
    {
        var feature = await catalogue.FindAsync(FeatureKeys.MaxUsers, cancellation).ConfigureAwait(false);
        if (feature is null || feature.Status != "built")
        {
            return;
        }

        var included = plan.Features.FirstOrDefault(f => f.Key == FeatureKeys.MaxUsers);
        var limit = included is null ? feature.Baseline : included.Limit;
        if (limit is 0)
        {
            throw PlanRefusals.LimitReached(FeatureKeys.MaxUsers, 0, new TitleValue(feature.NameAr, feature.NameEn));
        }
    }

    /// <summary>Removes what a failed add wrote, newest first; whatever cannot be removed stays invisible.</summary>
    private async Task UndoAsync(
        Guid clinicId, Guid? ownerId, Guid? branchId, bool planStarted, string operatorId, string stage, Exception failure)
    {
        LogAddFailed(logger, clinicId, stage, failure.GetType().Name);
        try
        {
            if (planStarted)
            {
                await clinicPlans.UndoStartAsync(CancellationToken.None).ConfigureAwait(false);
            }

            if (branchId is { } branch)
            {
                await branches.UndoMainAsync(branch, CancellationToken.None).ConfigureAwait(false);
            }

            if (ownerId is { } owner)
            {
                await owners.DiscardAsync(owner, CancellationToken.None).ConfigureAwait(false);
            }

            await registry.DiscardAsync(clinicId, operatorId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception undo) when (undo is not OperationCanceledException)
        {
            // The clinic stays in setting-up: in no list, count or job, and its id answers NOT_FOUND.
            LogUndoFailed(logger, clinicId, undo.GetType().Name);
        }
    }

    private static void Text(string? value, string field, int max, bool required, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0 && required)
        {
            errors[field] = [ClinicFields.Required];
        }
        else if (trimmed.Length > max)
        {
            errors[field] = [ClinicFields.TooLong];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The add of clinic {clinicId} failed at {stage} ({failure}); undoing")]
    private static partial void LogAddFailed(ILogger logger, Guid clinicId, string stage, string failure);

    [LoggerMessage(Level = LogLevel.Error, Message = "The undo of clinic {clinicId}'s add failed ({failure}); it stays invisible in setting-up")]
    private static partial void LogUndoFailed(ILogger logger, Guid clinicId, string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Clinic {clinicId} was added and its owner's invitation was not sent")]
    private static partial void LogInvitationNotSent(ILogger logger, Guid clinicId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Clinic {clinicId} was added and its owner's invitation failed ({failure}); it can be sent again from the detail")]
    private static partial void LogInvitationFailed(ILogger logger, Guid clinicId, string failure);
}
