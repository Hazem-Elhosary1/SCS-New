using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OwnerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "identity",
                table: "StaffAccount",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSignInAtUtc",
                schema: "identity",
                table: "StaffAccount",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "identity",
                table: "StaffAccount",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffAccount_Mobile",
                schema: "identity",
                table: "StaffAccount",
                sql: "[Mobile] IS NULL OR (LEN([Mobile]) BETWEEN 8 AND 16 AND [Mobile] NOT LIKE '%[^0-9+]%' AND CHARINDEX('+', [Mobile], 2) = 0)");

            // «آخر تسجيل دخول» for accounts that signed in before this column: their latest session
            // still kept (spec 015, data-model.md §2). The migrator works across clinics.
            migrationBuilder.Sql(
                """
                UPDATE a SET a.[LastSignInAtUtc] = s.[Latest]
                FROM [identity].[StaffAccount] AS a
                CROSS APPLY (SELECT MAX(x.[CreatedAtUtc]) AS [Latest] FROM [identity].[Session] AS x WHERE x.[AccountId] = a.[Id]) AS s
                WHERE s.[Latest] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffAccount_Mobile",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "LastSignInAtUtc",
                schema: "identity",
                table: "StaffAccount");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "identity",
                table: "StaffAccount");
        }
    }
}
