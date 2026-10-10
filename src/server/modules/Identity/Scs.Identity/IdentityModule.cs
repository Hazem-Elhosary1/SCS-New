using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Api;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity;

/// <summary>
/// Who is asking: staff accounts, signing in with a password and an email code, and sessions
/// that can be ended at once (spec 003). Migrated after Tenancy, whose isolation function its
/// tables use, and before every module whose endpoints need a signed-in person.
/// </summary>
public sealed class IdentityModule : IModule, IMigratableModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>((provider, builder) => IdentityDbContext.Configure(
            builder, provider.GetRequiredService<IOptions<ScsOptions>>().Value.Db.Connection));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScsMail();
        services.AddScsRequestGuards();

        services.TryAddSingleton<PasswordLinkTokens>();
        services.AddScoped<AccountLookup>();
        services.AddScoped<SessionService>();
        services.AddScoped<SignInService>();
        services.AddScoped<PasswordLinks>();
        services.AddScoped<PasswordSetter>();
        services.AddScoped<ForgotPasswordService>();
        services.AddScoped<IAccountHandover, AccountHandover>();
        services.AddScoped<IStaffSessions>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<IStaffSessionCheck>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<IStaffAccounts, StaffAccounts>();
        services.AddScoped<ICurrentStaff, CurrentStaff>();
        services.AddAuthRateLimits();
        services.AddSingleton<IRecurringJobRegistration, IdentityJobs>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapAuth();
        v1.MapPasswords();
    }

    public IReadOnlyList<string> GetMigrations(string migrationConnection)
    {
        using var db = IdentityDbContextFactory.Create(migrationConnection);
        return [.. db.Database.GetMigrations()];
    }

    public async Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation)
    {
        var db = IdentityDbContextFactory.Create(migrationConnection);
        await using (db.ConfigureAwait(false))
        {
            await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>The seeded account that holds a temporary password, for the screen of spec 004.</summary>
    public const string SeedTemporaryAccount = "temp@noor.example";

    /// <summary>
    /// Adds the made-up staff accounts every local and test environment starts with, if they are
    /// not there: three for each of the two seeded clinics, and one holding a temporary password. Never run against production
    /// (ops/runbooks/data-handling.md). Returns the emails it added.
    /// </summary>
    /// <param name="appConnection">The application login: accounts are created under isolation, clinic by clinic.</param>
    /// <param name="clinics">Short name and id of each seeded clinic.</param>
    /// <param name="password">The one password of the accounts that have one.</param>
    public static async Task<IReadOnlyList<string>> SeedAsync(
        string appConnection, IReadOnlyDictionary<string, Guid> clinics, string password, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(clinics);

        (string Clinic, string Email, string State)[] accounts =
        [
            ("noor", "owner@noor.example", AccountStates.Active),
            ("noor", "reception@noor.example", AccountStates.Active),
            ("noor", "former@noor.example", AccountStates.Disabled),
            ("noor", SeedTemporaryAccount, AccountStates.Active),
            ("shifa", "owner@shifa.example", AccountStates.Active),
            ("shifa", "doctor@shifa.example", AccountStates.Active),
            ("shifa", "new@shifa.example", AccountStates.Invited),
        ];

        var problems = PasswordRules.Check(password);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"The seed password does not meet the password rules: {string.Join(", ", problems)}.", nameof(password));
        }

        var added = new List<string>();
        var hash = PasswordHasher.Hash(password);

        foreach (var perClinic in accounts.GroupBy(a => a.Clinic))
        {
            if (!clinics.TryGetValue(perClinic.Key, out var clinicId))
            {
                continue;
            }

            var clinic = new ClinicContext();
            clinic.Establish(clinicId, ClinicSource.Job);

            var builder = new DbContextOptionsBuilder<IdentityDbContext>();
            IdentityDbContext.Configure(builder, appConnection);
            var db = new IdentityDbContext(builder.Options, clinic);
            await using (db.ConfigureAwait(false))
            {
                foreach (var account in perClinic)
                {
                    if (await db.Accounts.AnyAsync(a => a.Email == account.Email, cancellation).ConfigureAwait(false))
                    {
                        continue;
                    }

                    var (email, _) = EmailAddress.TryCreate(account.Email);
                    db.Accounts.Add(StaffAccount.Create(
                        email!, account.State, account.State == AccountStates.Invited ? null : hash, TimeProvider.System.GetUtcNow()));
                    added.Add(account.Email);
                }

                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

                // The temporary password is marked when the account is first seeded, and given a
                // fresh three days each time the seed runs while nobody has replaced it — so the
                // screen can always be tried, and a developer who did replace it is left alone.
                var temporaryUntil = TimeProvider.System.GetUtcNow().AddHours(72);
                var justAdded = added.Contains(SeedTemporaryAccount);
                await db.Accounts
                    .Where(a => a.Email == SeedTemporaryAccount && (justAdded || a.MustChangePassword))
                    .ExecuteUpdateAsync(
                        update => update
                            .SetProperty(a => a.MustChangePassword, true)
                            .SetProperty(a => a.TemporaryPasswordExpiresAtUtc, temporaryUntil),
                        cancellation)
                    .ConfigureAwait(false);
            }
        }

        return added;
    }
}
