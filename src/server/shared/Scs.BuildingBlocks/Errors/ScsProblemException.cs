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

    /// <summary>A refusal for invalid input; <paramref name="errors"/> maps field name to field codes.</summary>
    public static ScsProblemException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed) { Errors = errors };
}
