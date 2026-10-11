using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PasswordLinksAndTemporaryPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "identity",
                table: "StaffAccount",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TemporaryPasswordExpiresAtUtc",
                schema: "identity",
                table: "StaffAccount",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorAccountId",
                schema: "identity",
                table: "SignInEvent",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountEmailRequest",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HandledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountEmailRequest", x => x.Id);
                    table.CheckConstraint("CK_AccountEmailRequest_Kind", "[Kind] IN ('reset-link', 'password-changed')");
                    table.ForeignKey(
                        name: "FK_AccountEmailRequest_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordLink",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordLink", x => x.Id);
                    table.CheckConstraint("CK_PasswordLink_Kind", "[Kind] IN ('reset', 'new-account')");
                    table.ForeignKey(
                        name: "FK_PasswordLink_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Each clinic-owned table is isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation("identity", "PasswordLink");
            migrationBuilder.CreateClinicIsolation("identity", "AccountEmailRequest");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffAccount_ExpiryOnlyIfTemporary",
                schema: "identity",
                table: "StaffAccount",
                sql: "[MustChangePassword] = 1 OR [TemporaryPasswordExpiresAtUtc] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffAccount_TemporaryNotInvited",
                schema: "identity",
                table: "StaffAccount",
                sql: "[MustChangePassword] = 0 OR [State] <> 'invited'");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEmailRequest_AccountId",
                schema: "identity",
                table: "AccountEmailRequest",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEmailRequest_Waiting",
                schema: "identity",
                table: "AccountEmailRequest",
                column: "ClinicId",
                filter: "[HandledAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordLink_AccountId_Kind_CreatedAtUtc",
                schema: "identity",
                table: "PasswordLink",
                columns: new[] { "AccountId", "Kind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordLink_ClinicId_CreatedAtUtc",
                schema: "identity",
                table: "PasswordLink",
                columns: new[] { "ClinicId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PasswordLink_OneLivePerAccount",
                schema: "identity",
                table: "PasswordLink",
                column: "AccountId",
                unique: true,
                filter: "[UsedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("identity", "AccountEmailRequest");
            migrationBuilder.DropClinicIsolation("identity", "PasswordLink");

            migrationBuilder.DropTable(
                name: "AccountEmailRequest",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PasswordLink",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffAccount_ExpiryOnlyIfTemporary",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffAccount_TemporaryNotInvited",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAtUtc",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "ActorAccountId",
                schema: "identity",
                table: "SignInEvent");
        }
    }
}
