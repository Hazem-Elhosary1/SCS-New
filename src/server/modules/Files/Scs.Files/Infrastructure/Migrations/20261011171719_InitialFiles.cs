using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Tenancy;

#nullable disable

namespace Scs.Files.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.CreateTable(
                name: "RetentionPeriod",
                schema: "files",
                columns: table => new
                {
                    Class = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: true),
                    SetAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPeriod", x => x.Class);
                    table.CheckConstraint("CK_RetentionPeriod_Days", "[Days] IS NULL OR [Days] > 0");
                });

            migrationBuilder.CreateTable(
                name: "StoredFile",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    UploadedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Scan = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    RefusedFor = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScanTries = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecordKind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RestoredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RestoredByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetainFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HeldAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HeldByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReleasedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErasedFor = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFile", x => x.Id);
                    table.CheckConstraint("CK_StoredFile_Erased", "([ErasedAtUtc] IS NULL AND [ErasedFor] IS NULL) OR ([ErasedAtUtc] IS NOT NULL AND [ErasedFor] IN ('removed', 'age', 'refused'))");
                    table.CheckConstraint("CK_StoredFile_Held", "[HeldAtUtc] IS NULL OR [RecordId] IS NOT NULL");
                    table.CheckConstraint("CK_StoredFile_Record", "([RecordKind] IS NULL AND [RecordId] IS NULL AND [AttachedAtUtc] IS NULL) OR ([RecordKind] IS NOT NULL AND [RecordId] IS NOT NULL AND [AttachedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_StoredFile_RefusedFor", "([Scan] = 'refused' AND [RefusedFor] IN ('infected', 'unreadable')) OR ([Scan] <> 'refused' AND [RefusedFor] IS NULL)");
                    table.CheckConstraint("CK_StoredFile_Removed", "[RemovedAtUtc] IS NULL OR [RecordId] IS NOT NULL");
                    table.CheckConstraint("CK_StoredFile_Scan", "[Scan] IN ('pending', 'clean', 'refused')");
                    table.CheckConstraint("CK_StoredFile_Size", "[SizeBytes] > 0");
                    table.CheckConstraint("CK_StoredFile_Type", "[Type] IN ('pdf', 'png', 'jpeg')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFile_Pending",
                schema: "files",
                table: "StoredFile",
                columns: new[] { "ClinicId", "UploadedAtUtc" },
                filter: "[Scan] = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFile_Record",
                schema: "files",
                table: "StoredFile",
                columns: new[] { "ClinicId", "RecordKind", "RecordId" })
                .Annotation("SqlServer:Include", new[] { "RemovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFile_Removed",
                schema: "files",
                table: "StoredFile",
                columns: new[] { "ClinicId", "RemovedAtUtc" },
                filter: "[RemovedAtUtc] IS NOT NULL AND [ErasedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFile_Retained",
                schema: "files",
                table: "StoredFile",
                columns: new[] { "ClinicId", "Kind", "RetainFromUtc" },
                filter: "[RetainFromUtc] IS NOT NULL AND [ErasedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFile_Uploader",
                schema: "files",
                table: "StoredFile",
                columns: new[] { "ClinicId", "UploadedByAccountId", "UploadedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "RecordId" });

            // The four classes of SH-095, with no period: nothing is erased for its age until one is set.
            migrationBuilder.InsertData(
                schema: FilesSql.Schema,
                table: FilesSql.RetentionPeriod,
                columns: ["Class"],
                values: new object[,]
                {
                    { "medical-record" },
                    { "conversations" },
                    { "ticket-attachments" },
                    { "clinic-data" },
                });

            migrationBuilder.CreateClinicIsolation(FilesSql.Schema, FilesSql.StoredFile);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropClinicIsolation(FilesSql.Schema, FilesSql.StoredFile);
            migrationBuilder.DropTable(
                name: "RetentionPeriod",
                schema: "files");

            migrationBuilder.DropTable(
                name: "StoredFile",
                schema: "files");
        }
    }
}
