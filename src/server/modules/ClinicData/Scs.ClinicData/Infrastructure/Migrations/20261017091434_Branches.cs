using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.ClinicData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Branches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clinicdata");

            migrationBuilder.CreateTable(
                name: "Branch",
                schema: "clinicdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branch", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Branch_OneMain",
                schema: "clinicdata",
                table: "Branch",
                column: "ClinicId",
                unique: true,
                filter: "[IsMain] = 1");

            // Clinic-owned: the database itself keeps each clinic to its own branches.
            migrationBuilder.CreateClinicIsolation("clinicdata", "Branch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("clinicdata", "Branch");

            migrationBuilder.DropTable(
                name: "Branch",
                schema: "clinicdata");
        }
    }
}
