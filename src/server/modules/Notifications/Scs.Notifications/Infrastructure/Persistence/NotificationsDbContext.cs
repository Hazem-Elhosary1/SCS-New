using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Tenancy;
using Scs.Notifications.Domain;

namespace Scs.Notifications.Infrastructure.Persistence;

/// <summary>
/// The engine's schema (spec 008): what was told to whom, the emails owed for it, and the
/// clinic's email switches. All clinic-owned. Notifications are written by the shared writer on
/// the connection of whichever module is saving the change; this context reads them, marks them
/// read, settles their emails and removes them after ninety days.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IClinicContext clinic)
    : ClinicAwareDbContext(options, clinic)
{
    public const string HistoryTable = "__EFMigrationsHistory";

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<OutgoingEmail> Emails => Set<OutgoingEmail>();

    public DbSet<EmailSwitch> Switches => Set<EmailSwitch>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseSqlServer(connection, sql => sql.MigrationsHistoryTable(HistoryTable, NotifySql.Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(NotifySql.Schema);

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable(NotifySql.Notification, table =>
            {
                table.HasCheckConstraint(
                    "CK_Notification_Mark",
                    "[Mark] IN ('ticket', 'invoice', 'subscription', 'feedback', 'notice', 'warning', 'incident')");
                table.HasCheckConstraint("CK_Notification_Facts", "[Facts] IS NULL OR ISJSON([Facts]) = 1");
            });

            builder.HasKey(n => n.Id);
            builder.Property(n => n.Id).ValueGeneratedNever();
            builder.Property(n => n.EventKey).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.Property(n => n.Mark).HasMaxLength(16).IsUnicode(false).IsRequired();
            builder.Property(n => n.SubjectKind).HasMaxLength(32).IsUnicode(false);
            builder.Property(n => n.SubjectLabel).HasMaxLength(NotifySql.SubjectLabelMaxLength);
            builder.Property(n => n.Occasion).HasMaxLength(NotifySql.OccasionMaxLength).IsUnicode(false).IsRequired();
            builder.Property(n => n.Link).HasMaxLength(NotifySql.LinkMaxLength);
            builder.Property(n => n.RaisedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(n => n.ReadAtUtc).HasColumnType("datetimeoffset");

            // A person's bell, newest first.
            builder.HasIndex(n => new { n.ClinicId, n.RecipientAccountId, n.RaisedAtUtc, n.Id })
                .IsDescending(false, false, true, true)
                .IncludeProperties(n => n.ReadAtUtc);

            // Whether anything is unread: asked every thirty seconds by every open console.
            builder.HasIndex(n => new { n.ClinicId, n.RecipientAccountId })
                .HasFilter("[ReadAtUtc] IS NULL")
                .IncludeProperties(n => n.RaisedAtUtc)
                .HasDatabaseName("IX_Notification_Unread");

            // Told once for one occasion (FR-008). With no occasion every raise is its own happening.
            builder.HasIndex(n => new { n.ClinicId, n.RecipientAccountId, n.EventKey, n.SubjectId, n.Occasion })
                .IsUnique()
                .HasFilter("[Occasion] <> ''")
                .HasDatabaseName("UX_Notification_OncePerOccasion");
        });

        modelBuilder.Entity<OutgoingEmail>(builder =>
        {
            builder.ToTable(
                NotifySql.OutgoingEmail,
                table => table.HasCheckConstraint("CK_OutgoingEmail_State", "[State] IN ('waiting', 'sent', 'gave-up', 'dropped')"));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.State).HasMaxLength(8).IsUnicode(false).IsRequired();
            builder.Property(e => e.Attempts).HasDefaultValue((byte)0);
            builder.Property(e => e.NextAttemptAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(e => e.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            builder.Property(e => e.SettledAtUtc).HasColumnType("datetimeoffset");
            builder.HasOne(e => e.Notification).WithMany().HasForeignKey(e => e.NotificationId).OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.ClinicId, e.NextAttemptAtUtc })
                .HasFilter("[State] = 'waiting'")
                .HasDatabaseName("IX_OutgoingEmail_Waiting");
        });

        modelBuilder.Entity<EmailSwitch>(builder =>
        {
            builder.ToTable(NotifySql.EmailSwitch);
            builder.HasKey(s => new { s.ClinicId, s.EventKey });
            builder.Property(s => s.EventKey).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.Property(s => s.ChangedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        });

        ApplyClinicIsolation(modelBuilder);
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a running application.</summary>
internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args) => Create(
        Environment.GetEnvironmentVariable("SCS_DB__MIGRATION_CONNECTION")
        ?? "Server=localhost;Database=scs_design_time;Integrated Security=true;TrustServerCertificate=true");

    public static NotificationsDbContext Create(string connection)
    {
        var builder = new DbContextOptionsBuilder<NotificationsDbContext>();
        NotificationsDbContext.Configure(builder, connection);
        return new NotificationsDbContext(builder.Options, new ClinicContext());
    }
}
