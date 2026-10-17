using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.Plans.Domain;

namespace Scs.Plans.Infrastructure.Persistence;

/// <summary>
/// The plans' schema (spec 012: the feature catalogue; spec 013 adds the plans). No table here is
/// clinic-owned: the catalogue is the product's, named in the platform-wide list with its reason.
/// The rules of a feature are held here a second time, so nothing can write a broken row.
/// </summary>
public sealed class PlansDbContext(DbContextOptions<PlansDbContext> options) : DbContext(options)
{
    public const string Schema = "plans";
    public const string HistoryTable = "__EFMigrationsHistory";

    internal DbSet<Feature> Features => Set<Feature>();

    internal DbSet<Plan> Plans => Set<Plan>();

    internal DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Feature>(builder =>
        {
            builder.ToTable("Feature", table =>
            {
                table.HasCheckConstraint(
                    "CK_Feature_Key",
                    "LEN([Key]) BETWEEN 2 AND 64 AND [Key] NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_BIN2 AND LEFT([Key], 1) LIKE '[a-z]' COLLATE Latin1_General_BIN2");
                table.HasCheckConstraint(
                    "CK_Feature_Group", "[Group] IN ('core', 'channels', 'reports', 'money', 'clinical', 'ai', 'growth')");
                table.HasCheckConstraint("CK_Feature_Kind", "[Kind] IN ('switch', 'limit')");
                table.HasCheckConstraint("CK_Feature_Status", "[Status] IN ('built', 'upcoming')");
                table.HasCheckConstraint(
                    "CK_Feature_Specialty",
                    "[Specialty] IS NULL OR [Specialty] IN ('dental', 'orthopedics', 'neurology', 'pediatrics', 'internal_medicine', 'general_surgery', 'dermatology', 'obgyn', 'ophthalmology', 'ent', 'urology', 'cardiology', 'psychiatry', 'nutrition', 'physiotherapy')");
                table.HasCheckConstraint(
                    "CK_Feature_Limit", "[Kind] <> 'limit' OR ([UnitAr] IS NOT NULL AND [UnitEn] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_Feature_Switch", "[Kind] <> 'switch' OR ([UnitAr] IS NULL AND [UnitEn] IS NULL AND [Baseline] IS NULL)");
                table.HasCheckConstraint("CK_Feature_Baseline", "[Baseline] IS NULL OR [Baseline] BETWEEN 0 AND 1000000");
                table.HasCheckConstraint("CK_Feature_Retired", "[IsRetired] = 0 OR [IsSold] = 1");
            });

            builder.HasKey(f => f.Key);
            builder.Property(f => f.Key).HasMaxLength(FeatureRules.KeyMaxLength).IsUnicode(false).ValueGeneratedNever();
            builder.Property(f => f.NameAr).HasMaxLength(FeatureRules.NameMaxLength).IsRequired();
            builder.Property(f => f.NameEn).HasMaxLength(FeatureRules.NameMaxLength).IsRequired();
            builder.Property(f => f.DescriptionAr).HasMaxLength(FeatureRules.DescriptionMaxLength);
            builder.Property(f => f.DescriptionEn).HasMaxLength(FeatureRules.DescriptionMaxLength);
            builder.Property(f => f.Group).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(f => f.Kind).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(f => f.UnitAr).HasMaxLength(FeatureRules.UnitMaxLength);
            builder.Property(f => f.UnitEn).HasMaxLength(FeatureRules.UnitMaxLength);
            builder.Property(f => f.Specialty).HasMaxLength(24).IsUnicode(false);
            builder.Property(f => f.Status).HasMaxLength(10).IsUnicode(false).IsRequired();
            builder.Property(f => f.Version).IsConcurrencyToken();
            builder.Ignore(f => f.IsMonthly);
            builder.HasIndex(f => new { f.Group, f.SortOrder }).HasDatabaseName("IX_Feature_Order");
        });

