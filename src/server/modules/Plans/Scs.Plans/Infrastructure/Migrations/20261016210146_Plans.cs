using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Plans.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Plans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plan",
                schema: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(110)", unicode: false, maxLength: 110, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyEgp = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AnnualEgp = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthlyUsd = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AnnualUsd = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Badge = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    State = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DisabledReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plan", x => x.Id);
                    table.CheckConstraint("CK_Plan_Badge", "[Badge] IS NULL OR ([Badge] IN ('best', 'most_popular', 'best_selling') AND [State] = 'active')");
                    table.CheckConstraint("CK_Plan_Code", "LEN([Code]) BETWEEN 1 AND 110 AND [Code] NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_BIN2 AND LEFT([Code], 1) <> '-' AND RIGHT([Code], 1) <> '-'");
                    table.CheckConstraint("CK_Plan_Disabled", "[State] = 'disabled' OR ([DisabledReason] IS NULL AND [DisabledAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Plan_Prices", "[MonthlyEgp] >= 0 AND [AnnualEgp] >= 0 AND [MonthlyUsd] >= 0 AND [AnnualUsd] >= 0");
                    table.CheckConstraint("CK_Plan_State", "[State] IN ('active', 'disabled')");
                });

            migrationBuilder.CreateTable(
                name: "PlanFeature",
                schema: "plans",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Limit = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeature", x => new { x.PlanId, x.FeatureKey });
                    table.CheckConstraint("CK_PlanFeature_Limit", "[Limit] IS NULL OR [Limit] BETWEEN 1 AND 1000000");
                    table.ForeignKey(
                        name: "FK_PlanFeature_Feature_FeatureKey",
                        column: x => x.FeatureKey,
                        principalSchema: "plans",
                        principalTable: "Feature",
                        principalColumn: "Key");
                    table.ForeignKey(
                        name: "FK_PlanFeature_Plan_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "plans",
                        principalTable: "Plan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Plan_Badge",
                schema: "plans",
                table: "Plan",
                column: "State",
                unique: true,
                filter: "[Badge] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Plan_Code",
                schema: "plans",
                table: "Plan",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeature_Feature",
                schema: "plans",
                table: "PlanFeature",
                column: "FeatureKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanFeature",
                schema: "plans");

            migrationBuilder.DropTable(
                name: "Plan",
                schema: "plans");
        }
    }
}
