using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Tenancy;
using Scs.Subscriptions.Domain;

namespace Scs.Subscriptions.Infrastructure.Persistence;

/// <summary>
/// The subscriptions' schema (spec 014): which plan each clinic is on, and the meter of its four
/// monthly limits. Both tables are clinic-owned, under the database's isolation from the
/// migration that makes them. Specs 015 to 021 grow it with the subscription's states, invoices,
/// payments and extra units.
/// </summary>
public sealed class SubscriptionsDbContext(DbContextOptions<SubscriptionsDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string Schema = "subscriptions";
    public const string HistoryTable = "__EFMigrationsHistory";
    public const string ClinicPlanTable = "ClinicPlan";
    public const string MeterEntryTable = "MeterEntry";

    internal DbSet<ClinicPlan> ClinicPlans => Set<ClinicPlan>();

    internal DbSet<MeterEntry> Meter => Set<MeterEntry>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ClinicPlan>(builder =>
        {
            builder.ToTable(ClinicPlanTable);
            builder.HasKey(p => p.ClinicId);
            builder.Property(p => p.ClinicId).ValueGeneratedNever();
            builder.Property(p => p.SetAtUtc).HasColumnType("datetimeoffset");
            builder.Property(p => p.Version).IsRowVersion();
        });

        modelBuilder.Entity<MeterEntry>(builder =>
        {
            builder.ToTable(MeterEntryTable, table =>
            {
                table.HasCheckConstraint("CK_MeterEntry_Amount", "[Amount] <> 0");
                table.HasCheckConstraint(
                    "CK_MeterEntry_GivesBack",
                    "([Amount] < 0 AND [GivesBack] IS NOT NULL) OR ([Amount] > 0 AND [GivesBack] IS NULL)");
                table.HasCheckConstraint(
                    "CK_MeterEntry_Key",
                    "[FeatureKey] IN ('message_credits', 'teleconsult_minutes', 'ai_xray_analysis', 'ai_voice_notes')");
            });

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.FeatureKey).HasMaxLength(MeterEntry.KeyMaxLength).IsUnicode(false).IsRequired();
            builder.Property(e => e.Month).HasColumnType("char(7)").IsUnicode(false).IsRequired();
            builder.Property(e => e.AtUtc).HasColumnType("datetimeoffset");
            builder.Property(e => e.Purpose).HasMaxLength(MeterEntry.PurposeMaxLength).IsRequired();
            builder.Property(e => e.Reference).HasMaxLength(MeterEntry.ReferenceMaxLength);
            builder.Property(e => e.By).HasMaxLength(MeterEntry.ByMaxLength).IsRequired();

            // What a clinic used of a limit in a month: one seek, summed.
            builder.HasIndex(e => new { e.ClinicId, e.FeatureKey, e.Month })
                .IncludeProperties(e => e.Amount)
                .HasDatabaseName("IX_MeterEntry_Month");
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class SubscriptionsDbContextFactory : IDesignTimeDbContextFactory<SubscriptionsDbContext>
{
    public SubscriptionsDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static SubscriptionsDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<SubscriptionsDbContext>();
        SubscriptionsDbContext.Configure(builder, connection);
        return new SubscriptionsDbContext(builder.Options, new ClinicContext());
    }
}
