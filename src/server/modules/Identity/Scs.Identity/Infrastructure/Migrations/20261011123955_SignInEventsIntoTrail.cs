using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scs.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignInEventsIntoTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No structure changes. What was recorded before the audit trail existed is carried
            // into it once (spec 007): the sign-in events that belong in the trail, and the
            // roles every clinic already has. The Audit module is migrated before this one.
            foreach (var statement in TrailBackfillSql.All)
            {
                migrationBuilder.Sql(statement);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing is taken back: an entry of the trail is never removed (FR-021). Applying
            // the migration again adds only what is not there.
        }
    }
}
