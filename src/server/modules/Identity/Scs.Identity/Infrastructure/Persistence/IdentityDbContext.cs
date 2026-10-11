using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;

namespace Scs.Identity.Infrastructure.Persistence;

/// <summary>
/// Accounts, sign-in codes, sessions, events, password links, waiting emails and roles. All are clinic-owned: with no
/// clinic established they are invisible, so a request that has not yet found its account's
/// clinic can read none of them (spec 003, FR-056).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string Schema = "identity";
    public const string SecuritySchema = "security";
    public const string HistoryTable = "__EFMigrationsHistory";
    public const string EventsAppendOnlyTrigger = "TR_SignInEvent_AppendOnly";

    public DbSet<StaffAccount> Accounts => Set<StaffAccount>();

    public DbSet<SignInChallenge> Challenges => Set<SignInChallenge>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SignInEvent> Events => Set<SignInEvent>();

    public DbSet<PasswordLink> PasswordLinks => Set<PasswordLink>();

    public DbSet<AccountEmailRequest> EmailRequests => Set<AccountEmailRequest>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<StaffPreference> Preferences => Set<StaffPreference>();

    /// <summary>Platform-wide and insert-only for the application.</summary>
    public DbSet<AnonymousSignInAttempt> AnonymousAttempts => Set<AnonymousSignInAttempt>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StaffAccount>(builder =>
        {
            builder.ToTable("StaffAccount", table =>
            {
                table.HasCheckConstraint("CK_StaffAccount_State", "[State] IN ('invited', 'active', 'disabled')");
                table.HasCheckConstraint(
                    "CK_StaffAccount_PasswordUnlessInvited", "[State] = 'invited' OR [PasswordHash] IS NOT NULL");

                // A temporary password belongs to an account that has one, and its expiry to the mark.
                table.HasCheckConstraint(
                    "CK_StaffAccount_TemporaryNotInvited", "[MustChangePassword] = 0 OR [State] <> 'invited'");
                table.HasCheckConstraint(
                    "CK_StaffAccount_ExpiryOnlyIfTemporary",
                    "[MustChangePassword] = 1 OR [TemporaryPasswordExpiresAtUtc] IS NULL");
            });

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Email).HasMaxLength(StaffAccount.EmailMaxLength).IsRequired();
            builder.Property(a => a.PasswordHash).HasMaxLength(StaffAccount.PasswordHashMaxLength);
            builder.Property(a => a.State).HasMaxLength(StaffAccount.StateMaxLength).IsUnicode(false).IsRequired();
            builder.Property(a => a.FailedAttempts).HasDefaultValue((byte)0);
            builder.Property(a => a.SpentCodes).HasDefaultValue((byte)0);
            builder.Property(a => a.LastFailedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(a => a.LockedUntilUtc).HasColumnType("datetimeoffset");
            builder.Property(a => a.MustChangePassword).HasDefaultValue(false);
            builder.Property(a => a.TemporaryPasswordExpiresAtUtc).HasColumnType("datetimeoffset");
            builder.Property(a => a.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(a => a.RowVersion).IsRowVersion();

            // One email, one account, on the whole platform: a unique index is enforced across
            // clinics whatever row isolation says (FR-003).
            builder.HasIndex(a => a.Email).IsUnique().HasDatabaseName("UX_StaffAccount_Email");
            builder.HasIndex(a => a.ClinicId);

            // A role an account holds cannot be removed (spec 005, FR-013).
            builder.HasOne<Role>().WithMany().HasForeignKey(a => a.RoleId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(a => new { a.ClinicId, a.RoleId }).HasDatabaseName("IX_StaffAccount_Role");
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("Role", table =>
            {
                table.HasCheckConstraint("CK_Role_Category", "[Category] IN ('ready', 'custom')");
                table.HasCheckConstraint(
                    "CK_Role_ReadyKey",
                    "([Category] = 'ready' AND [ReadyKey] IN ('owner', 'manager', 'doctor')) OR ([Category] = 'custom' AND [ReadyKey] IS NULL)");

                // Only the owner role holds everything, and it always does.
                table.HasCheckConstraint(
                    "CK_Role_OnlyOwnerHoldsEverything",
                    "([HoldsEverything] = 1 AND [ReadyKey] = 'owner') OR ([HoldsEverything] = 0 AND ([ReadyKey] IS NULL OR [ReadyKey] <> 'owner'))");
                table.HasCheckConstraint("CK_Role_NameNotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
            });

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Name).HasMaxLength(Role.NameMaxLength).IsRequired();
            builder.Property(r => r.Description).HasMaxLength(Role.DescriptionMaxLength);
            builder.Property(r => r.Category).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(r => r.ReadyKey).HasMaxLength(8).IsUnicode(false);
            builder.Property(r => r.IsRenamed).HasDefaultValue(false);
            builder.Property(r => r.HoldsEverything).HasDefaultValue(false);
            builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(r => r.RowVersion).IsRowVersion();

            builder.HasIndex(r => new { r.ClinicId, r.Name }).IsUnique().HasDatabaseName("UX_Role_NameInClinic");
            builder.HasIndex(r => new { r.ClinicId, r.ReadyKey }).IsUnique()
                .HasFilter("[ReadyKey] IS NOT NULL")
                .HasDatabaseName("UX_Role_OneOfEachReadyRole");

            builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.ToTable("RolePermission");
            builder.HasKey(p => new { p.RoleId, p.Code });
            builder.Property(p => p.Code).HasMaxLength(RolePermission.CodeMaxLength).IsUnicode(false).IsRequired();
            builder.HasIndex(p => p.ClinicId);
        });

        modelBuilder.Entity<SignInChallenge>(builder =>
        {
            builder.ToTable("SignInChallenge");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.CodeHash).HasColumnType("binary(32)").IsRequired();
            builder.Property(c => c.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(c => c.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(c => c.ResendAvailableAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(c => c.ConsumedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.ReplacedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.IpAddress).HasMaxLength(Session.IpAddressMaxLength).IsUnicode(false);
            builder.HasOne<StaffAccount>().WithMany().HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Restrict);

            // At most one code that has been neither used nor replaced, per account.
            builder.HasIndex(c => c.AccountId).IsUnique()
                .HasFilter("[ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL")
                .HasDatabaseName("UX_SignInChallenge_OnePendingPerAccount");
            builder.HasIndex(c => new { c.ClinicId, c.CreatedAtUtc });
        });

        modelBuilder.Entity<Session>(builder =>
        {
            builder.ToTable("Session");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.RenewalHash).HasColumnType("binary(32)").IsRequired();
            builder.Property(s => s.PreviousRenewalHash).HasColumnType("binary(32)");
            builder.Property(s => s.PreviousValidUntilUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(s => s.LastUsedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(s => s.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(s => s.EndedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.EndReason).HasMaxLength(24).IsUnicode(false);
            builder.Property(s => s.IpAddress).HasMaxLength(Session.IpAddressMaxLength).IsUnicode(false);
            builder.Property(s => s.UserAgent).HasMaxLength(Session.UserAgentMaxLength);
            builder.HasOne<StaffAccount>().WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.ClinicId, s.AccountId })
                .HasFilter("[EndedAtUtc] IS NULL")
                .HasDatabaseName("IX_Session_AliveByAccount");
        });

        modelBuilder.Entity<SignInEvent>(builder =>
        {
            // Declaring the trigger keeps the provider from using a statement shape the
            // database refuses on a table that has one.
            builder.ToTable("SignInEvent", table => table.HasTrigger(EventsAppendOnlyTrigger));
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Kind).HasMaxLength(24).IsUnicode(false).IsRequired();
            builder.Property(e => e.Category).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(e => e.Detail).HasMaxLength(24).IsUnicode(false);
            builder.Property(e => e.IpAddress).HasMaxLength(Session.IpAddressMaxLength).IsUnicode(false);
            builder.Property(e => e.UserAgent).HasMaxLength(Session.UserAgentMaxLength);
            builder.Property(e => e.TraceId).HasColumnType("char(32)").IsRequired();
            builder.Property(e => e.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.HasIndex(e => new { e.ClinicId, e.OccurredAtUtc });
            builder.HasIndex(e => new { e.AccountId, e.OccurredAtUtc });
        });

        modelBuilder.Entity<PasswordLink>(builder =>
        {
            builder.ToTable(
                "PasswordLink",
                table => table.HasCheckConstraint("CK_PasswordLink_Kind", "[Kind] IN ('reset', 'new-account')"));
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.Kind).HasMaxLength(PasswordLink.KindMaxLength).IsUnicode(false).IsRequired();
            builder.Property(l => l.SecretHash).HasColumnType("binary(32)").IsRequired();
            builder.Property(l => l.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(l => l.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(l => l.UsedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(l => l.ReplacedAtUtc).HasColumnType("datetimeoffset");
            builder.HasOne<StaffAccount>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);

            // At most one link that has been neither used nor replaced, per account.
            builder.HasIndex(l => l.AccountId).IsUnique()
                .HasFilter("[UsedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL")
                .HasDatabaseName("UX_PasswordLink_OneLivePerAccount");
            builder.HasIndex(l => new { l.AccountId, l.Kind, l.CreatedAtUtc });
            builder.HasIndex(l => new { l.ClinicId, l.CreatedAtUtc });
        });

        modelBuilder.Entity<AccountEmailRequest>(builder =>
        {
            builder.ToTable(
                "AccountEmailRequest",
                table => table.HasCheckConstraint(
                    "CK_AccountEmailRequest_Kind", "[Kind] IN ('reset-link', 'password-changed')"));
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.Kind).HasMaxLength(AccountEmailRequest.KindMaxLength).IsUnicode(false).IsRequired();
            builder.Property(r => r.Outcome).HasMaxLength(AccountEmailRequest.OutcomeMaxLength).IsUnicode(false);
            builder.Property(r => r.RequestedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(r => r.HandledAtUtc).HasColumnType("datetimeoffset");
            builder.HasOne<StaffAccount>().WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(r => r.ClinicId)
                .HasFilter("[HandledAtUtc] IS NULL")
                .HasDatabaseName("IX_AccountEmailRequest_Waiting");
        });

        modelBuilder.Entity<StaffPreference>(builder =>
        {
            builder.ToTable("StaffPreference", table =>
            {
                table.HasCheckConstraint("CK_StaffPreference_Language", "[Language] IS NULL OR [Language] IN ('ar', 'en')");

                // The read-only banner cannot be closed, so it is never stored as closed.
                table.HasCheckConstraint(
                    "CK_StaffPreference_Banner",
                    "[DismissedBanner] IS NULL OR [DismissedBanner] IN ('trial', 'expiring', 'grace')");
                table.HasCheckConstraint(
                    "CK_StaffPreference_BannerWithItsDay",
                    "([DismissedBanner] IS NULL AND [DismissedOnDay] IS NULL) OR ([DismissedBanner] IS NOT NULL AND [DismissedOnDay] IS NOT NULL)");
            });

            builder.HasKey(p => p.AccountId);
            builder.Property(p => p.AccountId).ValueGeneratedNever();
            builder.Property(p => p.Language).HasMaxLength(2).IsUnicode(false);
            builder.Property(p => p.DismissedBanner).HasMaxLength(8).IsUnicode(false);
            builder.Property(p => p.DismissedOnDay).HasColumnType("date");
            builder.Property(p => p.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.HasOne<StaffAccount>().WithOne().HasForeignKey<StaffPreference>(p => p.AccountId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(p => p.ClinicId);
        });

        modelBuilder.Entity<AnonymousSignInAttempt>(builder =>
        {
            builder.ToTable("AnonymousSignInAttempt", SecuritySchema);
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).UseIdentityColumn();
            builder.Property(a => a.EmailKey).HasColumnType("binary(32)").IsRequired();
            builder.Property(a => a.IpAddress).HasMaxLength(Session.IpAddressMaxLength).IsUnicode(false);
            builder.Property(a => a.UserAgent).HasMaxLength(Session.UserAgentMaxLength);
            builder.Property(a => a.TraceId).HasColumnType("char(32)").IsRequired();
            builder.Property(a => a.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.HasIndex(a => a.OccurredAtUtc);
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static IdentityDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityDbContext.Configure(builder, connection);
        return new IdentityDbContext(builder.Options, new ClinicContext());
    }
}
