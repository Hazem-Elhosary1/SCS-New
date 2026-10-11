using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Infrastructure.Persistence;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountsSessionsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "security");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AnonymousSignInAttempt",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailKey = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TraceId = table.Column<string>(type: "char(32)", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymousSignInAttempt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignInEvent",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    Category = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Detail = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TraceId = table.Column<string>(type: "char(32)", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffAccount",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    FailedAttempts = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    LastFailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SpentCodes = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAccount", x => x.Id);
                    table.CheckConstraint("CK_StaffAccount_PasswordUnlessInvited", "[State] = 'invited' OR [PasswordHash] IS NOT NULL");
                    table.CheckConstraint("CK_StaffAccount_State", "[State] IN ('invited', 'active', 'disabled')");
                });

            migrationBuilder.CreateTable(
                name: "Session",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RenewalHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    PreviousRenewalHash = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    PreviousValidUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndReason = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Session_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SignInChallenge",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResendAvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResendsUsed = table.Column<byte>(type: "tinyint", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInChallenge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignInChallenge_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Each clinic-owned table is isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation("identity", "StaffAccount");
            migrationBuilder.CreateClinicIsolation("identity", "SignInChallenge");
            migrationBuilder.CreateClinicIsolation("identity", "Session");
            migrationBuilder.CreateClinicIsolation("identity", "SignInEvent");

            migrationBuilder.Sql(IdentitySql.CreateEventsAppendOnlyTrigger);
            migrationBuilder.Sql(IdentitySql.CreateFindAccountForSignIn);

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousSignInAttempt_OccurredAtUtc",
                schema: "security",
                table: "AnonymousSignInAttempt",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Session_AccountId",
                schema: "identity",
                table: "Session",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_AliveByAccount",
                schema: "identity",
                table: "Session",
                columns: new[] { "ClinicId", "AccountId" },
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SignInChallenge_ClinicId_CreatedAtUtc",
                schema: "identity",
                table: "SignInChallenge",
                columns: new[] { "ClinicId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SignInChallenge_OnePendingPerAccount",
                schema: "identity",
                table: "SignInChallenge",
                column: "AccountId",
                unique: true,
                filter: "[ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SignInEvent_AccountId_OccurredAtUtc",
                schema: "identity",
                table: "SignInEvent",
                columns: new[] { "AccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SignInEvent_ClinicId_OccurredAtUtc",
                schema: "identity",
                table: "SignInEvent",
                columns: new[] { "ClinicId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccount_ClinicId",
                schema: "identity",
                table: "StaffAccount",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "UX_StaffAccount_Email",
                schema: "identity",
                table: "StaffAccount",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IdentitySql.DropFindAccountForSignIn);
            migrationBuilder.Sql(IdentitySql.DropEventsAppendOnlyTrigger);
            migrationBuilder.DropClinicIsolation("identity", "SignInEvent");
            migrationBuilder.DropClinicIsolation("identity", "Session");
            migrationBuilder.DropClinicIsolation("identity", "SignInChallenge");
            migrationBuilder.DropClinicIsolation("identity", "StaffAccount");

            migrationBuilder.DropTable(
                name: "AnonymousSignInAttempt",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Session",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "SignInChallenge",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "SignInEvent",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "StaffAccount",
                schema: "identity");
        }
    }
}
