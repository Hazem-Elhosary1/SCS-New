using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scs.Platform.Application;
using Scs.Platform.Domain;

namespace Scs.Platform.Infrastructure.Persistence;

/// <summary>The Platform module's own schema. No table here is clinic-owned.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";
    public const string HistoryTable = "__EFMigrationsHistory";

    public DbSet<JobHeartbeat> JobHeartbeats => Set<JobHeartbeat>();

    /// <summary>The one way this context is configured, shared by the app, the migrator and the tools.</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new JobHeartbeatConfiguration());
    }
}

internal sealed class JobHeartbeatConfiguration : IEntityTypeConfiguration<JobHeartbeat>
{
    /// <summary>Fixed, so the migration is the same every time it is generated.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<JobHeartbeat> builder)
    {
        builder.ToTable("JobHeartbeat", table =>
            table.HasCheckConstraint("CK_JobHeartbeat_SingleRow", "[Id] = 1"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnType("smallint").ValueGeneratedNever();
        builder.Property(h => h.JobName).HasMaxLength(JobHeartbeat.JobNameMaxLength).IsRequired();
        builder.Property(h => h.LastRunAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(h => h.LastRunVersion).HasMaxLength(JobHeartbeat.VersionMaxLength).IsRequired();

        builder.HasData(new
        {
            Id = JobHeartbeat.SingleId,
            JobName = JobHeartbeat.HeartbeatJobName,
            LastRunAtUtc = SeededAt,
            LastRunVersion = "never",
        });
    }
}

internal sealed class HeartbeatStore(PlatformDbContext db) : IHeartbeatStore
{
    public Task<JobHeartbeat> GetAsync(CancellationToken cancellation) =>
        db.JobHeartbeats.SingleAsync(h => h.Id == JobHeartbeat.SingleId, cancellation);

    public Task SaveAsync(CancellationToken cancellation) => db.SaveChangesAsync(cancellation);
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
                         ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true";

        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        PlatformDbContext.Configure(builder, connection);
        return new PlatformDbContext(builder.Options);
    }
}
