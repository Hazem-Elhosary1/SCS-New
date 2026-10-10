using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Api;

internal sealed record ForgotPasswordRequest(string? Email)
{
    public override string ToString() => nameof(ForgotPasswordRequest);
}

internal sealed record LinkRequest(string? Token)
{
    public override string ToString() => nameof(LinkRequest);
}

internal sealed record SetWithLinkRequest(string? Token, string? Password)
{
    public override string ToString() => nameof(SetWithLinkRequest);
}

internal sealed record ChangePasswordRequest(string? Password)
{
    public override string ToString() => nameof(ChangePasswordRequest);
}

/// <param name="Email">The account the link belongs to, shown to the link's holder. Personal data.</param>
/// <param name="ClinicNameAr">Only for a new account: its holder is already identified by the link.</param>
internal sealed record LinkResponse(
    string Kind, [property: PersonalData] string Email, string? ClinicNameAr, string? ClinicNameEn)
{
    public override string ToString() => nameof(LinkResponse);
}

internal sealed record LinkUsedResponse(string Kind);

internal sealed record DevInvitationRequest(string? Email)
{
    public override string ToString() => nameof(DevInvitationRequest);
}

/// <summary>
/// Passwords and account activation (spec 004, contracts/password-api.openapi.yaml). The first
/// three addresses need no session and never use one: a browser signed in as someone else can
/// open a link without that session being touched. They share the sign-in request limit.
/// </summary>
internal static class PasswordEndpoints
{
    public static IEndpointRouteBuilder MapPasswords(this IEndpointRouteBuilder v1)
    {
        var password = v1.MapGroup("/auth/password");

        password.MapPost("/forgot", ForgotAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(AuthEndpoints.SignInLimit)
            .WithName("ForgotPassword");

        password.MapPost("/link", InspectLinkAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(AuthEndpoints.SignInLimit)
            .WithName("InspectPasswordLink");

        password.MapPost("/link/set", SetWithLinkAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(AuthEndpoints.SignInLimit)
            .WithName("SetPasswordWithLink");

        password.MapPost("/change", ChangeAsync)
            .AllowsTemporaryPassword()
            .WithName("ChangeTemporaryPassword");

        if (v1.ServiceProvider.GetRequiredService<IOptions<ScsOptions>>().Value.IsLocal)
        {
            // A developer machine only, like the job dashboard: there is no screen that sends an
            // invitation until specs 015 and 026, and activation has to be tried somehow.
            v1.MapPost("/dev/staff-invitations", DevInviteAsync)
                .AllowAnonymous().HandlesOwnSession()
                .WithName("DevSendInvitation");
        }

        return v1;
    }

    private static async Task<IResult> ForgotAsync(
        ForgotPasswordRequest? request, HttpContext context, ForgotPasswordService forgot, CancellationToken cancellation)
    {
        // The shape of an address is public knowledge; refusing a malformed one reveals nothing.
        var (_, emailError) = EmailAddress.TryCreate(request?.Email);
        if (emailError is not null)
        {
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["email"] = [emailError] });
        }

        var started = Stopwatch.GetTimestamp();
        await forgot.RequestAsync(request!.Email, AuthEndpoints.Origin(context), cancellation);

        // One answer, at one moment after asking, whatever the address and whatever was done
        // about it (FR-006). The mail itself is sent in the background.
        var remaining = SignInService.FailureFloor - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellation);
        }

