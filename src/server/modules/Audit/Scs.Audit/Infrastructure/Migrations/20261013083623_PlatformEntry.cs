using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Audit;

#nullable disable

namespace Scs.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformEntry",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorKind = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ActionCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    TargetKind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Facts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TraceId = table.Column<string>(type: "char(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformEntry", x => x.Id);
                    table.CheckConstraint("CK_PlatformEntry_ActorKind", "[ActorKind] IN ('operator', 'system')");
                    table.CheckConstraint("CK_PlatformEntry_Category", "[Category] IN ('security', 'changes', 'records', 'users')");
                    table.CheckConstraint("CK_PlatformEntry_Changes", "[Changes] IS NULL OR ISJSON([Changes]) = 1");
                    table.CheckConstraint("CK_PlatformEntry_Facts", "[Facts] IS NULL OR ISJSON([Facts]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEntry_ActorAccountId_OccurredAtUtc",
                schema: "audit",
                table: "PlatformEntry",
                columns: new[] { "ActorAccountId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEntry_TargetKind_TargetId_OccurredAtUtc",
                schema: "audit",
                table: "PlatformEntry",
                columns: new[] { "TargetKind", "TargetId", "OccurredAtUtc" });

            // Nothing changes an entry and nothing removes one, as for a clinic's (spec 007, FR-020).
            migrationBuilder.Sql(AuditSql.CreatePlatformAppendOnlyTrigger);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditSql.DropPlatformAppendOnlyTrigger);

            migrationBuilder.DropTable(
                name: "PlatformEntry",
                schema: "audit");
        }
    }
}
