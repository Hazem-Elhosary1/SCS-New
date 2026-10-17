using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicSpecialty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                schema: "tenancy",
                table: "Clinic",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Specialty",
                schema: "tenancy",
                table: "Clinic");
        }
    }
}
