using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Subscriptions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Subscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "subscriptions");

            migrationBuilder.CreateTable(
                name: "ClinicPlan",
                schema: "subscriptions",
                columns: table => new
                {
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SetAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicPlan", x => x.ClinicId);
                });

            migrationBuilder.CreateTable(
                name: "MeterEntry",
                schema: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Month = table.Column<string>(type: "char(7)", unicode: false, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    By = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BeyondAllowance = table.Column<bool>(type: "bit", nullable: false),
                    GivesBack = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterEntry", x => x.Id);
                    table.CheckConstraint("CK_MeterEntry_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_MeterEntry_GivesBack", "([Amount] < 0 AND [GivesBack] IS NOT NULL) OR ([Amount] > 0 AND [GivesBack] IS NULL)");
                    table.CheckConstraint("CK_MeterEntry_Key", "[FeatureKey] IN ('message_credits', 'teleconsult_minutes', 'ai_xray_analysis', 'ai_voice_notes')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeterEntry_Month",
                schema: "subscriptions",
                table: "MeterEntry",
                columns: new[] { "ClinicId", "FeatureKey", "Month" })
                .Annotation("SqlServer:Include", new[] { "Amount" });

            // Both tables are clinic-owned: the database itself keeps each clinic to its own rows.
            migrationBuilder.CreateClinicIsolation("subscriptions", "ClinicPlan");
            migrationBuilder.CreateClinicIsolation("subscriptions", "MeterEntry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("subscriptions", "MeterEntry");
            migrationBuilder.DropClinicIsolation("subscriptions", "ClinicPlan");

            migrationBuilder.DropTable(
                name: "ClinicPlan",
                schema: "subscriptions");

            migrationBuilder.DropTable(
                name: "MeterEntry",
                schema: "subscriptions");
        }
    }
}
