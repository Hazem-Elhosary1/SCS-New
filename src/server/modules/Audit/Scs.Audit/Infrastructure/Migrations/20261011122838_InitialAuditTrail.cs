using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "Entry",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorKind = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ActionCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    TargetKind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetReadyKey = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    Facts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TraceId = table.Column<string>(type: "char(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entry", x => x.Id);
                    table.CheckConstraint("CK_Entry_ActorKind", "[ActorKind] IN ('staff', 'operator', 'system')");
                    table.CheckConstraint("CK_Entry_Category", "[Category] IN ('security', 'changes', 'records', 'users')");
                    table.CheckConstraint("CK_Entry_Changes", "[Changes] IS NULL OR ISJSON([Changes]) = 1");
                    table.CheckConstraint("CK_Entry_Facts", "[Facts] IS NULL OR ISJSON([Facts]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entry_ClinicId_OccurredAtUtc_Id",
                schema: "audit",
                table: "Entry",
                columns: new[] { "ClinicId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "Category", "ActionCode", "ActorAccountId", "ActorKind" });

            migrationBuilder.CreateIndex(
                name: "IX_Entry_ClinicId_TargetKind_TargetId_OccurredAtUtc",
                schema: "audit",
                table: "Entry",
                columns: new[] { "ClinicId", "TargetKind", "TargetId", "OccurredAtUtc" });

            // Isolated in the migration that creates it (spec 002), and never changed or removed (FR-020).
            migrationBuilder.CreateClinicIsolation(AuditSql.Schema, AuditSql.Table);
            migrationBuilder.Sql(AuditSql.CreateAppendOnlyTrigger);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditSql.DropAppendOnlyTrigger);
            migrationBuilder.DropClinicIsolation(AuditSql.Schema, AuditSql.Table);

            migrationBuilder.DropTable(
                name: "Entry",
                schema: "audit");
        }
    }
}
