namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// The one list of database modules (procedures, functions, triggers) that run as another user
/// and can therefore see across clinics. Beside <see cref="DeclaredCrossings"/>, which lists the
/// application types that may use the operator connection, this is the whole of what can cross
/// the clinic boundary. The isolation guardrail reads the database's catalog and fails the build
/// for any module that runs as another user and is not listed here.
/// </summary>
public static class DeclaredDatabaseCrossings
{
    /// <summary>The user without a login that the sign-in lookup runs as.</summary>
    public const string SignInLookupUser = "scs_signin_lookup";

    public const string FindAccountForSignIn = "identity.usp_FindAccountForSignIn";

    public static IReadOnlyList<DatabaseCrossing> All { get; } =
    [
        new(
            FindAccountForSignIn,
            SignInLookupUser,
            "Before sign-in there is no clinic. Given an email, returns the account's id and its clinic's id and nothing else, so the request can continue under ordinary isolation (spec 003, FR-056)."),
    ];

    public static bool Contains(string schemaQualifiedName, string runsAs) =>
        All.Any(crossing =>
            string.Equals(crossing.Name, schemaQualifiedName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(crossing.RunsAs, runsAs, StringComparison.OrdinalIgnoreCase));
}

/// <param name="Name">Schema-qualified name of the module.</param>
/// <param name="RunsAs">The database user it runs as.</param>
/// <param name="Reason">Why nothing narrower works.</param>
public sealed record DatabaseCrossing(string Name, string RunsAs, string Reason);
