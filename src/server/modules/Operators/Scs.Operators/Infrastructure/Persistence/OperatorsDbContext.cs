using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.Operators.Domain;

namespace Scs.Operators.Infrastructure.Persistence;

/// <summary>
/// The operators' schema (spec 010). No table here is clinic-owned: an operator belongs to the
/// platform, and each table is named in the platform-wide list with its reason. Nothing of a
/// clinic or a patient is kept here.
/// </summary>
public sealed class OperatorsDbContext(DbContextOptions<OperatorsDbContext> options) : DbContext(options)
{
    public const string Schema = "ops";
    public const string HistoryTable = "__EFMigrationsHistory";

    internal DbSet<Operator> Operators => Set<Operator>();

    internal DbSet<SignInChallenge> Challenges => Set<SignInChallenge>();

    internal DbSet<OperatorSession> Sessions => Set<OperatorSession>();

    internal DbSet<PasswordLink> Links => Set<PasswordLink>();

    internal DbSet<KnownBrowser> Browsers => Set<KnownBrowser>();

    internal DbSet<SignInRecord> Records => Set<SignInRecord>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Operator>(builder =>
        {
            builder.ToTable("Operator", table =>
            {
                table.HasCheckConstraint("CK_Operator_State", "[State] IN ('invited', 'active', 'suspended')");
                table.HasCheckConstraint("CK_Operator_Language", "[Language] IN ('ar', 'en')");
                table.HasCheckConstraint(
                    "CK_Operator_Password", "[State] = 'invited' OR [PasswordHash] IS NOT NULL");
            });

            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();
            builder.Property(o => o.FullName).HasMaxLength(Operator.NameMaxLength).IsRequired();
            builder.Property(o => o.Email).HasMaxLength(Operator.EmailMaxLength).IsRequired();
            builder.Property(o => o.Mobile).HasMaxLength(Operator.MobileMaxLength);
            builder.Property(o => o.Language).HasMaxLength(2).IsUnicode(false).IsRequired();
            builder.Property(o => o.State).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(o => o.PasswordHash).HasMaxLength(256).IsUnicode(false);
            builder.Property(o => o.PasswordChangedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(o => o.LastFailedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(o => o.LockedUntilUtc).HasColumnType("datetimeoffset");
            builder.Property(o => o.LastSignInAtUtc).HasColumnType("datetimeoffset");
            builder.Property(o => o.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(o => o.InvitationOwedSinceUtc).HasColumnType("datetimeoffset");
            builder.Property(o => o.RowVersion).IsRowVersion();
            builder.HasIndex(o => o.Email).IsUnique().HasDatabaseName("UX_Operator_Email");
        });

        modelBuilder.Entity<SignInChallenge>(builder =>
        {
            builder.ToTable("SignInChallenge", table => table.HasCheckConstraint(
                "CK_SignInChallenge_Purpose",
                "([Purpose] = 'sign-in' AND [PendingPasswordHash] IS NULL) OR ([Purpose] = 'password-change' AND [PendingPasswordHash] IS NOT NULL AND [SessionId] IS NOT NULL)"));

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.Purpose).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(c => c.PendingPasswordHash).HasMaxLength(256).IsUnicode(false);
            builder.Property(c => c.CodeHash).HasMaxLength(32).IsRequired();
            builder.Property(c => c.CodeSentAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.ExpiresAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.ResendAvailableAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.Address).HasMaxLength(SignInChallenge.AddressMaxLength).IsUnicode(false);
            builder.Property(c => c.ConsumedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(c => c.ReplacedAtUtc).HasColumnType("datetimeoffset");
            builder.HasIndex(c => new { c.OperatorId, c.Purpose })
                .HasFilter("[ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL")
                .HasDatabaseName("IX_SignInChallenge_Standing");
            builder.HasIndex(c => c.ExpiresAtUtc).HasDatabaseName("IX_SignInChallenge_Expires");
        });

        modelBuilder.Entity<OperatorSession>(builder =>
        {
            builder.ToTable("Session", table => table.HasCheckConstraint(
                "CK_Session_Ended",
                "([EndedAtUtc] IS NULL AND [EndedFor] IS NULL) OR ([EndedAtUtc] IS NOT NULL AND [EndedFor] IN ('signed-out', 'replaced', 'idle', 'expired', 'password-reset', 'suspended', 'signed-out-everywhere', 'reuse', 'role-changed', 'locked'))"));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.StartedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.ExpiresAtUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.LastActivityAtUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.RenewalHash).HasMaxLength(32).IsRequired();
            builder.Property(s => s.PreviousRenewalHash).HasMaxLength(32);
            builder.Property(s => s.PreviousValidUntilUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.Address).HasMaxLength(SignInChallenge.AddressMaxLength).IsUnicode(false);
            builder.Property(s => s.UserAgent).HasMaxLength(OperatorSession.UserAgentMaxLength);
            builder.Property(s => s.EndedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(s => s.EndedFor).HasMaxLength(24).IsUnicode(false);

            // One open session for an operator (FR-030): two sign-ins at one moment cannot both win.
            builder.HasIndex(s => s.OperatorId).IsUnique()
                .HasFilter("[EndedAtUtc] IS NULL")
                .HasDatabaseName("UX_Session_OneOpen");
            builder.HasIndex(s => new { s.OperatorId, s.StartedAtUtc }).HasDatabaseName("IX_Session_Recent");
        });

        modelBuilder.Entity<PasswordLink>(builder =>
        {
            builder.ToTable("PasswordLink", table =>
                table.HasCheckConstraint("CK_PasswordLink_Kind", "[Kind] IN ('reset', 'first')"));

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.Kind).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(l => l.SecretHash).HasMaxLength(32).IsRequired();
            builder.Property(l => l.CreatedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(l => l.ExpiresAtUtc).HasColumnType("datetimeoffset");
            builder.Property(l => l.UsedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(l => l.EndedAtUtc).HasColumnType("datetimeoffset");
            builder.HasIndex(l => new { l.OperatorId, l.Kind, l.CreatedAtUtc }).HasDatabaseName("IX_PasswordLink_Recent");
        });

        modelBuilder.Entity<KnownBrowser>(builder =>
        {
            builder.ToTable("KnownBrowser");
            builder.HasKey(b => new { b.OperatorId, b.MarkHash });
            builder.Property(b => b.MarkHash).HasMaxLength(32);
            builder.Property(b => b.FirstSeenAtUtc).HasColumnType("datetimeoffset");
            builder.Property(b => b.LastSeenAtUtc).HasColumnType("datetimeoffset");
        });

        modelBuilder.Entity<SignInRecord>(builder =>
        {
            builder.ToTable("SignInRecord", table => table.HasTrigger(OperatorsSql.RecordTrigger));
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.OccurredAtUtc).HasColumnType("datetimeoffset");
            builder.Property(r => r.Kind).HasMaxLength(40).IsUnicode(false).IsRequired();
            builder.Property(r => r.Address).HasMaxLength(SignInChallenge.AddressMaxLength).IsUnicode(false);
            builder.Property(r => r.UserAgent).HasMaxLength(OperatorSession.UserAgentMaxLength);
            builder.Property(r => r.Facts).HasMaxLength(SignInRecord.FactsMaxLength);
            builder.Property(r => r.TraceId).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.HasIndex(r => new { r.OperatorId, r.OccurredAtUtc }).HasDatabaseName("IX_SignInRecord_Operator");
            builder.HasIndex(r => new { r.Kind, r.OccurredAtUtc }).HasDatabaseName("IX_SignInRecord_Kind");
        });
    }
}

/// <summary>SQL the migration runs beside the tables.</summary>
internal static class OperatorsSql
{
    public const string RecordTrigger = "TR_SignInRecord_KeptAsWritten";

    /// <summary>How young a record may be and still not be removed, whatever asks.</summary>
    public const int RemovableAfterDays = 30;

    /// <summary>
    /// A record of signing in is kept as it was written (FR-052): no change, and no removal
    /// before it is at least thirty days old — the job that removes records by age (FR-052a)
    /// works far beyond that.
    /// </summary>
    public const string CreateRecordTrigger =
        """
        CREATE TRIGGER [ops].[TR_SignInRecord_KeptAsWritten] ON [ops].[SignInRecord]
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted)
                THROW 51010, 'A sign-in record is not changed.', 1;
            IF EXISTS (SELECT 1 FROM deleted WHERE [OccurredAtUtc] > DATEADD(day, -30, SYSDATETIMEOFFSET()))
                THROW 51011, 'A sign-in record is not removed before it is thirty days old.', 1;
        END
        """;

    public const string DropRecordTrigger = "DROP TRIGGER IF EXISTS [ops].[TR_SignInRecord_KeptAsWritten]";
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class OperatorsDbContextFactory : IDesignTimeDbContextFactory<OperatorsDbContext>
{
    public OperatorsDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static OperatorsDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<OperatorsDbContext>();
        OperatorsDbContext.Configure(builder, connection);
        return new OperatorsDbContext(builder.Options);
    }
}
