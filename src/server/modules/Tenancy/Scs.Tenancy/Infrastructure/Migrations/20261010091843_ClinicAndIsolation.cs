using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicAndIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            // The function every clinic-owned table's isolation policy calls. Created once, here.
            migrationBuilder.CreateClinicAccessFunction();

            migrationBuilder.CreateTable(
                name: "Clinic",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinic", x => x.Id);
                    table.CheckConstraint("CK_Clinic_ClinicIdIsId", "[ClinicId] = [Id]");
                });

            // Same migration as the table: a clinic-owned table never exists unprotected.
            migrationBuilder.CreateClinicIsolation("tenancy", "Clinic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("tenancy", "Clinic");

            migrationBuilder.DropTable(
                name: "Clinic",
                schema: "tenancy");

            migrationBuilder.DropClinicAccessFunction();
        }
    }
}
