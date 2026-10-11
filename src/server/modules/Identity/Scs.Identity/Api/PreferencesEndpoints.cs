using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;

namespace Scs.Identity.Api;

/// <summary>
/// What the signed-in person keeps between sessions (contracts/shell-api.openapi.yaml). Their
/// own account's business, so open to any signed-in person: one of the three such routes
/// (specs/005-permissions-engine/contracts/access-engine.md §1).
/// </summary>
internal static class PreferencesEndpoints
{
    private const string Invalid = "INVALID";

    public static IEndpointRouteBuilder MapPreferences(this IEndpointRouteBuilder v1)
    {
        v1.MapPut("/account/preferences", SaveAsync)
            .OwnAccountOnly()
            .RecordsNothing("display preferences are never recorded (spec 007, FR-019)")
            .WithName("SavePreferences");

        return v1;
    }

    /// <summary>
    /// The body is read member by member, because "not sent" and "sent" mean different things:
    /// only what is sent is changed.
    /// </summary>
    private static async Task<IResult> SaveAsync(
        JsonElement body, ICurrentStaff staff, Preferences preferences, CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string? language = null;
        bool? sidebarFolded = null;
        string? dismissedBanner = null;
        var sent = 0;

        if (body.ValueKind == JsonValueKind.Object)
        {
            if (body.TryGetProperty("language", out var given))
            {
                sent++;
                language = OneOf(given, PreferenceValues.Languages);
                if (language is null)
                {
                    errors["language"] = [Invalid];
                }
            }

            if (body.TryGetProperty("sidebarFolded", out given))
            {
                sent++;
                if (given.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    sidebarFolded = given.GetBoolean();
                }
                else
                {
                    errors["sidebarFolded"] = [Invalid];
                }
            }

            if (body.TryGetProperty("dismissedBanner", out given))
            {
                sent++;
                dismissedBanner = OneOf(given, PreferenceValues.ClosableBanners);
                if (dismissedBanner is null)
                {
                    errors["dismissedBanner"] = [Invalid];
                }
            }
        }

        if (sent == 0)
        {
            errors["body"] = ["REQUIRED"];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        return TypedResults.Ok(
            await preferences.SaveAsync(staff.AccountId, language, sidebarFolded, dismissedBanner, cancellation));
    }

    private static string? OneOf(JsonElement given, IReadOnlyList<string> choices)
    {
        var text = given.ValueKind == JsonValueKind.String ? given.GetString() : null;
        return text is not null && choices.Contains(text, StringComparer.Ordinal) ? text : null;
    }
}
