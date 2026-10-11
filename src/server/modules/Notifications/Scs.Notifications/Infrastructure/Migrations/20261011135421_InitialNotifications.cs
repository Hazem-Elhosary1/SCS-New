using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notify");

            migrationBuilder.CreateTable(
                name: "EmailSwitch",
                schema: "notify",
                columns: table => new
                {
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSwitch", x => new { x.ClinicId, x.EventKey });
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Mark = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    SubjectKind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Occasion = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Facts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.CheckConstraint("CK_Notification_Facts", "[Facts] IS NULL OR ISJSON([Facts]) = 1");
                    table.CheckConstraint("CK_Notification_Mark", "[Mark] IN ('ticket', 'invoice', 'subscription', 'feedback', 'notice', 'warning', 'incident')");
                });

            migrationBuilder.CreateTable(
                name: "OutgoingEmail",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingEmail", x => x.Id);
                    table.CheckConstraint("CK_OutgoingEmail_State", "[State] IN ('waiting', 'sent', 'gave-up', 'dropped')");
                    table.ForeignKey(
                        name: "FK_OutgoingEmail_Notification_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notify",
                        principalTable: "Notification",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ClinicId_RecipientAccountId_RaisedAtUtc_Id",
                schema: "notify",
                table: "Notification",
                columns: new[] { "ClinicId", "RecipientAccountId", "RaisedAtUtc", "Id" },
                descending: new[] { false, false, true, true })
                .Annotation("SqlServer:Include", new[] { "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Unread",
                schema: "notify",
                table: "Notification",
                columns: new[] { "ClinicId", "RecipientAccountId" },
                filter: "[ReadAtUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Notification_OncePerOccasion",
                schema: "notify",
                table: "Notification",
                columns: new[] { "ClinicId", "RecipientAccountId", "EventKey", "SubjectId", "Occasion" },
                unique: true,
                filter: "[Occasion] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmail_NotificationId",
                schema: "notify",
                table: "OutgoingEmail",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmail_Waiting",
                schema: "notify",
                table: "OutgoingEmail",
                columns: new[] { "ClinicId", "NextAttemptAtUtc" },
                filter: "[State] = 'waiting'");

            // Each clinic-owned table is isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation(NotifySql.Schema, NotifySql.Notification);
            migrationBuilder.CreateClinicIsolation(NotifySql.Schema, NotifySql.OutgoingEmail);
            migrationBuilder.CreateClinicIsolation(NotifySql.Schema, NotifySql.EmailSwitch);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation(NotifySql.Schema, NotifySql.EmailSwitch);
            migrationBuilder.DropClinicIsolation(NotifySql.Schema, NotifySql.OutgoingEmail);
            migrationBuilder.DropClinicIsolation(NotifySql.Schema, NotifySql.Notification);
            migrationBuilder.DropTable(
                name: "EmailSwitch",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "OutgoingEmail",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "Notification",
                schema: "notify");
        }
    }
}
