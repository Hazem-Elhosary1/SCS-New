using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Operators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperatorsRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                schema: "ops",
                table: "Operator",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                schema: "ops",
                table: "Operator",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedById",
                schema: "ops",
                table: "Operator",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedByName",
                schema: "ops",
                table: "Operator",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                schema: "ops",
                table: "Operator",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                schema: "ops",
                table: "Operator",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAtUtc",
                schema: "ops",
                table: "Operator",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoStepRequired",
                schema: "ops",
                table: "Operator",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameKey = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SystemKey = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                    table.CheckConstraint("CK_Role_Named", "[SystemKey] IS NOT NULL OR LEN([Name]) > 0");
                    table.CheckConstraint("CK_Role_SystemKept", "[SystemKey] IS NULL OR [DeletedAtUtc] IS NULL");
                    table.CheckConstraint("CK_Role_SystemKey", "[SystemKey] IS NULL OR [SystemKey] IN ('super-admin', 'billing-admin', 'viewer')");
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "ops",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.Code });
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ops",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Operator_Newest",
                schema: "ops",
                table: "Operator",
                column: "CreatedAtUtc",
                descending: Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "IX_Operator_Role",
                schema: "ops",
                table: "Operator",
                column: "RoleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Operator_Department",
                schema: "ops",
                table: "Operator",
                sql: "[Department] IS NULL OR [Department] IN ('support', 'billing', 'success', 'sales', 'management')");

            migrationBuilder.CreateIndex(
                name: "UX_Role_Name",
                schema: "ops",
                table: "Role",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Role_SystemKey",
                schema: "ops",
                table: "Role",
                column: "SystemKey",
                unique: true,
                filter: "[SystemKey] IS NOT NULL");


            // The platform's own three roles (spec 011, FR-010), with fixed identifiers; their
            // names are the console's, so the row holds none. Written as literals: a migration
            // says what it did on the day it ran, whatever the catalogue becomes later.
            migrationBuilder.Sql(
                """
                INSERT INTO [ops].[Role] ([Id], [Name], [NameKey], [SystemKey], [CreatedAtUtc]) VALUES
                ('0b11a000-0000-7000-8000-000000000001', N'', 'super-admin', 'super-admin', SYSDATETIMEOFFSET()),
                ('0b11a000-0000-7000-8000-000000000002', N'', 'billing-admin', 'billing-admin', SYSDATETIMEOFFSET()),
                ('0b11a000-0000-7000-8000-000000000003', N'', 'viewer', 'viewer', SYSDATETIMEOFFSET());

                INSERT INTO [ops].[RolePermission] ([RoleId], [Code]) VALUES
                ('0b11a000-0000-7000-8000-000000000001', 'dashboard.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.edit'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.lifecycle'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.payments'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.integrations'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.users'),
                ('0b11a000-0000-7000-8000-000000000001', 'clinics.topups'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.viewAll'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.reply'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.close'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.assign'),
                ('0b11a000-0000-7000-8000-000000000001', 'tickets.create'),
                ('0b11a000-0000-7000-8000-000000000001', 'notices.send'),
                ('0b11a000-0000-7000-8000-000000000001', 'billing.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'billing.managePlans'),
                ('0b11a000-0000-7000-8000-000000000001', 'operators.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'operators.manage'),
                ('0b11a000-0000-7000-8000-000000000001', 'operators.manageRoles'),
                ('0b11a000-0000-7000-8000-000000000001', 'reports.financial'),
                ('0b11a000-0000-7000-8000-000000000001', 'reports.security'),
                ('0b11a000-0000-7000-8000-000000000001', 'reports.operators'),
                ('0b11a000-0000-7000-8000-000000000001', 'reports.activity'),
                ('0b11a000-0000-7000-8000-000000000001', 'reports.export'),
                ('0b11a000-0000-7000-8000-000000000001', 'security.manage'),
                ('0b11a000-0000-7000-8000-000000000001', 'settings.view'),
                ('0b11a000-0000-7000-8000-000000000001', 'settings.edit'),
                ('0b11a000-0000-7000-8000-000000000002', 'dashboard.view'),
                ('0b11a000-0000-7000-8000-000000000002', 'clinics.view'),
                ('0b11a000-0000-7000-8000-000000000002', 'clinics.payments'),
                ('0b11a000-0000-7000-8000-000000000002', 'clinics.topups'),
                ('0b11a000-0000-7000-8000-000000000002', 'tickets.view'),
                ('0b11a000-0000-7000-8000-000000000002', 'tickets.reply'),
                ('0b11a000-0000-7000-8000-000000000002', 'billing.view'),
                ('0b11a000-0000-7000-8000-000000000002', 'billing.managePlans'),
                ('0b11a000-0000-7000-8000-000000000002', 'reports.financial'),
                ('0b11a000-0000-7000-8000-000000000002', 'reports.export'),
                ('0b11a000-0000-7000-8000-000000000002', 'settings.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'dashboard.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'clinics.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'tickets.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'tickets.viewAll'),
                ('0b11a000-0000-7000-8000-000000000003', 'billing.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'operators.view'),
                ('0b11a000-0000-7000-8000-000000000003', 'reports.financial'),
                ('0b11a000-0000-7000-8000-000000000003', 'reports.security'),
                ('0b11a000-0000-7000-8000-000000000003', 'reports.operators'),
                ('0b11a000-0000-7000-8000-000000000003', 'reports.activity'),
                ('0b11a000-0000-7000-8000-000000000003', 'settings.view');
                """);

            // Everyone who was an operator before roles existed could do everything: each is a
            // «مسؤول عام» from now on (FR-027b). And an active account became active when its
            // first password was set, or — for the few made before that was recorded — when it
            // was created.
            migrationBuilder.Sql(
                """
                UPDATE [ops].[Operator] SET [RoleId] = '0b11a000-0000-7000-8000-000000000001';

                UPDATE o SET [ActivatedAtUtc] = COALESCE(
                    (SELECT MIN(r.[OccurredAtUtc]) FROM [ops].[SignInRecord] r
                     WHERE r.[OperatorId] = o.[Id] AND r.[Kind] = 'first_password_set'),
                    o.[CreatedAtUtc])
                FROM [ops].[Operator] o
                WHERE o.[State] <> 'invited';

                UPDATE [ops].[Operator] SET [SuspendedAtUtc] = SYSDATETIMEOFFSET() WHERE [State] = 'suspended';
                """);

            // No default for the role: an account is given one or is not written.
            migrationBuilder.Sql(
                """
                DECLARE @default sysname = (
                    SELECT dc.[name] FROM sys.default_constraints dc
                    JOIN sys.columns c ON c.[default_object_id] = dc.[object_id]
                    WHERE dc.[parent_object_id] = OBJECT_ID(N'[ops].[Operator]') AND c.[name] = N'RoleId');
                IF @default IS NOT NULL EXEC(N'ALTER TABLE [ops].[Operator] DROP CONSTRAINT [' + @default + N']');
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Operator_Role_RoleId",
                schema: "ops",
                table: "Operator",
                column: "RoleId",
                principalSchema: "ops",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Operator_Role_RoleId",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "ops");

            migrationBuilder.DropIndex(
                name: "IX_Operator_Newest",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropIndex(
                name: "IX_Operator_Role",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Operator_Department",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "Department",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "InvitedById",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "InvitedByName",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                schema: "ops",
                table: "Operator");

            migrationBuilder.DropColumn(
                name: "TwoStepRequired",
                schema: "ops",
                table: "Operator");
        }
    }
}
