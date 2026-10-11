using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffPreference",
                schema: "identity",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SidebarFolded = table.Column<bool>(type: "bit", nullable: true),
                    DismissedBanner = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    DismissedOnDay = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPreference", x => x.AccountId);
                    table.CheckConstraint("CK_StaffPreference_Banner", "[DismissedBanner] IS NULL OR [DismissedBanner] IN ('trial', 'expiring', 'grace')");
                    table.CheckConstraint("CK_StaffPreference_BannerWithItsDay", "([DismissedBanner] IS NULL AND [DismissedOnDay] IS NULL) OR ([DismissedBanner] IS NOT NULL AND [DismissedOnDay] IS NOT NULL)");
                    table.CheckConstraint("CK_StaffPreference_Language", "[Language] IS NULL OR [Language] IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "FK_StaffPreference_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Each clinic-owned table is isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation("identity", "StaffPreference");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPreference_ClinicId",
                schema: "identity",
                table: "StaffPreference",
                column: "ClinicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("identity", "StaffPreference");

            migrationBuilder.DropTable(
                name: "StaffPreference",
                schema: "identity");
        }
    }
}