        // Spec 013: the plans and what each includes (data-model.md).
        modelBuilder.Entity<Plan>(builder =>
        {
            builder.ToTable("Plan", table =>
            {
                table.HasCheckConstraint(
                    "CK_Plan_Code",
                    "LEN([Code]) BETWEEN 1 AND 110 AND [Code] NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_BIN2 AND LEFT([Code], 1) <> '-' AND RIGHT([Code], 1) <> '-'");
                table.HasCheckConstraint(
                    "CK_Plan_Prices", "[MonthlyEgp] >= 0 AND [AnnualEgp] >= 0 AND [MonthlyUsd] >= 0 AND [AnnualUsd] >= 0");
                table.HasCheckConstraint("CK_Plan_State", "[State] IN ('active', 'disabled')");
                table.HasCheckConstraint(
                    "CK_Plan_Badge",
                    "[Badge] IS NULL OR ([Badge] IN ('best', 'most_popular', 'best_selling') AND [State] = 'active')");
                table.HasCheckConstraint(
                    "CK_Plan_Disabled", "[State] = 'disabled' OR ([DisabledReason] IS NULL AND [DisabledAtUtc] IS NULL)");
            });

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.Code).HasMaxLength(PlanRules.CodeMaxLength).IsUnicode(false).IsRequired();
            builder.Property(p => p.NameAr).HasMaxLength(PlanRules.NameMaxLength).IsRequired();
            builder.Property(p => p.NameEn).HasMaxLength(PlanRules.NameMaxLength).IsRequired();
            builder.Property(p => p.Description).HasMaxLength(PlanRules.DescriptionMaxLength).IsRequired();
            builder.Property(p => p.MonthlyEgp).HasPrecision(12, 2);
            builder.Property(p => p.AnnualEgp).HasPrecision(12, 2);
            builder.Property(p => p.MonthlyUsd).HasPrecision(12, 2);
            builder.Property(p => p.AnnualUsd).HasPrecision(12, 2);
            builder.Property(p => p.Badge).HasMaxLength(16).IsUnicode(false);
            builder.Property(p => p.State).HasMaxLength(10).IsUnicode(false).IsRequired();
            builder.Property(p => p.DisabledReason).HasMaxLength(PlanRules.ReasonMaxLength);
            builder.Property(p => p.Version).IsConcurrencyToken();
            builder.Ignore(p => p.IsActive);

            // One plan on the platform holds a badge (ق-٤٥): every holder is active (CK_Plan_Badge), so
            // the state is unique among the plans that hold one. (A filter cannot name a computed column.)
            builder.HasIndex(p => p.State).IsUnique().HasFilter("[Badge] IS NOT NULL").HasDatabaseName("UX_Plan_Badge");
            builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("UX_Plan_Code");

            builder.HasMany(p => p.Features).WithOne().HasForeignKey(f => f.PlanId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(p => p.Features).HasField("features").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PlanFeature>(builder =>
        {
            builder.ToTable("PlanFeature", table =>
                table.HasCheckConstraint("CK_PlanFeature_Limit", "[Limit] IS NULL OR [Limit] BETWEEN 1 AND 1000000"));

            builder.HasKey(f => new { f.PlanId, f.FeatureKey });
            builder.Property(f => f.FeatureKey).HasMaxLength(FeatureRules.KeyMaxLength).IsUnicode(false);
            builder.HasOne<Feature>().WithMany().HasForeignKey(f => f.FeatureKey).OnDelete(DeleteBehavior.NoAction);
            builder.HasIndex(f => f.FeatureKey).HasDatabaseName("IX_PlanFeature_Feature");
        });
    }
}

internal sealed class PlansDbContextFactory : IDesignTimeDbContextFactory<PlansDbContext>
{
    public PlansDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static PlansDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<PlansDbContext>();
        PlansDbContext.Configure(builder, connection);
        return new PlansDbContext(builder.Options);
    }
}
