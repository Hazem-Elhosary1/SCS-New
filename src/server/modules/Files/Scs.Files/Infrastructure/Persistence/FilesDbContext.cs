using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Tenancy;
using Scs.Files.Domain;
using Scs.SharedKernel.Files;

namespace Scs.Files.Infrastructure.Persistence;

/// <summary>
/// The engine's schema (spec 009): one row for each uploaded file, clinic-owned, and the
/// platform's retention periods. A file is attached and removed by the shared writer on the
/// connection of whichever module saves its record; this context uploads, scans, reads and
/// erases.
/// </summary>
public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string HistoryTable = "__EFMigrationsHistory";

    public DbSet<StoredFile> Files => Set<StoredFile>();

    public DbSet<RetentionPeriod> RetentionPeriods => Set<RetentionPeriod>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, FilesSql.Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(FilesSql.Schema);

        modelBuilder.Entity<StoredFile>(builder =>
        {
            builder.ToTable(FilesSql.StoredFile, table =>
            {
                table.HasCheckConstraint("CK_StoredFile_Size", "[SizeBytes] > 0");
                table.HasCheckConstraint("CK_StoredFile_Type", "[Type] IN ('pdf', 'png', 'jpeg')");
                table.HasCheckConstraint("CK_StoredFile_Scan", "[Scan] IN ('pending', 'clean', 'refused')");
                table.HasCheckConstraint(
                    "CK_StoredFile_RefusedFor",
                    "([Scan] = 'refused' AND [RefusedFor] IN ('infected', 'unreadable')) OR ([Scan] <> 'refused' AND [RefusedFor] IS NULL)");
                table.HasCheckConstraint(
                    "CK_StoredFile_Record",
                    "([RecordKind] IS NULL AND [RecordId] IS NULL AND [AttachedAtUtc] IS NULL) OR ([RecordKind] IS NOT NULL AND [RecordId] IS NOT NULL AND [AttachedAtUtc] IS NOT NULL)");
                table.HasCheckConstraint("CK_StoredFile_Removed", "[RemovedAtUtc] IS NULL OR [RecordId] IS NOT NULL");
                table.HasCheckConstraint("CK_StoredFile_Held", "[HeldAtUtc] IS NULL OR [RecordId] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_StoredFile_Erased",
                    "([ErasedAtUtc] IS NULL AND [ErasedFor] IS NULL) OR ([ErasedAtUtc] IS NOT NULL AND [ErasedFor] IN ('removed', 'age', 'refused'))");
            });

            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();
            builder.Property(f => f.Kind).HasMaxLength(FileKinds.KeyMaxLength).IsUnicode(false).IsRequired();
            builder.Property(f => f.Name).HasMaxLength(FilesSql.NameMaxLength).IsRequired();
            builder.Property(f => f.Type).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(f => f.UploadedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            // Checked when it is written: two scans of one file cannot both settle it.
            builder.Property(f => f.Scan).HasMaxLength(16).IsUnicode(false).IsRequired().IsConcurrencyToken();
            builder.Property(f => f.RefusedFor).HasMaxLength(16).IsUnicode(false);
            builder.Property(f => f.ScannedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.ScanTries).HasDefaultValue(0);
            builder.Property(f => f.RecordKind).HasMaxLength(FileRecord.KindMaxLength).IsUnicode(false);
            builder.Property(f => f.AttachedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.RemovedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.RestoredAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.RetainFromUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.HeldAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.ReleasedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.ErasedAtUtc).HasColumnType("datetimeoffset");
            builder.Property(f => f.ErasedFor).HasMaxLength(16).IsUnicode(false);
            builder.Ignore(f => f.IsAttached);
            builder.Ignore(f => f.HasContent);

            // The files of a record, and how many it holds.
            builder.HasIndex(f => new { f.ClinicId, f.RecordKind, f.RecordId })
                .IncludeProperties(f => f.RemovedAtUtc)
                .HasDatabaseName("IX_StoredFile_Record");

            // The daily removal of what was never attached.
            builder.HasIndex(f => new { f.ClinicId, f.UploadedAtUtc })
                .HasFilter("[RecordId] IS NULL")
                .HasDatabaseName("IX_StoredFile_Unattached");

            // What waits for its scan, and for how long.
            builder.HasIndex(f => new { f.ClinicId, f.UploadedAtUtc })
                .HasFilter("[Scan] = 'pending'")
                .HasDatabaseName("IX_StoredFile_Pending");

            // The thirty days of a removal.
            builder.HasIndex(f => new { f.ClinicId, f.RemovedAtUtc })
                .HasFilter("[RemovedAtUtc] IS NOT NULL AND [ErasedAtUtc] IS NULL")
                .HasDatabaseName("IX_StoredFile_Removed");

            // Erasing for age.
            builder.HasIndex(f => new { f.ClinicId, f.Kind, f.RetainFromUtc })
                .HasFilter("[RetainFromUtc] IS NOT NULL AND [ErasedAtUtc] IS NULL")
                .HasDatabaseName("IX_StoredFile_Retained");

            // One person's files waiting, and uploads in the last ten minutes.
            builder.HasIndex(f => new { f.ClinicId, f.UploadedByAccountId, f.UploadedAtUtc })
                .IncludeProperties(f => f.RecordId)
                .HasDatabaseName("IX_StoredFile_Uploader");
        });

        modelBuilder.Entity<RetentionPeriod>(builder =>
        {
            builder.ToTable(
                FilesSql.RetentionPeriod,
                table => table.HasCheckConstraint("CK_RetentionPeriod_Days", "[Days] IS NULL OR [Days] > 0"));
            builder.HasKey(p => p.Class);
            builder.Property(p => p.Class).HasMaxLength(32).IsUnicode(false);
            builder.Property(p => p.SetAtUtc).HasColumnType("datetimeoffset");
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class FilesDbContextFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    public FilesDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static FilesDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<FilesDbContext>();
        FilesDbContext.Configure(builder, connection);
        return new FilesDbContext(builder.Options, new ClinicContext());
    }
}
