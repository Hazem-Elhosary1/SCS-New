using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Plans.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FeatureCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plans");

            migrationBuilder.CreateTable(
                name: "Feature",
                schema: "plans",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Group = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    UnitAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    UnitEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Baseline = table.Column<int>(type: "int", nullable: true),
                    Specialty = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    Status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    IsSold = table.Column<bool>(type: "bit", nullable: false),
                    IsRetired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feature", x => x.Key);
                    table.CheckConstraint("CK_Feature_Baseline", "[Baseline] IS NULL OR [Baseline] BETWEEN 0 AND 1000000");
                    table.CheckConstraint("CK_Feature_Group", "[Group] IN ('core', 'channels', 'reports', 'money', 'clinical', 'ai', 'growth')");
                    table.CheckConstraint("CK_Feature_Key", "LEN([Key]) BETWEEN 2 AND 64 AND [Key] NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_BIN2 AND LEFT([Key], 1) LIKE '[a-z]' COLLATE Latin1_General_BIN2");
                    table.CheckConstraint("CK_Feature_Kind", "[Kind] IN ('switch', 'limit')");
                    table.CheckConstraint("CK_Feature_Limit", "[Kind] <> 'limit' OR ([UnitAr] IS NOT NULL AND [UnitEn] IS NOT NULL)");
                    table.CheckConstraint("CK_Feature_Retired", "[IsRetired] = 0 OR [IsSold] = 1");
                    table.CheckConstraint("CK_Feature_Specialty", "[Specialty] IS NULL OR [Specialty] IN ('dental', 'orthopedics', 'neurology', 'pediatrics', 'internal_medicine', 'general_surgery', 'dermatology', 'obgyn', 'ophthalmology', 'ent', 'urology', 'cardiology', 'psychiatry', 'nutrition', 'physiotherapy')");
                    table.CheckConstraint("CK_Feature_Status", "[Status] IN ('built', 'upcoming')");
                    table.CheckConstraint("CK_Feature_Switch", "[Kind] <> 'switch' OR ([UnitAr] IS NULL AND [UnitEn] IS NULL AND [Baseline] IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feature_Order",
                schema: "plans",
                table: "Feature",
                columns: new[] { "Group", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feature",
                schema: "plans");
        }
    }
}
