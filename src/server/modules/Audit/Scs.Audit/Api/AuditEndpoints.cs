using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.Audit.Contracts;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.SharedKernel.Access;

namespace Scs.Audit.Api;

/// <summary>
/// The clinic's reading of its audit trail (contracts/audit-api.openapi.yaml). It has no screen
/// yet: «سجل الأنشطة والأمان» is built on it by spec 059.
/// </summary>
internal static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/audit/entries", async (HttpRequest request, IClinicAuditReading reading, CancellationToken cancellation) =>
                TypedResults.Ok(await reading.ReadAsync(Read(request), cancellation)))
            .Requires(Permissions.ReportsActivity)
            .WithName("ListAuditEntries");

        return v1;
    }

    /// <summary>
    /// Reads the question as it was typed. What cannot be read at all is refused here with the
    /// same field codes the reading uses, so a caller meets one kind of answer.
    /// </summary>
    private static AuditQuery Read(HttpRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        string? Text(string name) => request.Query[name].ToString() is { Length: > 0 } value ? value : null;

        DateTimeOffset? Moment(string name)
        {
            if (Text(name) is not { } value)
            {
                return null;
            }

            if (DateTimeOffset.TryParse(
                    value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
            {
                return moment;
            }

            errors["period"] = ["INVALID"];
            return null;
        }

        int Number(string name, int otherwise)
        {
            if (Text(name) is not { } value)
            {
                return otherwise;
            }

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            errors[name] = ["INVALID"];
            return otherwise;
        }

        Guid? actor = null;
        if (Text("actor") is { } typed)
        {
            if (Guid.TryParse(typed, out var id))
            {
                actor = id;
            }
            else
            {
                errors["actor"] = ["INVALID"];
            }
        }

        var query = new AuditQuery(
            Moment("from"),
            Moment("to"),
            Text("category"),
            actor,
            Text("action"),
            Text("q"),
            Number("page", 1),
            Number("size", AuditQuery.DefaultSize),
            ErrorMessages.ResolveLanguage(request));

        return errors.Count == 0 ? query : throw ScsProblemException.Validation(errors);
    }
}
