using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Tenancy;
using Scs.ClinicData.Domain;

namespace Scs.ClinicData.Infrastructure.Persistence;

/// <summary>
/// The clinic's own data of the clinic volume's chapter 11 «إدارة البيانات» (spec 015): its
/// branches, clinic-owned, under the database's isolation from the migration that makes them.
/// Specs 028 (branches) and 029 (doctors) grow it.
/// </summary>
public sealed class ClinicDataDbContext(DbContextOptions<ClinicDataDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string Schema = "clinicdata";
    public const string HistoryTable = "__EFMigrationsHistory";
    public const string BranchTable = "Branch";

    internal DbSet<Branch> Branches => Set<Branch>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Branch>(builder =>
        {
            builder.ToTable(BranchTable);
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).ValueGeneratedNever();
            builder.Property(b => b.NameAr).HasMaxLength(Branch.NameMaxLength).IsRequired();
            builder.Property(b => b.NameEn).HasMaxLength(Branch.NameMaxLength).IsRequired();
            builder.Property(b => b.Address).HasMaxLength(Branch.AddressMaxLength).IsRequired();
            builder.Property(b => b.CreatedAtUtc).HasColumnType("datetimeoffset");

            // One main branch per clinic, held by the database (FR-026, BRD Ops §3-4).
            builder.HasIndex(b => b.ClinicId)
                .IsUnique()
                .HasFilter("[IsMain] = 1")
                .HasDatabaseName("UX_Branch_OneMain");
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class ClinicDataDbContextFactory : IDesignTimeDbContextFactory<ClinicDataDbContext>
{
    public ClinicDataDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static ClinicDataDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<ClinicDataDbContext>();
        ClinicDataDbContext.Configure(builder, connection);
        return new ClinicDataDbContext(builder.Options, new ClinicContext());
    }
}
