using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec 015: the clinic's profile as an operator adds and corrects it. Only adds; the
            // clinics seeded before keep empty values (docs/database-changes.md).
            migrationBuilder.AddColumn<Guid>(
                name: "AccountManagerId",
                schema: "tenancy",
                table: "Clinic",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "tenancy",
                table: "Clinic",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "tenancy",
                table: "Clinic",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                schema: "tenancy",
                table: "Clinic",
                type: "varchar(2)",
                unicode: false,
                nullable: false,
                defaultValue: "ar");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "tenancy",
                table: "Clinic",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                schema: "tenancy",
                table: "Clinic",
                type: "char(11)",
                unicode: false,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clinic_DefaultLanguage",
                schema: "tenancy",
                table: "Clinic",
                sql: "[DefaultLanguage] IN ('ar', 'en')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clinic_Specialty",
                schema: "tenancy",
                table: "Clinic",
                sql: "[Specialty] IS NULL OR [Specialty] IN ('dental', 'orthopedics', 'neurology', 'pediatrics', 'internal_medicine', 'general_surgery', 'dermatology', 'obgyn', 'ophthalmology', 'ent', 'urology', 'cardiology', 'psychiatry', 'nutrition', 'physiotherapy')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clinic_State",
                schema: "tenancy",
                table: "Clinic",
                sql: "[State] IN ('setting-up', 'active', 'suspended', 'cancelled', 'discarded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clinic_TaxNumber",
                schema: "tenancy",
                table: "Clinic",
                sql: "[TaxNumber] IS NULL OR [TaxNumber] LIKE '[0-9][0-9][0-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9]'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clinic_DefaultLanguage",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clinic_Specialty",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clinic_State",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clinic_TaxNumber",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "AccountManagerId",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "tenancy",
                table: "Clinic");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                schema: "tenancy",
                table: "Clinic");
        }
    }
}
