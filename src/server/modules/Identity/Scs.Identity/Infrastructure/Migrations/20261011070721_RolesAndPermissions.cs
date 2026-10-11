using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolesAndPermissions : Migration
    {
        // What the two editable ready roles hold in a clinic that exists when this migration
        // runs (BRD Chapter 10 §2-3, 11 Oct 2026). Frozen here: a clinic created later gets its
        // roles from ReadyRoles, and a later change to the catalogue never rewrites these.
        private static readonly string[] ManagerCodes =
        [
            "bookings.view", "bookings.viewAll", "bookings.manage", "bookings.cancel",
            "bookings.attendance", "bookings.queue", "bookings.waitlist", "patients.view",
            "patients.manage", "patients.merge", "patients.results", "patients.consents.collect",
            "patients.recall.view", "patients.recall.log", "money.view", "money.invoice",
            "money.collect", "money.refund", "money.shift", "money.statements.view",
            "money.statements.approve", "inbox.view", "inbox.reply", "inbox.link",
            "inbox.block", "tickets.view", "tickets.viewAll", "tickets.create",
            "tickets.reply", "reports.financial", "reports.clinical", "reports.activity",
            "reports.weekly", "reports.feedback", "reports.feedback.followup", "reports.export",
            "admin.users.view", "admin.users.manage", "admin.roles.view", "admin.roles.manage",
            "admin.licences.view", "admin.licences.manage", "data.doctors.view", "data.doctors.manage",
            "data.branches.view", "data.branches.manage", "data.services.view", "data.services.manage",
            "settings.view", "settings.hours", "settings.policies", "settings.documents",
            "settings.payments", "settings.bookingPage", "subscription.view", "data.consents.view",
            "data.consents.manage", "patients.lab.view", "patients.lab.manage",
        ];

        private static readonly string[] DoctorCodes =
        [
            "bookings.view", "bookings.manage", "bookings.attendance", "bookings.queue",
            "patients.view", "patients.manage", "patients.records.view", "patients.exam.edit",
            "patients.exam.sign", "patients.documents", "patients.orders", "patients.results",
            "patients.consents.collect", "patients.consents.withdraw", "patients.plans.create", "patients.plans.present",
            "patients.plans.accept", "patients.plans.execute", "patients.recall.view", "patients.recall.log",
            "money.view", "money.statements.view", "inbox.view", "inbox.reply",
            "tickets.view", "tickets.create", "tickets.reply", "reports.clinical",
            "reports.weekly", "reports.feedback", "admin.users.view", "admin.roles.view",
            "data.doctors.view", "data.branches.view", "data.services.view", "settings.view",
            "settings.myHours", "data.consents.view", "patients.lab.view", "patients.lab.manage",
        ];

        private static string Values(string[] codes) => string.Join(", ", codes.Select(code => $"('{code}')"));

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                schema: "identity",
                table: "StaffAccount",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    ReadyKey = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    IsRenamed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HoldsEverything = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                    table.CheckConstraint("CK_Role_Category", "[Category] IN ('ready', 'custom')");
                    table.CheckConstraint("CK_Role_NameNotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_Role_OnlyOwnerHoldsEverything", "([HoldsEverything] = 1 AND [ReadyKey] = 'owner') OR ([HoldsEverything] = 0 AND ([ReadyKey] IS NULL OR [ReadyKey] <> 'owner'))");
                    table.CheckConstraint("CK_Role_ReadyKey", "([Category] = 'ready' AND [ReadyKey] IN ('owner', 'manager', 'doctor')) OR ([Category] = 'custom' AND [ReadyKey] IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.Code });
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccount_Role",
                schema: "identity",
                table: "StaffAccount",
                columns: new[] { "ClinicId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccount_RoleId",
                schema: "identity",
                table: "StaffAccount",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_Role_NameInClinic",
                schema: "identity",
                table: "Role",
                columns: new[] { "ClinicId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Role_OneOfEachReadyRole",
                schema: "identity",
                table: "Role",
                columns: new[] { "ClinicId", "ReadyKey" },
                unique: true,
                filter: "[ReadyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_ClinicId",
                schema: "identity",
                table: "RolePermission",
                column: "ClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAccount_Role_RoleId",
                schema: "identity",
                table: "StaffAccount",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Each clinic-owned table is isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation("identity", "Role");
            migrationBuilder.CreateClinicIsolation("identity", "RolePermission");

            // Every clinic that exists gets the three ready roles (spec 005, FR-009), exactly as
            // ReadyRoles gives them to a clinic registered from now on.
            migrationBuilder.Sql(
                """
                INSERT INTO [identity].[Role]
                    (Id, ClinicId, Name, Description, Category, ReadyKey, IsRenamed, HoldsEverything, CreatedAtUtc)
                SELECT NEWID(), c.Id, v.Name, v.Description, 'ready', v.ReadyKey, 0, v.HoldsEverything, SYSDATETIMEOFFSET()
                FROM [tenancy].[Clinic] c
                CROSS JOIN (VALUES
                    ('owner', N'مالك العيادة', N'كل صلاحيات العيادة', 1),
                    ('manager', N'مدير العيادة', N'تشغيل العيادة اليومي، دون غرفة الكشف ولا الإعدادات الفنية ولا الاشتراك', 0),
                    ('doctor', N'طبيب', N'غرفة الكشف وملفات المرضى ومواعيد الطبيب نفسه', 0)
                ) v(ReadyKey, Name, Description, HoldsEverything);
                """);

            migrationBuilder.Sql(
                $"""
                 INSERT INTO [identity].[RolePermission] (RoleId, Code, ClinicId)
                 SELECT r.Id, v.Code, r.ClinicId
                 FROM [identity].[Role] r
                 CROSS JOIN (VALUES {Values(ManagerCodes)}) v(Code)
                 WHERE r.ReadyKey = 'manager';

                 INSERT INTO [identity].[RolePermission] (RoleId, Code, ClinicId)
                 SELECT r.Id, v.Code, r.ClinicId
                 FROM [identity].[Role] r
                 CROSS JOIN (VALUES {Values(DoctorCodes)}) v(Code)
                 WHERE r.ReadyKey = 'doctor';
                 """);

            // No clinic is left without an owner: its oldest account — an active one before any
            // other — becomes «مالك العيادة» (FR-036). Nobody else is given a role.
            migrationBuilder.Sql(
                """
                WITH Ranked AS (
                    SELECT a.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY a.ClinicId
                               ORDER BY CASE a.State WHEN 'active' THEN 0 ELSE 1 END, a.CreatedAtUtc, a.Id) AS Position
                    FROM [identity].[StaffAccount] a
                    WHERE a.RoleId IS NULL
                )
                UPDATE a
                SET a.RoleId = r.Id
                FROM [identity].[StaffAccount] a
                JOIN Ranked f ON f.Id = a.Id AND f.Position = 1
                JOIN [identity].[Role] r ON r.ClinicId = a.ClinicId AND r.ReadyKey = 'owner';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffAccount_Role_RoleId",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropClinicIsolation("identity", "RolePermission");
            migrationBuilder.DropClinicIsolation("identity", "Role");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_StaffAccount_Role",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropIndex(
                name: "IX_StaffAccount_RoleId",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "identity",
                table: "StaffAccount");
        }
    }
}
