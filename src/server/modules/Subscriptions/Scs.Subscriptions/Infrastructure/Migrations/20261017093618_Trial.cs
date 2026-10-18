using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Subscriptions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Trial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsAtUtc",
                schema: "subscriptions",
                table: "ClinicPlan",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrialEndsAtUtc",
                schema: "subscriptions",
                table: "ClinicPlan");
        }
    }
}
