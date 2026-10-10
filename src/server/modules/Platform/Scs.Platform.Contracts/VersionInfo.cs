namespace Scs.Platform.Contracts;

/// <summary>Which release is running (contracts/platform-api.openapi.yaml, <c>Version</c>).</summary>
public sealed record VersionInfo(string Version, string Commit, DateTimeOffset BuiltAt, string Environment);
