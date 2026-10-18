using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel.Clinics;
using Scs.Tenancy.Domain;

namespace Scs.Tenancy.Infrastructure.Persistence;

/// <summary>
/// The Tenancy module's data as the application sees it: under clinic isolation, and read-only
/// (the application login is denied writes on this schema and everything on the access records).
/// It also owns every migration of the module, including the isolation function itself.
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string Schema = ClinicIsolationSql.Schema;
    public const string CrossingSchema = "crossing";
    public const string HistoryTable = "__EFMigrationsHistory";

    public DbSet<Clinic> Clinics => Set<Clinic>();

    /// <summary>Platform-wide: readable with no clinic established.</summary>
    public DbSet<ClinicDirectoryEntry> Directory => Set<ClinicDirectoryEntry>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        TenancyModel.Configure(modelBuilder);
        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>
/// The same data through the operator connection: every clinic, writable. Used only by the
/// declared cross-clinic operations (contracts/isolation.md §5). It owns no migrations.
/// </summary>
public sealed class CrossingDbContext(ICrossClinicConnectionFactory crossing, IServiceProvider? services = null) : DbContext
{
    public DbSet<Clinic> Clinics => Set<Clinic>();

    public DbSet<ClinicDirectoryEntry> Directory => Set<ClinicDirectoryEntry>();

    public DbSet<CrossClinicAccess> Access => Set<CrossClinicAccess>();

    /// <summary>The operator connection, for a second context of the same kind (the add's turn, spec 015).</summary>
    internal ICrossClinicConnectionFactory Factory => crossing;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        crossing.Configure(optionsBuilder);

        // The trail names the operator of the request, as it does for every other entry (spec 015, research R5).
        if (services is not null)
        {
            optionsBuilder.UseApplicationServiceProvider(services);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) => TenancyModel.Configure(modelBuilder);
}

/// <summary>One description of the tables, shared by the two contexts.</summary>
internal static class TenancyModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TenancyDbContext.Schema);

        modelBuilder.Entity<Clinic>(builder =>
        {
            builder.ToTable("Clinic", table =>
            {
                table.HasCheckConstraint("CK_Clinic_ClinicIdIsId", "[ClinicId] = [Id]");

                // Spec 015 (data-model.md §1). SQL Server has no regular expressions: LIKE patterns.
                table.HasCheckConstraint(
                    "CK_Clinic_State", "[State] IN ('setting-up', 'active', 'suspended', 'cancelled', 'discarded')");
                table.HasCheckConstraint(
                    "CK_Clinic_Specialty",
                    $"[Specialty] IS NULL OR [Specialty] IN ({string.Join(", ", Specialties.All.Select(s => $"'{s}'"))})");
                table.HasCheckConstraint(
                    "CK_Clinic_TaxNumber",
                    "[TaxNumber] IS NULL OR [TaxNumber] LIKE '[0-9][0-9][0-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9]'");
                table.HasCheckConstraint("CK_Clinic_DefaultLanguage", "[DefaultLanguage] IN ('ar', 'en')");
            });

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.ClinicId).IsRequired();
            builder.Property(c => c.NameAr).HasMaxLength(Clinic.NameMaxLength).IsRequired();
            builder.Property(c => c.NameEn).HasMaxLength(Clinic.NameMaxLength).IsRequired();
            builder.Property(c => c.State).HasMaxLength(Clinic.StateMaxLength).IsRequired().HasDefaultValue("active");
            builder.Property(c => c.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(c => c.Specialty).HasMaxLength(Clinic.SpecialtyMaxLength).IsUnicode(false);
            builder.Property(c => c.Address).HasMaxLength(Clinic.AddressMaxLength);
            builder.Property(c => c.TaxNumber).HasColumnType("char(11)").IsUnicode(false);
            builder.Property(c => c.DefaultLanguage).HasColumnType("varchar(2)").IsUnicode(false).IsRequired().HasDefaultValue("ar");
            builder.Property(c => c.CreatedBy).HasMaxLength(Clinic.CreatedByMaxLength);
            builder.Property(c => c.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ClinicDirectoryEntry>(builder =>
        {
            builder.ToTable("ClinicDirectory");
            builder.HasKey(e => e.ShortName);
            builder.Property(e => e.ShortName).HasMaxLength(ShortName.MaxLength).IsRequired();
            builder.Property(e => e.ClinicId).IsRequired();
            builder.Property(e => e.IsCurrent).IsRequired();
            builder.Property(e => e.AssignedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.HasOne<Clinic>().WithMany().HasForeignKey(e => e.ClinicId).OnDelete(DeleteBehavior.Restrict);

            // Exactly one current name per clinic.
            builder.HasIndex(e => e.ClinicId).IsUnique().HasFilter("[IsCurrent] = 1")
                .HasDatabaseName("UX_ClinicDirectory_CurrentPerClinic");
        });

        modelBuilder.Entity<CrossClinicAccess>(builder =>
        {
            builder.ToTable("CrossClinicAccess", TenancyDbContext.CrossingSchema);
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).UseIdentityColumn();
            builder.Property(a => a.Operation).HasMaxLength(CrossClinicAccess.OperationMaxLength).IsRequired();
            builder.Property(a => a.OperatorId).HasMaxLength(CrossClinicAccess.OperatorMaxLength).IsRequired();
            builder.Property(a => a.TraceId).HasColumnType("char(32)");
            builder.Property(a => a.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.HasIndex(a => a.OccurredAtUtc);
        });
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static TenancyDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        TenancyDbContext.Configure(builder, connection);
        return new TenancyDbContext(builder.Options, new ClinicContext());
    }
}
