using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.Operators.Infrastructure.Persistence;

#nullable disable

namespace Scs.Operators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOperators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "KnownBrowser",
                schema: "ops",
                columns: table => new
                {
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarkHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownBrowser", x => new { x.OperatorId, x.MarkHash });
                });

            migrationBuilder.CreateTable(
                name: "Operator",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Language = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAttempts = table.Column<byte>(type: "tinyint", nullable: false),
                    LastFailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SpentCodes = table.Column<byte>(type: "tinyint", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSignInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InvitationOwedSinceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operator", x => x.Id);
                    table.CheckConstraint("CK_Operator_Language", "[Language] IN ('ar', 'en')");
                    table.CheckConstraint("CK_Operator_Password", "[State] = 'invited' OR [PasswordHash] IS NOT NULL");
                    table.CheckConstraint("CK_Operator_State", "[State] IN ('invited', 'active', 'suspended')");
                });

            migrationBuilder.CreateTable(
                name: "PasswordLink",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordLink", x => x.Id);
                    table.CheckConstraint("CK_PasswordLink_Kind", "[Kind] IN ('reset', 'first')");
                });

            migrationBuilder.CreateTable(
                name: "Session",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Remember = table.Column<bool>(type: "bit", nullable: false),
                    RenewalHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    PreviousRenewalHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    PreviousValidUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedFor = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Session", x => x.Id);
                    table.CheckConstraint("CK_Session_Ended", "([EndedAtUtc] IS NULL AND [EndedFor] IS NULL) OR ([EndedAtUtc] IS NOT NULL AND [EndedFor] IN ('signed-out', 'replaced', 'idle', 'expired', 'password-reset', 'suspended', 'signed-out-everywhere', 'reuse', 'role-changed', 'locked'))");
                });

            migrationBuilder.CreateTable(
                name: "SignInChallenge",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PendingPasswordHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    CodeHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CodeSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResendAvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false),
                    ResendsUsed = table.Column<byte>(type: "tinyint", nullable: false),
                    Remember = table.Column<bool>(type: "bit", nullable: false),
                    Address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInChallenge", x => x.Id);
                    table.CheckConstraint("CK_SignInChallenge_Purpose", "([Purpose] = 'sign-in' AND [PendingPasswordHash] IS NULL) OR ([Purpose] = 'password-change' AND [PendingPasswordHash] IS NOT NULL AND [SessionId] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "SignInRecord",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Facts = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Operator_Email",
                schema: "ops",
                table: "Operator",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordLink_Recent",
                schema: "ops",
                table: "PasswordLink",
                columns: new[] { "OperatorId", "Kind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Session_Recent",
                schema: "ops",
                table: "Session",
                columns: new[] { "OperatorId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Session_OneOpen",
                schema: "ops",
                table: "Session",
                column: "OperatorId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SignInChallenge_Expires",
                schema: "ops",
                table: "SignInChallenge",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SignInChallenge_Standing",
                schema: "ops",
                table: "SignInChallenge",
                columns: new[] { "OperatorId", "Purpose" },
                filter: "[ConsumedAtUtc] IS NULL AND [ReplacedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SignInRecord_Kind",
                schema: "ops",
                table: "SignInRecord",
                columns: new[] { "Kind", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SignInRecord_Operator",
                schema: "ops",
                table: "SignInRecord",
                columns: new[] { "OperatorId", "OccurredAtUtc" });

            // A record of signing in is kept as written: no change, no early removal (FR-052).
            migrationBuilder.Sql(OperatorsSql.CreateRecordTrigger);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OperatorsSql.DropRecordTrigger);

            migrationBuilder.DropTable(
                name: "KnownBrowser",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Operator",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "PasswordLink",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Session",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "SignInChallenge",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "SignInRecord",
                schema: "ops");
        }
    }
}
