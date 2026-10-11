namespace Scs.IntegrationTests.Fixtures.Domain;

/// <summary>
/// Stands in for a module's domain entity: it lives in a <c>Domain</c> namespace and carries
/// no classification attributes at all, as real entities will not.
/// </summary>
public sealed record ProbePatient(int Id, string FullName, string NationalId);
