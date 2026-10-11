using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "JobHeartbeat",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRunVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobHeartbeat", x => x.Id);
                    table.CheckConstraint("CK_JobHeartbeat_SingleRow", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "JobHeartbeat",
                columns: new[] { "Id", "JobName", "LastRunAtUtc", "LastRunVersion" },
                values: new object[] { (short)1, "platform-heartbeat", new DateTimeOffset(new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "never" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobHeartbeat",
                schema: "platform");
        }
    }
}
