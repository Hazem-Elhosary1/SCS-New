using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// Creating accounts and changing what lets them in, for the established clinic. An account of
/// another clinic is invisible here, so every operation answers "not found" for it.
/// </summary>
internal sealed class StaffAccounts(
    IdentityDbContext db, IClinicContext clinic, SessionService sessions, TimeProvider clock) : IStaffAccounts
{
    public async Task<Guid> CreateAsync(string email, string state, string? password, CancellationToken cancellation)
    {
        clinic.Require();

        if (state is not (AccountStates.Invited or AccountStates.Active or AccountStates.Disabled))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (address, emailError) = EmailAddress.TryCreate(email);
        if (emailError is not null)
        {
            errors["email"] = [emailError];
        }

        string? hash = null;
        if (state != AccountStates.Invited)
        {
            var problems = string.IsNullOrEmpty(password) ? ["REQUIRED"] : PasswordRules.Check(password);
            if (problems.Count > 0)
            {
                errors["password"] = [.. problems];
            }
            else
            {
                hash = PasswordHasher.Hash(password!);
            }
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var account = StaffAccount.Create(address!, state, hash, clock.GetUtcNow());
        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (failure.IsUniqueViolation())
        {
            // The same answer whether this clinic or another one holds the email (FR-003).
            db.Entry(account).State = EntityState.Detached;
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["email"] = ["TAKEN"] });
        }

        return account.Id;
    }

    public async Task SetStateAsync(Guid accountId, string state, CancellationToken cancellation)
    {
        if (state is not (AccountStates.Active or AccountStates.Disabled))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        var account = await FindAsync(accountId, cancellation).ConfigureAwait(false);
        if (account.State == state)
        {
            return;
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            account.SetState(state);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            if (state == AccountStates.Disabled)
            {
                // A disabled account's links stop working (spec 004, FR-013).
                var now = clock.GetUtcNow();
                await db.PasswordLinks
                    .Where(l => l.AccountId == accountId && l.UsedAtUtc == null && l.ReplacedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(l => l.ReplacedAtUtc, now), cancellation)
                    .ConfigureAwait(false);

                // Disabling an account ends its sessions (SH-045).
                await sessions.EndForAccountCoreAsync(accountId, SessionEndReasons.Disabled, RequestOrigin.None, cancellation)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task UnlockAsync(Guid accountId, CancellationToken cancellation)
    {
        var account = await FindAsync(accountId, cancellation).ConfigureAwait(false);
        account.ClearLock();
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
    }

    public Task ClearLockAsync(Guid accountId, CancellationToken cancellation) => UnlockAsync(accountId, cancellation);

    private async Task<StaffAccount> FindAsync(Guid accountId, CancellationToken cancellation)
    {
        clinic.Require();
        return await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellation).ConfigureAwait(false)
               ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
    }
}

/// <summary>The signed-in person of this request, read from what the session handler established.</summary>
internal sealed class CurrentStaff(IHttpContextAccessor http) : ICurrentStaff
{
    private ClaimsPrincipal? User => http.HttpContext?.User;

    public bool IsSignedIn => User?.FindFirst(StaffClaims.AccountId) is not null;

    public Guid AccountId => Read(StaffClaims.AccountId);

    public Guid SessionId => Read(StaffClaims.SessionId);

    public Guid ClinicId => Read(StaffClaims.ClinicId);

    private Guid Read(string claim) =>
        Guid.TryParse(User?.FindFirstValue(claim), out var value)
            ? value
            : throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
}
