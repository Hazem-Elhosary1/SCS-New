using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Audit;
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
using Scs.SharedKernel.Access;
using Scs.Tenancy.Contracts;

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
        services.AddScoped<ReadyRoles>();
        services.AddScoped<Preferences>();
        services.AddScoped<StaffAccessSource>();
        services.AddScoped<IStaffAccessSource>(provider => provider.GetRequiredService<StaffAccessSource>());
        services.AddScoped<IClinicRoles, ClinicRoles>();
        services.AddScoped<IClinicProvisioner, RolesProvisioner>();
        services.AddScsAudit();
        services.AddScoped<StaffDirectory>();
        services.AddScoped<IStaffDirectory>(provider => provider.GetRequiredService<StaffDirectory>());
        services.AddScoped<IAuditActorNames>(provider => provider.GetRequiredService<StaffDirectory>());
        services.AddAuthRateLimits();
        services.AddSingleton<IRecurringJobRegistration, IdentityJobs>();
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapAuth();
        v1.MapPasswords();
        v1.MapAccess();
        v1.MapPreferences();
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
    /// The seeded custom role, held by <c>reception@noor.example</c> and
    /// <c>reception@shifa.example</c>. It does not hold «عرض الأدوار والصلاحيات» — all three
    /// ready roles do — so its holders are the ones who see the no-permission state of spec 005
    /// and the short side bar of spec 006.
    /// </summary>
    public const string SeedCustomRole = "فريق العيادة";

    private static readonly string[] SeedCustomRoleCodes =
    [
        Permissions.BookingsView, Permissions.BookingsManage, Permissions.BookingsAttendance,
        Permissions.PatientsView, Permissions.MoneyView, Permissions.MoneyCollect,
    ];

    /// <summary>
    /// Adds the made-up staff accounts every local and test environment starts with, if they are
    /// not there: four for each of the two seeded clinics — one of the Noor clinic's holding a temporary password — each with a role. Never run against production
    /// (ops/runbooks/data-handling.md). Returns the emails it added.
    /// </summary>
    /// <param name="appConnection">The application login: accounts are created under isolation, clinic by clinic.</param>
    /// <param name="clinics">Short name and id of each seeded clinic.</param>
    /// <param name="password">The one password of the accounts that have one.</param>
    public static async Task<IReadOnlyList<string>> SeedAsync(
        string appConnection, IReadOnlyDictionary<string, Guid> clinics, string password, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(clinics);

        // Each account's role is its ready key, or the seeded custom role (spec 005, FR-036).
        (string Clinic, string Email, string State, string Role)[] accounts =
        [
            ("noor", "owner@noor.example", AccountStates.Active, ReadyRoleKeys.Owner),
            ("noor", "reception@noor.example", AccountStates.Active, SeedCustomRole),
            ("noor", "former@noor.example", AccountStates.Disabled, ReadyRoleKeys.Doctor),
            ("noor", SeedTemporaryAccount, AccountStates.Active, ReadyRoleKeys.Manager),
            ("shifa", "owner@shifa.example", AccountStates.Active, ReadyRoleKeys.Owner),
            ("shifa", "doctor@shifa.example", AccountStates.Active, ReadyRoleKeys.Doctor),
            ("shifa", "new@shifa.example", AccountStates.Invited, ReadyRoleKeys.Manager),
            ("shifa", "reception@shifa.example", AccountStates.Active, SeedCustomRole),
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

                await SeedRolesAsync(
                    db, clinic, [.. perClinic.Select(a => (a.Email, a.Role))], cancellation).ConfigureAwait(false);
            }
        }

        return added;
    }

    /// <summary>
    /// Gives the seeded clinic its ready roles, one custom role, and each
    /// seeded account that has no role the one written beside it. An account that already has a
    /// role is left alone, so a developer's own changes are kept.
    /// </summary>
    private static async Task SeedRolesAsync(
        IdentityDbContext db, ClinicContext clinic, (string Email, string Role)[] accounts,
        CancellationToken cancellation)
    {
        await new ReadyRoles(db, clinic, TimeProvider.System).EnsureAsync(cancellation).ConfigureAwait(false);

        if (!await db.Roles.AnyAsync(r => r.Name == SeedCustomRole, cancellation).ConfigureAwait(false))
        {
            var custom = Role.Custom(SeedCustomRole, "مكتب المواعيد والخزنة في وظيفة واحدة", null, TimeProvider.System.GetUtcNow());
            custom.Hold(SeedCustomRoleCodes);
            db.Roles.Add(custom);
            db.Record(RoleEntries.Created(custom, bySystem: true));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        var roles = await db.Roles.AsNoTracking()
            .Select(r => new { r.Id, r.ReadyKey, r.Name })
            .ToListAsync(cancellation).ConfigureAwait(false);

        foreach (var (email, wanted) in accounts)
        {
            var role = roles.FirstOrDefault(r => r.ReadyKey == wanted) ?? roles.FirstOrDefault(r => r.Name == wanted);
            if (role is null)
            {
                // The clinic removed that ready role; the account stays as it is.
                continue;
            }

            await db.Accounts
                .Where(a => a.Email == email && a.RoleId == null)
                .ExecuteUpdateAsync(update => update.SetProperty(a => a.RoleId, role.Id), cancellation)
                .ConfigureAwait(false);
        }
    }
}
