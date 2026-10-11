using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DirectoryAndAccessRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crossing");

            migrationBuilder.CreateTable(
                name: "ClinicDirectory",
                schema: "tenancy",
                columns: table => new
                {
                    ShortName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicDirectory", x => x.ShortName);
                    table.ForeignKey(
                        name: "FK_ClinicDirectory_Clinic_ClinicId",
                        column: x => x.ClinicId,
                        principalSchema: "tenancy",
                        principalTable: "Clinic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CrossClinicAccess",
                schema: "crossing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Operation = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperatorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TraceId = table.Column<string>(type: "char(32)", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossClinicAccess", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ClinicDirectory_CurrentPerClinic",
                schema: "tenancy",
                table: "ClinicDirectory",
                column: "ClinicId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CrossClinicAccess_OccurredAtUtc",
                schema: "crossing",
                table: "CrossClinicAccess",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicDirectory",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "CrossClinicAccess",
                schema: "crossing");
        }
    }
}
