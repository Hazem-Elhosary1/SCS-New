using Scs.BuildingBlocks.Tenancy;

namespace Scs.Identity.Infrastructure.Persistence;

/// <summary>
/// The hand-written SQL of the Identity module. Once a release has applied a migration that
/// uses a statement here, that statement is frozen: change behaviour with a new migration.
/// </summary>
internal static class IdentitySql
{
    public const string FindAccountProcedure = "usp_FindAccountForSignIn";

    /// <summary>
    /// The one declared database crossing of sign-in (spec 003, FR-056). Before sign-in there is
    /// no clinic, and with no clinic the account table shows nothing. This procedure runs as a
    /// user without a login that isolation lets see every clinic, and returns two identifiers
    /// and nothing else. That user holds no permission of its own: the procedure reads the
    /// table because both belong to the same owner, so nothing else can be read as it.
    /// </summary>
    public const string CreateFindAccountForSignIn =
        $"""
         CREATE PROCEDURE [{IdentityDbContext.Schema}].[{FindAccountProcedure}]
             @Email nvarchar(254)
         WITH EXECUTE AS '{DeclaredDatabaseCrossings.SignInLookupUser}'
         AS
         BEGIN
             SET NOCOUNT ON;

             SELECT TOP (1) [Id] AS [AccountId], [ClinicId]
             FROM [{IdentityDbContext.Schema}].[StaffAccount]
             WHERE [Email] = @Email;
         END
         """;

    public const string DropFindAccountForSignIn =
        $"DROP PROCEDURE [{IdentityDbContext.Schema}].[{FindAccountProcedure}];";

    /// <summary>
    /// Sign-in events are written once. Only the logins an administrator placed in the
    /// cross-clinic role may remove them (for retention); nobody may change one.
    /// </summary>
    public const string CreateEventsAppendOnlyTrigger =
        $"""
         CREATE TRIGGER [{IdentityDbContext.Schema}].[{IdentityDbContext.EventsAppendOnlyTrigger}]
         ON [{IdentityDbContext.Schema}].[SignInEvent]
         AFTER UPDATE, DELETE
         AS
         BEGIN
             SET NOCOUNT ON;

             IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                 RETURN;

             IF EXISTS (SELECT 1 FROM inserted) OR IS_ROLEMEMBER(N'{ClinicIsolationSql.CrossClinicRole}') <> 1
                 THROW 51003, N'Sign-in events cannot be changed, and are removed only by retention.', 1;
         END
         """;

    public const string DropEventsAppendOnlyTrigger =
        $"DROP TRIGGER [{IdentityDbContext.Schema}].[{IdentityDbContext.EventsAppendOnlyTrigger}];";

    /// <summary>The statement the application runs to use the lookup.</summary>
    public const string FindAccountForSignIn =
        $"EXEC [{IdentityDbContext.Schema}].[{FindAccountProcedure}] @Email = @email";
}
