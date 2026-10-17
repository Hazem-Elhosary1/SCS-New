using Microsoft.AspNetCore.Http;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Errors;

/// <summary>
/// Thrown by application code to refuse a request with a known code. The exception message is
/// for logs only and is never returned to the caller.
/// </summary>
public class ScsProblemException(int status, string code, string? logMessage = null)
    : Exception(logMessage ?? code)
{
    public int Status { get; } = status;

    public string Code { get; } = code;

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Extra members of the answer that a client needs to act (a time, a count). Never personal data.</summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; init; }

    /// <summary>
    /// What fills the placeholders of the code's words, in each language — <c>{feature}</c> with the
    /// feature's name, say (spec 014). Never personal data.
    /// </summary>
    public IReadOnlyDictionary<string, TitleValue>? TitleValues { get; init; }

    /// <summary>A refusal for invalid input; <paramref name="errors"/> maps field name to field codes.</summary>
    public static ScsProblemException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed) { Errors = errors };
}

/// <summary>One placeholder's value in Arabic and in English.</summary>
public sealed record TitleValue(string Ar, string En)
{
    /// <summary>The same in both languages: a number, a code.</summary>
    public static TitleValue Both(string value) => new(value, value);
}
