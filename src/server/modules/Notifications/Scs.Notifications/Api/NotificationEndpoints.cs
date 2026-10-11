using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.Notifications.Contracts;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Notifications.Api;

internal sealed record SwitchesResponse(IReadOnlyList<EmailSwitchView> Switches);

/// <summary>
/// The bell's addresses and the clinic's email switches (contracts/notifications-api.openapi.yaml).
/// The bell's four are about the signed-in person's own notifications and need no permission
/// beyond being signed in (FR-028). The switches have no screen yet: the page «التنبيهات» is
/// built on them by spec 084.
/// </summary>
internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/notifications/state", async (ICurrentStaff staff, IBell bell, CancellationToken cancellation) =>
                TypedResults.Ok(await bell.StateAsync(staff.AccountId, cancellation)))
            .OwnAccountOnly()
            .WithName("GetBellState");

        v1.MapGet("/notifications", async (HttpRequest request, ICurrentStaff staff, IBell bell, CancellationToken cancellation) =>
            {
                var size = NotificationPage.DefaultSize;
                var asked = request.Query["size"].ToString();
                if (asked.Length > 0 && !int.TryParse(asked, NumberStyles.None, CultureInfo.InvariantCulture, out size))
                {
                    throw ScsProblemException.Validation(
                        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["size"] = ["INVALID"] });
                }

                var before = request.Query["before"].ToString();
                return TypedResults.Ok(await bell.ListAsync(
                    staff.AccountId, before.Length == 0 ? null : before, size, ErrorMessages.ResolveLanguage(request), cancellation));
            })
            .OwnAccountOnly()
            .WithName("ListNotifications");

        v1.MapPost("/notifications/{id:guid}/read", async (Guid id, ICurrentStaff staff, IBell bell, CancellationToken cancellation) =>
            {
                await bell.MarkReadAsync(staff.AccountId, id, cancellation);
                return TypedResults.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing("a person's own read marks")
            .WithName("MarkNotificationRead");

        v1.MapPost("/notifications/read-all", async (ICurrentStaff staff, IBell bell, CancellationToken cancellation) =>
            {
                await bell.MarkAllReadAsync(staff.AccountId, cancellation);
                return TypedResults.NoContent();
            })
            .OwnAccountOnly()
            .RecordsNothing("a person's own read marks")
            .WithName("MarkAllNotificationsRead");

        v1.MapGet("/settings/notification-emails", async (HttpRequest request, IEmailSwitches switches, CancellationToken cancellation) =>
                TypedResults.Ok(new SwitchesResponse(await switches.ListAsync(ErrorMessages.ResolveLanguage(request), cancellation))))
            .Requires(Permissions.SettingsView)
            .WithName("ListNotificationEmailSwitches");

        v1.MapPut("/settings/notification-emails/{eventKey}", async (
                string eventKey, JsonElement body, HttpRequest request, IEmailSwitches switches, CancellationToken cancellation) =>
            {
                if (body.ValueKind != JsonValueKind.Object
                    || !body.TryGetProperty("enabled", out var enabled)
                    || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw ScsProblemException.Validation(
                        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["enabled"] = ["REQUIRED"] });
                }

                var set = await switches.SetAsync(eventKey, enabled.GetBoolean(), ErrorMessages.ResolveLanguage(request), cancellation);
                return TypedResults.Ok(new { @event = set.Event, enabled = set.Enabled });
            })
            .Requires(Permissions.SettingsNotifications)
            .Records(AuditActions.SettingsNotificationEmailChanged)
            .WithName("SetNotificationEmailSwitch");

        return v1;
    }
}
