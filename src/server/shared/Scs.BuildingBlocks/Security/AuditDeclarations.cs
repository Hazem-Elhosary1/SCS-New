using Microsoft.AspNetCore.Builder;

namespace Scs.BuildingBlocks.Security;

/// <summary>What a route that changes something says about the audit trail.</summary>
public enum AuditDeclarationKind
{
    /// <summary>It records these actions of the catalogue.</summary>
    Records = 0,

    /// <summary>It changes nothing a person would trace; the reason is written beside it.</summary>
    RecordsNothing = 1,

    /// <summary>Its effect is a sign-in event, which the sign-in code carries into the trail.</summary>
    RecordedBySignIn = 2,
}

/// <summary>
/// What a changing route declares about the audit trail (spec 007, FR-023): the actions it
/// records, or why it records none. A test fails for a <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or
/// <c>DELETE</c> route that carries none, so a later spec cannot forget to record.
/// </summary>
public sealed class AuditDeclarationMetadata
{
    private AuditDeclarationMetadata(AuditDeclarationKind kind, IReadOnlyList<string> actions, string? reason)
    {
        Kind = kind;
        Actions = actions;
        Reason = reason;
    }

    public AuditDeclarationKind Kind { get; }

    /// <summary>Codes of the audit catalogue; empty unless <see cref="AuditDeclarationKind.Records"/>.</summary>
    public IReadOnlyList<string> Actions { get; }

    /// <summary>Why nothing is recorded; only for <see cref="AuditDeclarationKind.RecordsNothing"/>.</summary>
    public string? Reason { get; }

    public static AuditDeclarationMetadata Records(params string[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Length == 0
            ? throw new ArgumentException("Name at least one action of the audit catalogue.", nameof(actions))
            : new(AuditDeclarationKind.Records, [.. actions], null);
    }

    public static AuditDeclarationMetadata RecordsNothing(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Say why nothing is recorded.", nameof(reason))
            : new(AuditDeclarationKind.RecordsNothing, [], reason.Trim());

    public static AuditDeclarationMetadata RecordedBySignIn { get; } = new(AuditDeclarationKind.RecordedBySignIn, [], null);
}

/// <summary>
/// On a scheduled job: the actions it records. A job changes data with nobody signed in, so its
/// entries name the platform as the actor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecordsAttribute(params string[] actions) : Attribute
{
    public IReadOnlyList<string> Actions { get; } = actions;
}

/// <summary>On a scheduled job: why it records nothing.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecordsNothingAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}

public static class AuditDeclarationExtensions
{
    /// <summary>The route records these actions. Use constants of <c>AuditActions</c>.</summary>
    public static TBuilder Records<TBuilder>(this TBuilder builder, params string[] actions)
        where TBuilder : IEndpointConventionBuilder =>
        builder.Declare(AuditDeclarationMetadata.Records(actions));

    /// <summary>The route changes nothing a person would trace. Say why.</summary>
    public static TBuilder RecordsNothing<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder =>
        builder.Declare(AuditDeclarationMetadata.RecordsNothing(reason));

    /// <summary>The route's effect is a sign-in event; the sign-in code writes its entry.</summary>
    public static TBuilder RecordedBySignIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.Declare(AuditDeclarationMetadata.RecordedBySignIn);

    private static TBuilder Declare<TBuilder>(this TBuilder builder, AuditDeclarationMetadata declaration)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<AuditDeclarationMetadata>().Any())
            {
                throw new InvalidOperationException(
                    $"The route '{endpoint.DisplayName}' declares what it records more than once.");
            }

            endpoint.Metadata.Add(declaration);
        });
        return builder;
    }
}
