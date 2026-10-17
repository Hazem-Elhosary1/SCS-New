using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Plans.Infrastructure.Migrations
{
    /// <summary>
    /// «المستخدمون» is built: spec 014 counts and enforces it (FR-018). The spec that builds a
    /// feature marks it, in its own migration (spec 012).
    /// </summary>
    public partial class MaxUsersBuilt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [plans].[Feature] SET [Status] = 'built' WHERE [Key] = 'max_users' AND [Status] = 'upcoming';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [plans].[Feature] SET [Status] = 'upcoming' WHERE [Key] = 'max_users';");
        }
    }
}