        return TypedResults.Accepted((string?)null);
    }

    private static async Task InspectLinkAsync(
        LinkRequest? request, HttpContext context, PasswordLinkTokens tokens, PasswordLinks links,
        IClinicContext clinic, IClinicProfile profile, CancellationToken cancellation)
    {
        var token = tokens.Read(request?.Token);
        if (token is null)
        {
            await LinkInvalidAsync(context, kind: null);
            return;
        }

        // The token names its clinic and cannot be forged: from here on, ordinary isolation.
        clinic.Establish(token.ClinicId, ClinicSource.Account);

        var view = await links.InspectAsync(token, cancellation);
        if (view is null)
        {
            await LinkInvalidAsync(context, token.Kind);
            return;
        }

        string? nameAr = null;
        string? nameEn = null;
        if (view.Kind == PasswordLinkKinds.NewAccount)
        {
            var own = await profile.GetOwnAsync(cancellation);
            (nameAr, nameEn) = (own.NameAr, own.NameEn);
        }

        await context.Response.WriteAsJsonAsync(new LinkResponse(view.Kind, view.Email, nameAr, nameEn), cancellation);
    }

    private static async Task SetWithLinkAsync(
        SetWithLinkRequest? request, HttpContext context, PasswordLinkTokens tokens, PasswordLinks links,
        PasswordSetter setter, IdentityDbContext db, IClinicContext clinic, IClinicJobScheduler jobs,
        TimeProvider clock, CancellationToken cancellation)
    {
        var token = tokens.Read(request?.Token);
        if (token is null)
        {
            await LinkInvalidAsync(context, kind: null);
            return;
        }

        var clinicId = token.ClinicId;
        clinic.Establish(clinicId, ClinicSource.Account);

        // The link first, then the rules, then "differs from the current one": a refused
        // password leaves the link as it was.
        var view = await links.InspectAsync(token, cancellation);
        if (view is null)
        {
            await LinkInvalidAsync(context, token.Kind);
            return;
        }

        var isReset = view.Kind == PasswordLinkKinds.Reset;
        var result = await setter.SetAsync(
            new SetPassword(
                view.AccountId,
                request?.Password,
                isReset ? SignInEventKinds.ResetCompleted : SignInEventKinds.InvitationAccepted,
                AuthEndpoints.Origin(context))
            {
                Link = token,
                MustDiffer = isReset,
            },
            cancellation);

        if (result != SetPasswordResult.Done)
        {
            await LinkInvalidAsync(context, token.Kind);
            return;
        }

        if (isReset)
        {
            // The owner is told, by a message nobody can switch off (FR-017). If it cannot be
            // sent the reset still stands.
            db.EmailRequests.Add(AccountEmailRequest.For(view.AccountId, AccountEmailKinds.PasswordChanged, clock.GetUtcNow()));
            await db.SaveChangesAsync(cancellation);
            jobs.TryEnqueueForClinic<AccountEmailJob>(clinicId);
        }

        // No session is opened and no cookie is set: the person signs in with both steps.
        await context.Response.WriteAsJsonAsync(new LinkUsedResponse(view.Kind), cancellation);
    }

    private static async Task<IResult> ChangeAsync(
        ChangePasswordRequest? request, HttpContext context, ICurrentStaff staff, IdentityDbContext db,
        PasswordSetter setter, CancellationToken cancellation)
    {
        var marked = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == staff.AccountId)
            .Select(a => (bool?)a.MustChangePassword)
            .SingleOrDefaultAsync(cancellation);

        if (marked is null)
        {
            throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded);
        }

        if (marked == false)
        {
            // Changing an ordinary password belongs with the security settings (spec 085).
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.PasswordChangeNotRequired);
        }

        var result = await setter.SetAsync(
            new SetPassword(
                staff.AccountId, request?.Password, SignInEventKinds.TemporaryPasswordChanged, AuthEndpoints.Origin(context))
            {
                MustDiffer = true,
                KeepSessionId = staff.SessionId,
            },
            cancellation);

        return result == SetPasswordResult.Done
            ? TypedResults.NoContent()
            : throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded);
    }

    /// <summary>Local only. Sends the invitation of an account that was never activated; always 204.</summary>
    private static async Task<IResult> DevInviteAsync(
        DevInvitationRequest? request, AccountLookup lookup, IClinicContext clinic, IdentityDbContext db,
        IAccountHandover handover, CancellationToken cancellation)
    {
        var found = await lookup.FindAsync(EmailAddress.Normalize(request?.Email), cancellation);
        if (found is not null)
        {
            clinic.Establish(found.ClinicId, ClinicSource.Account);
            var invited = await db.Accounts.AsNoTracking()
                .AnyAsync(a => a.Id == found.AccountId && a.State == AccountStates.Invited, cancellation);

            if (invited)
            {
                await handover.ResendInvitationAsync(found.AccountId, cancellation);
            }
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// One answer for every link that cannot be used. It says which kind of link it was, so the
    /// right screen is shown, and never why it stopped working; a token that cannot be read is
    /// answered as a reset link (FR-023, FR-024).
    /// </summary>
    private static Task LinkInvalidAsync(HttpContext context, string? kind) =>
        ProblemWriter.WriteAsync(
            context,
            StatusCodes.Status410Gone,
            ErrorCodes.LinkInvalid,
            errors: null,
            new Dictionary<string, object?> { ["kind"] = kind ?? PasswordLinkKinds.Reset });
}
