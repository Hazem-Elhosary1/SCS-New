using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.Audit.Domain;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Tenancy;

namespace Scs.Audit.Infrastructure.Persistence;

/// <summary>
/// The audit trail's schema (spec 007). One table, clinic-owned and append-only. This context
/// reads it; entries are written by the shared writer on the connection of whichever module is
/// saving the change, so that the change and its entry are one transaction.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string HistoryTable = "__EFMigrationsHistory";

    public DbSet<Entry> Entries => Set<Entry>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, AuditSql.Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditSql.Schema);

        modelBuilder.Entity<Entry>(builder =>
        {
            builder.ToTable(AuditSql.Table, table =>
            {
                table.HasTrigger(AuditSql.AppendOnlyTrigger);
                table.HasCheckConstraint("CK_Entry_ActorKind", "[ActorKind] IN ('staff', 'operator', 'system')");
                table.HasCheckConstraint("CK_Entry_Category", "[Category] IN ('security', 'changes', 'records', 'users')");
                table.HasCheckConstraint("CK_Entry_Facts", "[Facts] IS NULL OR ISJSON([Facts]) = 1");
                table.HasCheckConstraint("CK_Entry_Changes", "[Changes] IS NULL OR ISJSON([Changes]) = 1");
            });

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(e => e.ActorKind).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(e => e.ActorName).HasMaxLength(AuditSql.ActorNameMaxLength);
            builder.Property(e => e.ActionCode).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.Property(e => e.Category).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(e => e.TargetKind).HasMaxLength(32).IsUnicode(false);
            builder.Property(e => e.TargetName).HasMaxLength(AuditSql.TargetNameMaxLength);
            builder.Property(e => e.TargetReadyKey).HasMaxLength(16).IsUnicode(false);
            builder.Property(e => e.IpAddress).HasMaxLength(AuditSql.IpAddressMaxLength).IsUnicode(false);
            builder.Property(e => e.UserAgent).HasMaxLength(AuditSql.UserAgentMaxLength);
            builder.Property(e => e.TraceId).HasColumnType("char(32)").IsRequired();

            // A clinic's trail by time, newest first, with what the facets narrow by.
            builder.HasIndex(e => new { e.ClinicId, e.OccurredAtUtc, e.Id })
                .IsDescending(false, true, true)
                .IncludeProperties(e => new { e.Category, e.ActionCode, e.ActorAccountId, e.ActorKind });

            // The history of one thing (used from spec 026 on).
            builder.HasIndex(e => new { e.ClinicId, e.TargetKind, e.TargetId, e.OccurredAtUtc });
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static AuditDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<AuditDbContext>();
        AuditDbContext.Configure(builder, connection);
        return new AuditDbContext(builder.Options, new ClinicContext());
    }
}
