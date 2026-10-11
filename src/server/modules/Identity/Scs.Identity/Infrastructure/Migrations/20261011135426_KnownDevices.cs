using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnownDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnownDevice",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarkHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownDevice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnownDevice_StaffAccount_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "StaffAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnownDevice_ClinicId",
                schema: "identity",
                table: "KnownDevice",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "UX_KnownDevice_OnePerBrowser",
                schema: "identity",
                table: "KnownDevice",
                columns: new[] { "AccountId", "MarkHash" },
                unique: true);

            // Isolated in the migration that creates it (spec 002).
            migrationBuilder.CreateClinicIsolation("identity", "KnownDevice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation("identity", "KnownDevice");
            migrationBuilder.DropTable(
                name: "KnownDevice",
                schema: "identity");
        }
    }
}
