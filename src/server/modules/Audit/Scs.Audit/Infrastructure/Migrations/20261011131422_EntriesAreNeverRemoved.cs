using Microsoft.EntityFrameworkCore.Migrations;
using Scs.BuildingBlocks.Audit;

#nullable disable

namespace Scs.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntriesAreNeverRemoved : Migration
    {
        /// <summary>
        /// The trigger as the first migration of this module once created it: it let a member of
        /// the cross-clinic role delete an entry. The application holds such a login, so that was
        /// a way for the application to remove one (spec 007, FR-021).
        /// </summary>
        private const string TriggerThatAllowedRetention =
            """
            CREATE TRIGGER [audit].[TR_Entry_AppendOnly]
            ON [audit].[Entry]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                    RETURN;

                IF EXISTS (SELECT 1 FROM inserted) OR IS_ROLEMEMBER(N'scs_cross_clinic') <> 1
                    THROW 51007, N'Audit entries cannot be changed, and are removed only by retention.', 1;
            END
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No structure changes: the trigger now refuses a delete for everyone.
            migrationBuilder.Sql(AuditSql.DropAppendOnlyTrigger);
            migrationBuilder.Sql(AuditSql.CreateAppendOnlyTrigger);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditSql.DropAppendOnlyTrigger);
            migrationBuilder.Sql(TriggerThatAllowedRetention);
        }
    }
}
